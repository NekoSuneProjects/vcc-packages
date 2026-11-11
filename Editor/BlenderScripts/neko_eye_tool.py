"""
Neko Eye Installer - Blender side.

Runs headless under Blender 4.0.2:

    blender --background --factory-startup --python neko_eye_tool.py -- --input=... --output=...

Eye bones are optional. Plenty of avatars have none, so placement, removal and skinning are
each independently sourced:

  placement  explicit centres/radii sent from the installer, or measured from eye-bone weights
  removal    bone weights, a sphere around each placement, named materials, or nothing
  skinning   the eye bones if they exist, otherwise the head bone

The new eyes are built as their own object rather than joined into the body, which keeps the
body's shape keys untouched - that is where mesh-merging eye installers usually go wrong.

Progress is reported on stdout as "Status: ..." and "Progress: 0.42", failures as "Error: ...".
The Unity side parses those.
"""

import argparse
import json
import math
import os
import sys
import traceback

import bpy
import bmesh
from mathutils import Matrix, Vector

SCRIPT_VERSION = 2

# Bone name fragments, lowercased.
LEFT_MARKERS = ("left", "_l", ".l", "l_", "hidari", "左")   # 左
RIGHT_MARKERS = ("right", "_r", ".r", "r_", "migi", "右")   # 右

# Covers English rigs, VRM/VRoid ("J_Bip_L_Eye", "J_Adj_L_FaceEye") and MMD-style Japanese.
EYE_BONE_HINTS = ("eye", "eyeball", "hitomi", "me_", "目", "瞳")
HEAD_BONE_HINTS = ("head", "頭")  # 頭

EYE_MATERIAL_HINTS = (
    "eye", "iris", "cornea", "sclera", "pupil", "hitomi",
    "highlight", "eyehighlight", "eyeline_eye",
)


# ----------------------------------------------------------------------
# Small helpers
# ----------------------------------------------------------------------

def status(message, progress=None):
    print("Status: %s" % message)
    if progress is not None:
        print("Progress: %.3f" % max(0.0, min(1.0, progress)))
    sys.stdout.flush()


def fail(message):
    print("Error: %s" % message)
    sys.stdout.flush()
    raise RuntimeError(message)


def parse_vector(text):
    """Parses 'x,y,z' into a Vector, or returns None when empty."""
    if not text:
        return None

    parts = [p.strip() for p in text.split(",")]
    if len(parts) != 3:
        fail("Expected 'x,y,z' but got '%s'" % text)

    try:
        return Vector((float(parts[0]), float(parts[1]), float(parts[2])))
    except ValueError:
        fail("Could not read numbers from '%s'" % text)


def parse_args():
    argv = sys.argv
    argv = argv[argv.index("--") + 1:] if "--" in argv else []

    parser = argparse.ArgumentParser(prog="neko_eye_tool")
    parser.add_argument("--input", required=True, help="Source avatar FBX")
    parser.add_argument("--output", required=True, help="Destination FBX")
    parser.add_argument("--report", required=True, help="Destination JSON report")
    parser.add_argument("--expected-version", type=int, default=SCRIPT_VERSION)

    parser.add_argument("--left-eye-bone", default="", help="Left eye bone name, if the rig has one")
    parser.add_argument("--right-eye-bone", default="", help="Right eye bone name, if the rig has one")
    parser.add_argument("--skin-bone-left", default="", help="Bone to bind the left eye to")
    parser.add_argument("--skin-bone-right", default="", help="Bone to bind the right eye to")
    parser.add_argument("--target-meshes", default="", help="Semicolon separated mesh names; empty = all")

    parser.add_argument("--eye-style", default="sphere", choices=("sphere", "flat"))
    parser.add_argument("--segments", type=int, default=24)
    parser.add_argument("--rings", type=int, default=16)
    parser.add_argument("--iris-scale", type=float, default=0.55)

    # Manual placement. When supplied these win over measurement, which is what makes the tool
    # usable on rigs with no eye bones at all.
    #
    # With --anchor-bone the centres are an OFFSET from that bone's head, which is how the
    # installer sends them: the scene avatar may be moved, rotated or scaled relative to the FBX,
    # and measuring from a shared bone cancels all of that out. Without it they are absolute.
    parser.add_argument("--anchor-bone", default="",
                        help="Bone the placement offsets are measured from")
    parser.add_argument("--left-eye-center", default="", help="x,y,z offset from the anchor bone")
    parser.add_argument("--right-eye-center", default="", help="x,y,z offset from the anchor bone")
    parser.add_argument("--left-eye-radius", type=float, default=0.0)
    parser.add_argument("--right-eye-radius", type=float, default=0.0)
    parser.add_argument("--forward", default="", help="x,y,z facing override in Blender world space")

    parser.add_argument("--scale", type=float, default=1.0, help="Multiplier on the eye radius")
    parser.add_argument("--depth-offset", type=float, default=0.0, help="Metres along the facing direction")

    parser.add_argument("--delete-mode", default="bones",
                        choices=("bones", "sphere", "material", "none"),
                        help="How to remove the original eye geometry")
    parser.add_argument("--weight-threshold", type=float, default=0.05)
    parser.add_argument("--delete-radius-scale", type=float, default=1.0,
                        help="Multiplier on the eye radius when deleting by sphere")
    parser.add_argument("--eye-materials", default="",
                        help="Semicolon separated material names to treat as eye materials")
    parser.add_argument("--remove-unused-slots", action="store_true")

    parser.add_argument("--detect-only", action="store_true",
                        help="Write the report and make no changes")

    return parser.parse_args(argv)


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def import_fbx(path):
    if not os.path.isfile(path):
        fail("Input FBX does not exist: %s" % path)

    bpy.ops.import_scene.fbx(
        filepath=path,
        use_anim=False,
        ignore_leaf_bones=True,
        automatic_bone_orientation=False,
    )


def find_armature():
    armatures = [o for o in bpy.data.objects if o.type == "ARMATURE"]
    if not armatures:
        fail("No armature found in the FBX. The avatar needs a rig to skin the eyes to.")

    def deform_score(armature):
        return sum(
            1 for o in bpy.data.objects
            if o.type == "MESH"
            and any(m.type == "ARMATURE" and m.object == armature for m in o.modifiers)
        )

    armatures.sort(key=deform_score, reverse=True)
    return armatures[0]


def side_of(name):
    lowered = name.lower()
    for marker in LEFT_MARKERS:
        if marker in lowered:
            return "left"
    for marker in RIGHT_MARKERS:
        if marker in lowered:
            return "right"
    return None


def matches_any(name, hints):
    lowered = name.lower()
    return any(hint in lowered for hint in hints)


def detect_eye_bones(armature, explicit_left, explicit_right):
    """Returns (left, right, candidates). Either name may be None - that is not an error."""
    bones = {b.name: b for b in armature.data.bones}

    left = explicit_left if explicit_left in bones else None
    right = explicit_right if explicit_right in bones else None

    candidates = sorted(
        (name for name in bones if matches_any(name, EYE_BONE_HINTS)),
        key=len,
    )

    if left and right:
        return left, right, candidates

    for name in candidates:
        side = side_of(name)
        if side == "left" and left is None:
            left = name
        elif side == "right" and right is None:
            right = name

    return left, right, candidates


def find_head_bone(armature):
    """Fallback skin target when the rig has no eye bones."""
    bones = [b.name for b in armature.data.bones]

    matches = sorted((n for n in bones if matches_any(n, HEAD_BONE_HINTS)), key=len)
    for name in matches:
        # "Head" beats "HeadTop_End" or a headwear bone.
        if "end" not in name.lower() and "top" not in name.lower():
            return name

    return matches[0] if matches else None


def collect_meshes(armature, names_filter):
    wanted = [n.strip() for n in names_filter.split(";") if n.strip()]

    def skinned(obj):
        return any(m.type == "ARMATURE" and m.object == armature for m in obj.modifiers)

    meshes = [
        obj for obj in bpy.data.objects
        if obj.type == "MESH" and skinned(obj) and (not wanted or obj.name in wanted)
    ]

    if not meshes and wanted:
        # Unity sends the scene object name, which may have been renamed since import.
        print("Info: no mesh matched %s, searching all skinned meshes instead" % wanted)
        meshes = [obj for obj in bpy.data.objects if obj.type == "MESH" and skinned(obj)]

    if not meshes:
        fail("No skinned meshes found for the armature.")

    return meshes


# ----------------------------------------------------------------------
# Measuring the existing eyes
# ----------------------------------------------------------------------

def material_is_eyelike(material, extra_names):
    if material is None:
        return False

    lowered = material.name.lower()
    if any(name and name.lower() in lowered for name in extra_names):
        return True

    return any(hint in lowered for hint in EYE_MATERIAL_HINTS)


def gather_weighted_vertices(mesh_object, group_name, threshold):
    group = mesh_object.vertex_groups.get(group_name)
    if group is None:
        return set()

    group_index = group.index
    matched = set()

    for vertex in mesh_object.data.vertices:
        for element in vertex.groups:
            if element.group == group_index and element.weight >= threshold:
                matched.add(vertex.index)
                break

    return matched


def gather_vertices_in_sphere(mesh_object, centre, radius):
    matrix = mesh_object.matrix_world
    limit = radius * radius

    return {
        v.index for v in mesh_object.data.vertices
        if (matrix @ v.co - centre).length_squared <= limit
    }


def gather_vertices_by_material(mesh_object, extra_names):
    slots = {
        index for index, slot in enumerate(mesh_object.material_slots)
        if material_is_eyelike(slot.material, extra_names)
    }
    if not slots:
        return set()

    matched = set()
    for polygon in mesh_object.data.polygons:
        if polygon.material_index in slots:
            matched.update(polygon.vertices)

    return matched


def measure(mesh_object, vertex_indices):
    if not vertex_indices:
        return None, 0.0

    matrix = mesh_object.matrix_world
    points = [matrix @ mesh_object.data.vertices[i].co for i in vertex_indices]

    minimum = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
    maximum = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))

    size = maximum - minimum
    return (minimum + maximum) * 0.5, max(size.x, size.y, size.z) * 0.5


def facing_direction(left_centre, right_centre, fallback_origin=None):
    """
    The direction both eyes look.

    Derived from the line between the two sockets rather than from a bone: that line is the
    head's lateral axis and is reliable on every rig, and crossing it with world up gives the
    facing direction. Measuring from a head bone out to one eye fails badly - the eye's sideways
    offset dominates the short front-to-back one and the result is tens of degrees off.
    """
    default = Vector((0.0, -1.0, 0.0))

    if left_centre is None or right_centre is None:
        return default

    lateral = left_centre - right_centre
    lateral.z = 0.0
    if lateral.length < 1e-6:
        return default

    forward = lateral.normalized().cross(Vector((0.0, 0.0, 1.0)))
    if forward.length < 1e-6:
        return default
    forward.normalize()

    if fallback_origin is not None:
        outward = ((left_centre + right_centre) * 0.5) - fallback_origin
        outward.z = 0.0
        if outward.length > 1e-6 and forward.dot(outward) < 0.0:
            forward = -forward

    return forward


# ----------------------------------------------------------------------
# Deleting the old eyes
# ----------------------------------------------------------------------

def delete_vertices(mesh_object, vertex_indices):
    """
    Delete by index through the edit-mode operator so shape keys stay consistent.
    A bmesh to_mesh round trip would silently drop or desync them.
    """
    if not vertex_indices:
        return 0

    bpy.context.view_layer.objects.active = mesh_object
    for obj in bpy.context.selected_objects:
        obj.select_set(False)
    mesh_object.select_set(True)

    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="DESELECT")
    bpy.ops.object.mode_set(mode="OBJECT")

    for index in vertex_indices:
        mesh_object.data.vertices[index].select = True

    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.delete(type="VERT")
    bpy.ops.object.mode_set(mode="OBJECT")

    return len(vertex_indices)


# ----------------------------------------------------------------------
# Building the new eyes
# ----------------------------------------------------------------------

def build_eye_mesh(name, centre, radius, forward, style, segments, rings, iris_scale,
                   reference_matrix):
    """
    A UV sphere (or a flattened one) centred on the socket, facing `forward`, with UVs planar
    projected along the facing direction so an iris texture lands dead centre and stays circular.
    """
    mesh = bpy.data.meshes.new(name)
    bm = bmesh.new()

    bmesh.ops.create_uvsphere(bm, u_segments=segments, v_segments=rings, radius=1.0)

    rotation = Vector((0.0, -1.0, 0.0)).rotation_difference(forward).to_matrix().to_4x4()

    if style == "flat":
        # Squash along the facing axis so the eye sits inside the socket like a lens
        # instead of bulging out of the head.
        scale = Matrix.Diagonal((radius, radius * 0.45, radius, 1.0))
    else:
        scale = Matrix.Diagonal((radius, radius, radius, 1.0))

    bmesh.ops.transform(bm, matrix=scale, verts=bm.verts)
    bmesh.ops.transform(bm, matrix=rotation, verts=bm.verts)

    uv_layer = bm.loops.layers.uv.new("UVMap")

    up = Vector((0.0, 0.0, 1.0))
    if abs(forward.dot(up)) > 0.99:
        up = Vector((0.0, 1.0, 0.0))
    right = forward.cross(up).normalized()
    up = right.cross(forward).normalized()

    extent = max(radius, 1e-6) / max(iris_scale, 0.05)

    for face in bm.faces:
        for loop in face.loops:
            local = loop.vert.co
            loop[uv_layer].uv = (
                0.5 + (local.dot(right) / (2.0 * extent)),
                0.5 + (local.dot(up) / (2.0 * extent)),
            )

    # Move into place only now that the UVs are projected, since the projection measures
    # from the sphere's own centre.
    bmesh.ops.translate(bm, verts=bm.verts, vec=centre)

    # Everything above was built in world space. Convert into the space of the mesh the eyes
    # sit alongside, and give the new object that same transform.
    #
    # This matters: an avatar FBX imports with a 0.01 unit scale and an axis rotation on the
    # object, so world-space vertices would be re-read as local ones on export - the eyes come
    # back at the origin, rotated onto their side.
    bmesh.ops.transform(bm, matrix=reference_matrix.inverted(), verts=bm.verts)

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()

    material = bpy.data.materials.get("NekoEye")
    if material is None:
        material = bpy.data.materials.new("NekoEye")
    mesh.materials.append(material)

    mesh.update()

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj.matrix_world = reference_matrix.copy()
    return obj


def skin_to_bone(mesh_object, armature, bone_name):
    """Bind every vertex 100% to a single bone."""
    group = mesh_object.vertex_groups.new(name=bone_name)
    group.add(range(len(mesh_object.data.vertices)), 1.0, "REPLACE")

    modifier = mesh_object.modifiers.new(name="Armature", type="ARMATURE")
    modifier.object = armature

    # Parent without moving the object. Assigning .parent on its own re-evaluates the child
    # through the parent's matrix, which would apply the armature's 0.01 import scale twice.
    world = mesh_object.matrix_world.copy()
    mesh_object.parent = armature
    mesh_object.matrix_parent_inverse = armature.matrix_world.inverted()
    mesh_object.matrix_world = world


def join_objects(objects, name):
    if not objects:
        return None
    if len(objects) == 1:
        objects[0].name = name
        return objects[0]

    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()

    joined = bpy.context.view_layer.objects.active
    joined.name = name
    return joined


# ----------------------------------------------------------------------
# Main
# ----------------------------------------------------------------------

def run(args):
    if args.expected_version != SCRIPT_VERSION:
        fail(
            "Script version mismatch: Unity expects %d, this script is %d. "
            "Reinstall the Neko Eye Installer package."
            % (args.expected_version, SCRIPT_VERSION)
        )

    status("Resetting scene", 0.02)
    reset_scene()

    status("Importing avatar FBX", 0.05)
    import_fbx(args.input)

    status("Locating armature", 0.15)
    armature = find_armature()

    status("Resolving bones", 0.20)
    left_bone, right_bone, candidates = detect_eye_bones(
        armature, args.left_eye_bone, args.right_eye_bone
    )
    has_eye_bones = bool(left_bone and right_bone)

    head_bone = find_head_bone(armature)

    # Skinning target: the eye bones when they exist, otherwise the head, so the eyes at least
    # follow head movement even on a rig that cannot articulate them.
    skin_left = args.skin_bone_left or left_bone or head_bone
    skin_right = args.skin_bone_right or right_bone or head_bone

    bone_names = {b.name for b in armature.data.bones}
    for name in (skin_left, skin_right):
        if name and name not in bone_names:
            fail("Bone '%s' is not in this rig." % name)

    if not skin_left or not skin_right:
        fail(
            "No eye bones and no head bone were found, so there is nothing to attach the eyes to.\n\n"
            "Bones in this file: %s"
            % ", ".join(sorted(bone_names)[:25])
        )

    print("Info: eye bones = %s / %s (found: %s)"
          % (left_bone or "none", right_bone or "none", "yes" if has_eye_bones else "no"))
    print("Info: skinning to %s / %s" % (skin_left, skin_right))

    meshes = collect_meshes(armature, args.target_meshes)

    report = {
        "scriptVersion": SCRIPT_VERSION,
        "armature": armature.name,
        "leftEyeBone": left_bone or "",
        "rightEyeBone": right_bone or "",
        "skinBoneLeft": skin_left,
        "skinBoneRight": skin_right,
        "hasEyeBones": has_eye_bones,
        "headBone": head_bone or "",
        "boneCandidates": candidates,
        "allBones": sorted(bone_names),
        "meshes": [m.name for m in meshes],
        "eyes": {},
        "deletedVertices": 0,
        "detectOnly": bool(args.detect_only),
    }

    # ------------------------------------------------------------------
    # Placement
    # ------------------------------------------------------------------

    status("Working out eye placement", 0.30)

    manual_left = parse_vector(args.left_eye_center)
    manual_right = parse_vector(args.right_eye_center)
    manual = manual_left is not None and manual_right is not None

    # Resolve the shared anchor and turn the incoming offsets into world positions.
    anchor_name = args.anchor_bone
    anchor_position = Vector((0.0, 0.0, 0.0))

    if anchor_name:
        if anchor_name not in bone_names:
            fail("Anchor bone '%s' is not in this rig." % anchor_name)

        anchor_position = armature.matrix_world @ armature.data.bones[anchor_name].head_local
        print("Info: anchor %s at (%.4f, %.4f, %.4f)"
              % (anchor_name, anchor_position.x, anchor_position.y, anchor_position.z))

        if manual:
            manual_left = anchor_position + manual_left
            manual_right = anchor_position + manual_right
    elif manual:
        print("Info: no anchor bone given, treating manual placement as absolute")

    report["anchorBone"] = anchor_name
    report["anchorPosition"] = [anchor_position.x, anchor_position.y, anchor_position.z]

    found = {}

    for side, bone_name, manual_centre, manual_radius in (
        ("left", left_bone, manual_left, args.left_eye_radius),
        ("right", right_bone, manual_right, args.right_eye_radius),
    ):
        mesh_object = meshes[0]
        indices = set()

        # Measure the old geometry whenever we can - it is still needed for bone-weight
        # deletion, and gives a sensible default radius the installer can show.
        if bone_name:
            best = None
            for candidate_mesh in meshes:
                hits = gather_weighted_vertices(candidate_mesh, bone_name, args.weight_threshold)
                if hits and (best is None or len(hits) > len(best[1])):
                    best = (candidate_mesh, hits)

            if best is not None:
                mesh_object, indices = best

        measured_centre, measured_radius = measure(mesh_object, indices)

        centre = manual_centre if manual_centre is not None else measured_centre
        radius = manual_radius if manual_radius > 1e-6 else measured_radius

        if centre is None:
            fail(
                "Nothing to place the %s eye from. This rig has no usable eye bone, and no "
                "manual placement was sent.\n\n"
                "Turn on Manual Placement in the installer and drag the eyes into position."
                % side
            )

        if radius <= 1e-6:
            fail("The %s eye radius came out as zero. Set a size in the installer." % side)

        found[side] = {
            "bone": bone_name or "",
            "object": mesh_object,
            "indices": indices,
            "centre": centre,
            "radius": radius,
        }

        print("Info: %s eye - centre (%.4f, %.4f, %.4f) radius %.4f m, %d original verts%s"
              % (side, centre.x, centre.y, centre.z, radius, len(indices),
                 " (manual)" if manual else ""))

    forward_override = parse_vector(args.forward)
    if forward_override is not None and forward_override.length > 1e-6:
        forward = forward_override.normalized()
    else:
        origin = None
        if head_bone:
            origin = armature.matrix_world @ armature.data.bones[head_bone].head_local

        forward = facing_direction(found["left"]["centre"], found["right"]["centre"], origin)

    print("Info: facing direction = (%.3f, %.3f, %.3f)" % (forward.x, forward.y, forward.z))

    for side in ("left", "right"):
        data = found[side]
        data["forward"] = forward

        report["eyes"][side] = {
            "bone": data["bone"],
            "mesh": data["object"].name,
            "vertexCount": len(data["indices"]),
            "centre": [data["centre"].x, data["centre"].y, data["centre"].z],
            "radius": data["radius"],
            "forward": [forward.x, forward.y, forward.z],
        }

    if args.detect_only:
        status("Detection complete", 1.0)
        write_report(args.report, report)
        return

    # ------------------------------------------------------------------
    # Removal
    # ------------------------------------------------------------------

    mode = args.delete_mode
    if mode == "bones" and not has_eye_bones:
        print("Info: no eye bones, falling back to sphere removal")
        mode = "sphere"

    if mode != "none":
        status("Removing original eye geometry", 0.45)

        extra_materials = [n.strip() for n in args.eye_materials.split(";") if n.strip()]
        per_object = {}

        for side in ("left", "right"):
            data = found[side]

            if mode == "bones":
                target, hits = data["object"], data["indices"]

            elif mode == "sphere":
                # Uses the placement the user dragged into position, so this works on rigs
                # with no eye bones at all.
                radius = data["radius"] * max(args.delete_radius_scale, 0.01)
                target, hits = None, set()
                for mesh_object in meshes:
                    found_here = gather_vertices_in_sphere(mesh_object, data["centre"], radius)
                    if len(found_here) > len(hits):
                        target, hits = mesh_object, found_here

            else:  # material
                target, hits = None, set()
                for mesh_object in meshes:
                    found_here = gather_vertices_by_material(mesh_object, extra_materials)
                    if len(found_here) > len(hits):
                        target, hits = mesh_object, found_here

            if target is not None and hits:
                per_object.setdefault(target.name, set()).update(hits)

        if not per_object:
            print("Info: nothing matched for removal mode '%s'; leaving the original eyes" % mode)

        for object_name, indices in per_object.items():
            mesh_object = bpy.data.objects[object_name]
            report["deletedVertices"] += delete_vertices(mesh_object, indices)

            if args.remove_unused_slots:
                try:
                    bpy.context.view_layer.objects.active = mesh_object
                    bpy.ops.object.material_slot_remove_unused()
                except Exception as error:  # noqa: BLE001 - non-fatal cleanup
                    print("Info: could not remove unused material slots: %s" % error)

    report["deleteMode"] = mode

    # ------------------------------------------------------------------
    # Build and export
    # ------------------------------------------------------------------

    status("Building new eye meshes", 0.65)

    new_objects = []
    for side, skin_bone in (("left", skin_left), ("right", skin_right)):
        data = found[side]
        radius = data["radius"] * max(args.scale, 0.01)
        centre = data["centre"] + forward * args.depth_offset

        obj = build_eye_mesh(
            "NekoEye_%s" % side.capitalize(),
            centre,
            radius,
            forward,
            args.eye_style,
            max(args.segments, 8),
            max(args.rings, 6),
            args.iris_scale,
            data["object"].matrix_world,
        )
        skin_to_bone(obj, armature, skin_bone)
        new_objects.append(obj)

        report["eyes"][side]["newRadius"] = radius
        report["eyes"][side]["skinBone"] = skin_bone

    status("Joining eye meshes", 0.78)
    joined = join_objects(new_objects, "NekoEyes")
    report["eyeObject"] = joined.name if joined else ""

    status("Exporting FBX", 0.88)
    export_dir = os.path.dirname(args.output)
    if export_dir and not os.path.isdir(export_dir):
        os.makedirs(export_dir)

    bpy.ops.object.select_all(action="DESELECT")
    bpy.ops.export_scene.fbx(
        filepath=args.output,
        use_selection=False,
        object_types={"ARMATURE", "MESH"},
        add_leaf_bones=False,
        bake_anim=False,
        use_mesh_modifiers=False,
        mesh_smooth_type="FACE",
        global_scale=1.0,
        apply_scale_options="FBX_SCALE_NONE",
        path_mode="AUTO",
    )

    write_report(args.report, report)
    status("Done", 1.0)


def write_report(path, report):
    directory = os.path.dirname(path)
    if directory and not os.path.isdir(directory):
        os.makedirs(directory)

    with open(path, "w", encoding="utf-8") as handle:
        json.dump(report, handle, indent=2, ensure_ascii=False)

    print("Info: report written to %s" % path)


def main():
    try:
        run(parse_args())
    except Exception as error:  # noqa: BLE001 - surfaced to Unity via the Error: line
        traceback.print_exc()
        print("Error: %s" % error)
        sys.stdout.flush()
        sys.exit(1)


if __name__ == "__main__":
    main()
