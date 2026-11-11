#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using NekoSuneVR.EyeInstaller.UI;

namespace NekoSuneVR.EyeInstaller
{
    /// <summary>
    /// Eye mesh installer that does its geometry work in Blender instead of in C#.
    ///
    /// The setup is: pick the avatar, press Detect, press Install. The eye bones, the old eye
    /// geometry, the socket position and the eye size are all measured from the rig - there is no
    /// vertex painting, no mask texture and no manual bone-weight capture.
    ///
    /// Nothing destructive happens to your project. The source FBX is read, never written; the new
    /// mesh lands in a generated folder; and the scene change is a single undo step.
    /// </summary>
    public class NekoEyeInstaller : EditorWindow
    {
        private const int ScriptVersion = 2;
        private const string GeneratedRoot = "Assets/NekoSuneVR/NekoEyeInstaller/Generated";
        private const string EyeObjectName = "NekoEyes";
        private const string ShaderName = "NekoSuneVR/Eye";

        private enum EyeStyle
        {
            Sphere,
            Flat
        }

        private enum RemovalMode
        {
            BoneWeights,
            PlacementSpheres,
            EyeMaterials,
            KeepOriginal
        }

        // ------------------------------------------------------------------
        // Unity <-> Blender space
        // ------------------------------------------------------------------
        //
        // An avatar FBX imports into Blender Z-up right-handed; Unity is Y-up left-handed. For a
        // character facing Unity +Z the exporter lands it facing Blender -Y, giving the axis swap
        // below. Verified on a real avatar: Blender (0.038, -0.0084, 1.1085) is the left eye, and
        // Unity's LeftEye bone sits at (-0.038, 1.1085, 0.0084).
        //
        // The axis swap alone is not enough. Blender sees the FBX's own frame, while the avatar in
        // the scene may be moved, rotated, scaled or re-parented - a merged avatar almost always
        // is. Converting world positions directly then swings the eyes around the world origin.
        //
        // So positions travel as an OFFSET FROM A SHARED BONE, expressed in the avatar root's own
        // axes. Translation cancels against the anchor, and rotation and scale cancel against the
        // root, leaving only the axis swap - which is fixed and verifiable.

        private static Vector3 SwapToUnity(Vector3 b)
        {
            return new Vector3(-b.x, b.z, -b.y);
        }

        private static Vector3 SwapToBlender(Vector3 u)
        {
            return new Vector3(-u.x, -u.z, u.y);
        }

        /// <summary>The bone both Unity and Blender can measure from. Hips is present on every rig.</summary>
        private Transform ResolveAnchorBone()
        {
            if (avatarRoot == null)
                return null;

            var animator = avatarRoot.GetComponent<Animator>();
            if (animator != null && animator.isHuman)
            {
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                if (hips != null)
                    return hips;
            }

            return faceMesh != null ? faceMesh.rootBone : null;
        }

        /// <summary>Uniform scale between the scene avatar and the FBX it was imported from.</summary>
        private float AvatarScale()
        {
            if (avatarRoot == null) return 1f;

            float scale = avatarRoot.transform.lossyScale.x;
            return Mathf.Abs(scale) < 1e-6f ? 1f : scale;
        }

        private Vector3 WorldToBlenderOffset(Vector3 world, Transform anchor)
        {
            // InverseTransformVector, not ...Point: this is a displacement, so it must not pick up
            // the root's own translation - only its rotation and scale.
            Vector3 local = avatarRoot.transform.InverseTransformVector(world - anchor.position);
            return SwapToBlender(local);
        }

        private Vector3 BlenderOffsetToWorld(Vector3 blenderOffset, Transform anchor)
        {
            return anchor.position + avatarRoot.transform.TransformVector(SwapToUnity(blenderOffset));
        }

        [SerializeField] private GameObject avatarRoot;
        [SerializeField] private SkinnedMeshRenderer faceMesh;
        [SerializeField] private string leftEyeBoneOverride = string.Empty;
        [SerializeField] private string rightEyeBoneOverride = string.Empty;

        [SerializeField] private EyeStyle eyeStyle = EyeStyle.Sphere;
        [SerializeField, Range(0.4f, 1.6f)] private float eyeScale = 1f;
        [SerializeField, Range(-0.02f, 0.02f)] private float depthOffset = 0f;
        [SerializeField, Range(0.2f, 1f)] private float irisScale = 0.55f;
        [SerializeField, Range(8, 48)] private int segments = 24;
        [SerializeField, Range(6, 32)] private int rings = 16;

        [SerializeField, Range(0.01f, 0.5f)] private float weightThreshold = 0.05f;
        [SerializeField] private RemovalMode removalMode = RemovalMode.BoneWeights;
        [SerializeField, Range(0.5f, 2f)] private float deleteRadiusScale = 1f;
        [SerializeField] private string eyeMaterialNames = string.Empty;
        [SerializeField] private bool removeUnusedSlots = true;

        [SerializeField] private Material eyeMaterial;
        [SerializeField] private Texture2D irisTexture;

        [Header("Manual placement, in Unity world space")]
        [SerializeField] private bool manualPlacement;
        [SerializeField] private bool showSceneHandles = true;
        [SerializeField] private bool mirrorEyes = true;
        [SerializeField] private Vector3 leftEyeCenter;
        [SerializeField] private Vector3 rightEyeCenter;
        [SerializeField] private float leftEyeRadius = 0.03f;
        [SerializeField] private float rightEyeRadius = 0.03f;
        [SerializeField] private bool hasPlacement;

        private Vector2 scroll;
        private string blenderPath;
        private string blenderVersion;
        private string detectionSummary = string.Empty;
        private string lastLogPath = string.Empty;
        private bool busy;

        [MenuItem("Tools/NekoSuneVR/Eye Installer", false, 41)]
        public static void Open()
        {
            var window = GetWindow<NekoEyeInstaller>(true, "Eye Installer", true);
            window.minSize = new Vector2(520, 700);
            window.RefreshBlenderStatus();
            window.Show();
        }

        private void OnEnable()
        {
            RefreshBlenderStatus();

            if (avatarRoot == null && Selection.activeGameObject != null)
            {
                var animator = Selection.activeGameObject.GetComponentInParent<Animator>();
                if (animator != null)
                    avatarRoot = animator.gameObject;
            }

            if (avatarRoot != null && faceMesh == null)
            {
                AutoDetectFaceMesh();
                AutoFillEyeBones();
            }

            SceneView.duringSceneGui += OnSceneGUI;
            UpdateChecker.Changed += Repaint;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            UpdateChecker.Changed -= Repaint;
        }

        private void RefreshBlenderStatus()
        {
            blenderPath = NekoBlenderBootstrap.FindBlenderExecutable();
            blenderVersion = string.IsNullOrEmpty(blenderPath)
                ? null
                : NekoBlenderBootstrap.QueryVersion(blenderPath);
        }

        // ------------------------------------------------------------------
        // GUI
        // ------------------------------------------------------------------

        private void OnGUI()
        {
            NekoUI.WindowBackground(this);

            scroll = EditorGUILayout.BeginScrollView(scroll);

            DrawHeader();
            DrawUpdateBanner();

            DrawBlenderSection();
            DrawAvatarSection();
            DrawPlacementSection();
            DrawShapeSection();
            DrawDetectionSection();
            DrawMaterialSection();
            DrawActionSection();

            GUILayout.Space(10f);

            EditorGUILayout.EndScrollView();
            NekoAboutFooter.DrawWithSeparator();
        }

        // ------------------------------------------------------------------
        // Header + update checker
        // ------------------------------------------------------------------

        private static void DrawHeader()
        {
            string current = "v" + UpdateChecker.CurrentVersion;
            string text = current, tip;
            bool update = UpdateChecker.Status == UpdateChecker.State.UpdateAvailable;

            switch (UpdateChecker.Status)
            {
                case UpdateChecker.State.Checking:
                    text = current + "  ···";
                    tip = "Checking for updates...";
                    break;
                case UpdateChecker.State.UpdateAvailable:
                    text = current + "  →  v" + UpdateChecker.LatestVersion;
                    tip = "Update available: v" + UpdateChecker.LatestVersion + "\nClick to check for updates.";
                    break;
                case UpdateChecker.State.UpToDate:
                    tip = "You're up to date.\nClick to check for updates.";
                    break;
                case UpdateChecker.State.Failed:
                    tip = "Couldn't check for updates (offline?). Click to try again.";
                    break;
                default:
                    tip = "Click to check for updates.";
                    break;
            }

            if (NekoUI.Header("Eye Installer", "Blender-powered eye mesh replacement", text, tip, update))
                UpdateChecker.Check();
        }

        /// <summary>Card shown under the header when a newer version is out.</summary>
        private static void DrawUpdateBanner()
        {
            if (!UpdateChecker.ShowBanner) return;

            NekoUI.BeginCard("Update available: v" + UpdateChecker.LatestVersion);
            NekoUI.BeginCardBody();

            NekoUI.Notice(
                "You have v" + UpdateChecker.CurrentVersion + ". Update to get the latest fixes and features." +
                (string.IsNullOrEmpty(UpdateChecker.Notes) ? string.Empty : "\n" + UpdateChecker.Notes),
                NekoUI.Warn);

            GUILayout.Space(5f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (NekoUI.GhostButton("Download update", 140f, 26f)) UpdateChecker.OpenDownload();
                GUILayout.Space(5f);
                if (NekoUI.GhostButton("Later", 80f, 26f)) UpdateChecker.RemindLater();
                GUILayout.Space(5f);
                if (NekoUI.GhostButton("Skip this version", 130f, 26f)) UpdateChecker.SkipThisVersion();
                GUILayout.FlexibleSpace();
            }

            NekoUI.EndCardBody();
            NekoUI.EndCard();
        }

        private void DrawBlenderSection()
        {
            NekoUI.BeginCard("Blender " + NekoBlenderBootstrap.RequiredVersion);
            NekoUI.BeginCardBody();

            bool found = !string.IsNullOrEmpty(blenderPath);
            bool versionOk = found
                             && !string.IsNullOrEmpty(blenderVersion)
                             && blenderVersion.StartsWith(NekoBlenderBootstrap.RequiredVersion, StringComparison.Ordinal);

            DrawStatusRow(
                found ? "Blender " + (blenderVersion ?? "unknown") : "Not found",
                found ? (versionOk ? NekoUI.Good : NekoUI.Warn) : NekoUI.Warn,
                versionOk ? "Ready" : (found ? "Wrong version" : "Missing"));

            if (found)
            {
                NekoUI.FieldLabel(blenderPath);
            }
            else
            {
                NekoUI.FieldLabel(
                    "A portable copy is kept next to your project, outside Assets, so Unity never imports it:\n" +
                    NekoBlenderBootstrap.ExpectedInstallFolder);

                GUILayout.Space(4f);
                if (NekoUI.PrimaryButton("Download Blender " + NekoBlenderBootstrap.RequiredVersion + "  (~300 MB)", 32f))
                {
                    NekoBlenderBootstrap.EnsureBlender();
                    RefreshBlenderStatus();
                }
            }

            GUILayout.Space(4f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (NekoUI.GhostButton("Re-check", 84f))
                    RefreshBlenderStatus();

                GUILayout.Space(5f);

                if (NekoUI.GhostButton("Set Path", 84f))
                {
                    string picked = EditorUtility.OpenFilePanel(
                        "Select blender executable", NekoBlenderBootstrap.ToolsRoot, "");

                    if (!string.IsNullOrEmpty(picked))
                    {
                        NekoBlenderBootstrap.PathOverride = picked;
                        RefreshBlenderStatus();
                    }
                }

                if (!string.IsNullOrEmpty(NekoBlenderBootstrap.PathOverride))
                {
                    GUILayout.Space(5f);
                    if (NekoUI.GhostButton("Clear Override", 104f))
                    {
                        NekoBlenderBootstrap.PathOverride = string.Empty;
                        RefreshBlenderStatus();
                    }
                }

                GUILayout.FlexibleSpace();
            }

            NekoUI.EndCardBody();
            NekoUI.EndCard();
        }

        /// <summary>Dot, label and right-aligned state - matches the hub window's project panel.</summary>
        private static void DrawStatusRow(string label, Color color, string state)
        {
            Rect row = GUILayoutUtility.GetRect(0, int.MaxValue, 20f, 20f);

            NekoUI.RoundedBox(new Rect(row.x + 2f, row.y + 7f, 7f, 7f), color, 3.5f);
            GUI.Label(new Rect(row.x + 16f, row.y, row.width - 110f, row.height), label, NekoUI.Body);

            GUI.Label(new Rect(row.xMax - 106f, row.y, 100f, row.height), state,
                new GUIStyle(NekoUI.Muted) { alignment = TextAnchor.MiddleRight });
        }

        private void DrawAvatarSection()
        {
            NekoUI.BeginCard("Avatar");
            NekoUI.BeginCardBody();

            EditorGUI.BeginChangeCheck();
            avatarRoot = (GameObject)EditorGUILayout.ObjectField("Avatar Root", avatarRoot, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck())
                OnAvatarChanged();

            if (avatarRoot == null)
            {
                NekoUI.FieldLabel("Pick the avatar whose eyes you want to replace.");
                NekoUI.EndCardBody();
                NekoUI.EndCard();
                return;
            }

            GUILayout.Space(4f);

            EditorGUI.BeginChangeCheck();
            faceMesh = (SkinnedMeshRenderer)EditorGUILayout.ObjectField(
                new GUIContent("Face Mesh",
                    "The mesh the eyes are part of. On a merged avatar this decides which FBX is opened, " +
                    "so it must be the body/face mesh rather than an outfit."),
                faceMesh, typeof(SkinnedMeshRenderer), true);
            if (EditorGUI.EndChangeCheck())
                detectionSummary = string.Empty;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (NekoUI.GhostButton("Auto-detect Face Mesh", 170f))
                {
                    AutoDetectFaceMesh();
                    AutoFillEyeBones();
                }
                GUILayout.FlexibleSpace();
            }

            string fbx = ResolveSourceFbx();
            if (faceMesh == null)
            {
                NekoUI.Notice("Choose the mesh your eyes belong to, or press Auto-detect.", NekoUI.Warn);
            }
            else if (string.IsNullOrEmpty(fbx))
            {
                NekoUI.Notice(
                    "\"" + faceMesh.name + "\" does not come from an FBX, so there is no source file to edit. " +
                    "Pick the mesh that was imported from your avatar's FBX.",
                    NekoUI.Bad);
            }
            else
            {
                NekoUI.FieldLabel("Source FBX:  " + fbx);
            }

            GUILayout.Space(6f);
            NekoUI.Separator();
            GUILayout.Space(4f);

            NekoUI.FieldLabel("Bone overrides - filled from the humanoid rig when possible.");

            leftEyeBoneOverride = EditorGUILayout.TextField("Left Eye Bone", leftEyeBoneOverride);
            rightEyeBoneOverride = EditorGUILayout.TextField("Right Eye Bone", rightEyeBoneOverride);

            var animator = avatarRoot.GetComponent<Animator>();
            if (animator != null && animator.isHuman)
            {
                GUILayout.Space(3f);
                if (NekoUI.GhostButton("Fill From Humanoid Rig", 170f))
                {
                    var left = animator.GetBoneTransform(HumanBodyBones.LeftEye);
                    var right = animator.GetBoneTransform(HumanBodyBones.RightEye);

                    leftEyeBoneOverride = left != null ? left.name : string.Empty;
                    rightEyeBoneOverride = right != null ? right.name : string.Empty;

                    if (left == null || right == null)
                    {
                        EditorUtility.DisplayDialog(
                            "Eye Installer",
                            "This humanoid rig has no eye bones mapped. Blender will try to find them by name instead.",
                            "OK");
                    }
                }
            }

            NekoUI.EndCardBody();
            NekoUI.EndCard();
        }

        private void DrawPlacementSection()
        {
            NekoUI.BeginCard("Placement");
            NekoUI.BeginCardBody();

            bool bones = HasEyeBones();

            if (!bones)
            {
                NekoUI.Notice(
                    "This rig has no eye bones. Place the eyes by hand below - they will be skinned " +
                    "to the head bone, so they follow head movement but will not track or look around.",
                    NekoUI.Warn);
            }

            manualPlacement = NekoUI.Toggle("Manual Placement", manualPlacement || !bones,
                "Use the position and size below instead of measuring from the eye bones.");

            showSceneHandles = NekoUI.Toggle("Show Scene Handles", showSceneHandles,
                "Draw draggable gizmos in the Scene view. Drag the arrows to move an eye and the " +
                "outer circle to resize it.");

            mirrorEyes = NekoUI.Toggle("Mirror Left to Right", mirrorEyes,
                "Moving either eye mirrors it onto the other side across the avatar's centre line.");

            GUILayout.Space(4f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (NekoUI.GhostButton(hasPlacement ? "Reset From Detection" : "Seed From Head", 160f))
                {
                    if (hasPlacement && bones)
                        RunJob(true);
                    else
                        SeedPlacementFromHead();
                }

                GUILayout.Space(5f);

                if (NekoUI.GhostButton("Frame In Scene", 130f))
                {
                    if (!hasPlacement) SeedPlacementFromHead();

                    var view = SceneView.lastActiveSceneView;
                    if (view != null)
                    {
                        view.LookAt((leftEyeCenter + rightEyeCenter) * 0.5f,
                            view.rotation, Mathf.Max(0.2f, leftEyeRadius * 8f));
                    }
                }

                GUILayout.FlexibleSpace();
            }

            if (!hasPlacement)
            {
                NekoUI.FieldLabel(
                    "Run Detect Eyes to measure from the rig, or press Seed From Head to start from a guess.");

                NekoUI.EndCardBody();
                NekoUI.EndCard();
                return;
            }

            GUILayout.Space(4f);
            NekoUI.Separator();
            GUILayout.Space(4f);

            EditorGUI.BeginChangeCheck();

            leftEyeCenter = EditorGUILayout.Vector3Field("Left Centre", leftEyeCenter);
            leftEyeRadius = EditorGUILayout.Slider("Left Size", leftEyeRadius, 0.002f, 0.15f);

            using (new EditorGUI.DisabledScope(mirrorEyes))
            {
                rightEyeCenter = EditorGUILayout.Vector3Field("Right Centre", rightEyeCenter);
                rightEyeRadius = EditorGUILayout.Slider("Right Size", rightEyeRadius, 0.002f, 0.15f);
            }

            if (EditorGUI.EndChangeCheck())
            {
                if (mirrorEyes)
                    MirrorFromLeft();

                manualPlacement = true;
                SceneView.RepaintAll();
            }

            NekoUI.FieldLabel(
                "Size is the eye radius in metres. " +
                (leftEyeRadius * 2f).ToString("0.###") + " m across at the moment.");

            NekoUI.EndCardBody();
            NekoUI.EndCard();
        }

        private void DrawShapeSection()
        {
            NekoUI.BeginCard("Eye Shape");
            NekoUI.BeginCardBody();

            eyeStyle = (EyeStyle)EditorGUILayout.EnumPopup(
                new GUIContent("Style", "Sphere is a full eyeball. Flat is a shallow lens that sits inside the socket."),
                eyeStyle);

            eyeScale = EditorGUILayout.Slider(
                new GUIContent("Size", "Multiplier on the eye radius measured from the original geometry."),
                eyeScale, 0.4f, 1.6f);

            depthOffset = EditorGUILayout.Slider(
                new GUIContent("Depth Offset", "Metres to push the eye forward (+) or back into the head (-)."),
                depthOffset, -0.02f, 0.02f);

            irisScale = EditorGUILayout.Slider(
                new GUIContent("Iris UV Coverage", "How much of the eye front the iris texture covers."),
                irisScale, 0.2f, 1f);

            GUILayout.Space(4f);

            segments = EditorGUILayout.IntSlider("Segments", segments, 8, 48);
            rings = EditorGUILayout.IntSlider("Rings", rings, 6, 32);

            NekoUI.FieldLabel("Roughly " + (segments * rings * 2) + " triangles for both eyes.");

            NekoUI.EndCardBody();
            NekoUI.EndCard();
        }

        private void DrawDetectionSection()
        {
            NekoUI.BeginCard("Detection");
            NekoUI.BeginCardBody();

            removalMode = (RemovalMode)EditorGUILayout.EnumPopup(
                new GUIContent("Remove Old Eyes By", "How to find the original eye geometry to delete."),
                removalMode);

            switch (removalMode)
            {
                case RemovalMode.BoneWeights:
                    NekoUI.FieldLabel("Deletes whatever the eye bones move. Needs eye bones.");

                    weightThreshold = EditorGUILayout.Slider(
                        new GUIContent("Weight Threshold", "Minimum eye-bone weight for a vertex to count."),
                        weightThreshold, 0.01f, 0.5f);

                    if (!HasEyeBones())
                    {
                        NekoUI.Notice(
                            "No eye bones on this rig, so this will fall back to Placement Spheres.",
                            NekoUI.Warn);
                    }
                    break;

                case RemovalMode.PlacementSpheres:
                    NekoUI.FieldLabel(
                        "Deletes everything inside the placement spheres above. Works on any rig - " +
                        "size the spheres to cover the old eyes.");

                    deleteRadiusScale = EditorGUILayout.Slider(
                        new GUIContent("Sphere Scale", "Multiplier on the eye size when deciding what to delete."),
                        deleteRadiusScale, 0.5f, 2f);
                    break;

                case RemovalMode.EyeMaterials:
                    NekoUI.FieldLabel("Deletes faces using eye-looking materials. Good when the eyes have their own slot.");

                    eyeMaterialNames = EditorGUILayout.TextField(
                        new GUIContent("Extra Names", "Semicolon separated, matched loosely. Common eye words are built in."),
                        eyeMaterialNames);

                    DrawFaceMeshMaterialHints();
                    break;

                case RemovalMode.KeepOriginal:
                    NekoUI.FieldLabel("Leaves the original eyes in place and adds the new ones on top. Good for a first look.");
                    break;
            }

            GUILayout.Space(3f);
            removeUnusedSlots = NekoUI.Toggle("Clean Empty Material Slots", removeUnusedSlots,
                "Drop material slots left empty once the old eyes are removed.");

            GUILayout.Space(5f);

            using (new EditorGUI.DisabledScope(!CanRun()))
            {
                if (NekoUI.GhostButton("Detect Eyes  -  no changes made", 0f, 28f))
                    RunJob(true);
            }

            if (!string.IsNullOrEmpty(detectionSummary))
            {
                GUILayout.Space(4f);
                NekoUI.Notice(detectionSummary, NekoUI.AccentA);
            }

            NekoUI.EndCardBody();
            NekoUI.EndCard();
        }

        /// <summary>Lists the face mesh's material slots so you can copy a name into the filter.</summary>
        private void DrawFaceMeshMaterialHints()
        {
            if (faceMesh == null || faceMesh.sharedMaterials == null)
                return;

            var names = faceMesh.sharedMaterials
                .Where(m => m != null)
                .Select(m => m.name)
                .Distinct()
                .ToArray();

            if (names.Length == 0)
                return;

            NekoUI.FieldLabel("Slots on " + faceMesh.name + ":  " + string.Join(",  ", names));
        }

        private void DrawMaterialSection()
        {
            NekoUI.BeginCard("Eye Material");
            NekoUI.BeginCardBody();

            eyeMaterial = (Material)EditorGUILayout.ObjectField(
                new GUIContent("Material", "Optional. Leave empty to generate one from the bundled Neko eye shader."),
                eyeMaterial, typeof(Material), false);

            using (new EditorGUI.DisabledScope(eyeMaterial != null))
            {
                irisTexture = (Texture2D)EditorGUILayout.ObjectField(
                    new GUIContent("Iris Texture", "Used on the generated material. The eye UVs put the iris dead centre."),
                    irisTexture, typeof(Texture2D), false);
            }

            if (eyeMaterial == null && Shader.Find(ShaderName) == null)
            {
                NekoUI.Notice(
                    "The bundled eye shader was not found. Assign your own material, or reimport the package.",
                    NekoUI.Warn);
            }
            else if (eyeMaterial == null)
            {
                NekoUI.FieldLabel("A material will be generated from " + ShaderName + ".");
            }

            NekoUI.EndCardBody();
            NekoUI.EndCard();
        }

        private void DrawActionSection()
        {
            GUILayout.Space(6f);

            if (avatarRoot == null)
                NekoUI.Notice("Select your avatar to continue.", NekoUI.Warn);
            else if (string.IsNullOrEmpty(blenderPath))
                NekoUI.Notice("Blender " + NekoBlenderBootstrap.RequiredVersion + " is required. Download it above.", NekoUI.Warn);
            else if (CanRun())
                NekoUI.Notice("Ready. Your source FBX is read, never written.", NekoUI.Good);

            GUILayout.Space(4f);

            if (NekoUI.PrimaryButton(busy ? "Working..." : "Install Eyes", 42f, CanRun()))
                RunJob(false);

            GUILayout.Space(4f);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(14f);

                using (new EditorGUI.DisabledScope(avatarRoot == null))
                {
                    if (NekoUI.GhostButton("Remove Installed Eyes", 160f, 26f))
                        RemoveInstalledEyes();
                }

                if (!string.IsNullOrEmpty(lastLogPath))
                {
                    GUILayout.Space(5f);
                    if (NekoUI.GhostButton("Open Last Blender Log", 160f, 26f))
                        EditorUtility.RevealInFinder(lastLogPath);
                }

                GUILayout.FlexibleSpace();
                GUILayout.Space(14f);
            }

            GUILayout.Space(6f);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(14f);
                GUILayout.Label(
                    "The edited mesh is written to " + GeneratedRoot +
                    ", and the scene change is a single undo step.",
                    NekoUI.Muted);
                GUILayout.Space(14f);
            }
        }

        private bool CanRun()
        {
            return !busy
                   && avatarRoot != null
                   && !string.IsNullOrEmpty(blenderPath)
                   && !string.IsNullOrEmpty(ResolveSourceFbx());
        }

        // ------------------------------------------------------------------
        // Job driving
        // ------------------------------------------------------------------

        /// <summary>
        /// The FBX behind the chosen face mesh.
        ///
        /// Deliberately not a majority vote over every skinned mesh: on a merged avatar the
        /// outfits outnumber the body, so a vote picks a clothing FBX that has no eye bones at
        /// all. The eyes live on one specific mesh, so that mesh decides which file to open.
        /// </summary>
        private string ResolveSourceFbx()
        {
            if (faceMesh == null || faceMesh.sharedMesh == null)
                return null;

            string path = AssetDatabase.GetAssetPath(faceMesh.sharedMesh);

            if (string.IsNullOrEmpty(path) || !path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                return null;

            // After an install the renderer points at our own output. Re-running on that would
            // delete eyes from an already-edited mesh and stack a second set on top, compounding
            // every time, so the original is remembered on disk and used instead.
            if (path.StartsWith(GeneratedRoot, StringComparison.OrdinalIgnoreCase))
            {
                string remembered = ReadRememberedSource();
                return !string.IsNullOrEmpty(remembered) && File.Exists(remembered) ? remembered : null;
            }

            return path;
        }

        private string SourceRecordPath()
        {
            return GeneratedRoot + "/" + SafeAvatarName() + "/source.txt";
        }

        private string ReadRememberedSource()
        {
            try
            {
                string record = SourceRecordPath();
                if (!File.Exists(record))
                    return null;

                string stored = File.ReadAllText(record).Trim();
                return string.IsNullOrEmpty(stored) ? null : stored;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[NekoEye] Could not read the remembered source FBX: " + ex.Message);
                return null;
            }
        }

        private void RememberSource(string sourceFbx)
        {
            try
            {
                if (string.IsNullOrEmpty(sourceFbx) ||
                    sourceFbx.StartsWith(GeneratedRoot, StringComparison.OrdinalIgnoreCase))
                    return;

                string record = SourceRecordPath();
                Directory.CreateDirectory(Path.GetDirectoryName(
                    Path.Combine(NekoBlenderBootstrap.ProjectRoot, record)));

                File.WriteAllText(Path.Combine(NekoBlenderBootstrap.ProjectRoot, record), sourceFbx);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[NekoEye] Could not record the source FBX: " + ex.Message);
            }
        }

        /// <summary>Mesh object names to hand Blender, so it only searches the face mesh.</summary>
        private List<string> ResolveTargetMeshNames()
        {
            var names = new List<string>();
            if (faceMesh == null) return names;

            // The scene object may have been renamed, so send both it and the mesh asset name.
            if (faceMesh.sharedMesh != null && !string.IsNullOrEmpty(faceMesh.sharedMesh.name))
                names.Add(faceMesh.sharedMesh.name);

            if (!names.Contains(faceMesh.name))
                names.Add(faceMesh.name);

            return names;
        }

        /// <summary>
        /// Picks the mesh the eyes most likely live on, best evidence first.
        /// </summary>
        private void AutoDetectFaceMesh()
        {
            if (avatarRoot == null)
                return;

            var renderers = avatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(r => r.sharedMesh != null)
                .ToArray();

            if (renderers.Length == 0)
                return;

            // 1. A mesh actually skinned to the humanoid eye bones is definitive.
            var animator = avatarRoot.GetComponent<Animator>();
            if (animator != null && animator.isHuman)
            {
                var leftEye = animator.GetBoneTransform(HumanBodyBones.LeftEye);
                if (leftEye != null)
                {
                    var bound = renderers.FirstOrDefault(
                        r => r.bones != null && r.bones.Any(b => b == leftEye));

                    if (bound != null)
                    {
                        faceMesh = bound;
                        return;
                    }
                }
            }

            // 2. Otherwise, a mesh bound to something eye-shaped by name.
            var named = renderers.FirstOrDefault(
                r => r.bones != null && r.bones.Any(LooksLikeEyeBone));

            if (named != null)
            {
                faceMesh = named;
                return;
            }

            // 3. Last resort: the most blendshape-heavy mesh is almost always the face.
            faceMesh = renderers.OrderByDescending(r => r.sharedMesh.blendShapeCount).First();
        }

        private static bool LooksLikeEyeBone(Transform bone)
        {
            if (bone == null) return false;

            string name = bone.name.ToLowerInvariant();
            return name.Contains("eye")
                   || name.Contains("hitomi")
                   || name.Contains("目")   // 目
                   || name.Contains("瞳");  // 瞳
        }

        /// <summary>
        /// Fills the bone overrides from Unity's humanoid mapping.
        ///
        /// Worth doing automatically: Unity already knows which bones are the eyes even when they
        /// are named in Japanese or in some VRM convention Blender's name matching would miss.
        /// </summary>
        private void AutoFillEyeBones()
        {
            if (avatarRoot == null) return;

            var animator = avatarRoot.GetComponent<Animator>();
            if (animator == null || !animator.isHuman) return;

            var left = animator.GetBoneTransform(HumanBodyBones.LeftEye);
            var right = animator.GetBoneTransform(HumanBodyBones.RightEye);

            if (left != null && string.IsNullOrWhiteSpace(leftEyeBoneOverride))
                leftEyeBoneOverride = left.name;

            if (right != null && string.IsNullOrWhiteSpace(rightEyeBoneOverride))
                rightEyeBoneOverride = right.name;
        }

        // ------------------------------------------------------------------
        // Scene view placement
        // ------------------------------------------------------------------

        /// <summary>
        /// Draggable eye placement in the Scene view.
        ///
        /// Needed because the new eyes are skinned: once installed, moving the NekoEyes object
        /// does nothing, since a skinned mesh follows its bones and ignores its own transform.
        /// Placement therefore has to be decided before the mesh is built, which means seeing it
        /// in the viewport first.
        /// </summary>
        private void OnSceneGUI(SceneView view)
        {
            if (!showSceneHandles || !hasPlacement || avatarRoot == null)
                return;

            bool changed = false;

            changed |= DrawEyeHandle(ref leftEyeCenter, ref leftEyeRadius, "L", new Color(0.4f, 0.8f, 1f));
            changed |= DrawEyeHandle(ref rightEyeCenter, ref rightEyeRadius, "R", new Color(1f, 0.55f, 0.75f));

            if (!changed)
                return;

            if (mirrorEyes)
                MirrorFromLeft();

            manualPlacement = true;
            Repaint();
        }

        private bool DrawEyeHandle(ref Vector3 centre, ref float radius, string label, Color color)
        {
            Handles.color = color;

            EditorGUI.BeginChangeCheck();

            Vector3 movedCentre = Handles.PositionHandle(centre, Quaternion.identity);
            float movedRadius = Handles.RadiusHandle(Quaternion.identity, centre, radius);

            Handles.Label(centre + Vector3.up * (radius + 0.015f), label);

            if (!EditorGUI.EndChangeCheck())
                return false;

            centre = movedCentre;
            radius = Mathf.Max(0.001f, movedRadius);
            return true;
        }

        /// <summary>
        /// Mirrors the left eye onto the right across the avatar's own YZ plane, so a symmetric
        /// face only needs one side placed.
        /// </summary>
        private void MirrorFromLeft()
        {
            if (avatarRoot == null) return;

            Vector3 local = avatarRoot.transform.InverseTransformPoint(leftEyeCenter);
            local.x = -local.x;

            rightEyeCenter = avatarRoot.transform.TransformPoint(local);
            rightEyeRadius = leftEyeRadius;
        }

        /// <summary>Seeds placement from whatever the last detect pass measured.</summary>
        private void ApplyPlacementFromReport(EyeReport report)
        {
            if (report == null || report.eyes == null)
                return;

            var anchor = ResolveAnchorBone();
            if (anchor == null || report.anchorPosition == null || report.anchorPosition.Length != 3)
            {
                Debug.LogWarning(
                    "[NekoEye] No shared anchor bone, so the measured placement cannot be mapped into " +
                    "the scene. Place the eyes by hand in the Scene view.");
                return;
            }

            var blenderAnchor = new Vector3(
                report.anchorPosition[0], report.anchorPosition[1], report.anchorPosition[2]);

            float scale = AvatarScale();
            bool any = false;

            if (report.eyes.left != null && report.eyes.left.centre != null && report.eyes.left.centre.Length == 3)
            {
                var centre = new Vector3(
                    report.eyes.left.centre[0], report.eyes.left.centre[1], report.eyes.left.centre[2]);

                leftEyeCenter = BlenderOffsetToWorld(centre - blenderAnchor, anchor);
                leftEyeRadius = Mathf.Max(0.001f, report.eyes.left.radius * scale);
                any = true;
            }

            if (report.eyes.right != null && report.eyes.right.centre != null && report.eyes.right.centre.Length == 3)
            {
                var centre = new Vector3(
                    report.eyes.right.centre[0], report.eyes.right.centre[1], report.eyes.right.centre[2]);

                rightEyeCenter = BlenderOffsetToWorld(centre - blenderAnchor, anchor);
                rightEyeRadius = Mathf.Max(0.001f, report.eyes.right.radius * scale);
                any = true;
            }

            if (!any)
                return;

            hasPlacement = true;
            WarnIfPlacementLooksWrong();
            SceneView.RepaintAll();
        }

        /// <summary>
        /// Sanity-checks the Blender/Unity axis mapping against the rig itself.
        ///
        /// If the converted centre lands nowhere near the actual eye bone, the conversion is
        /// wrong and silently placing eyes in the wrong spot would be worse than saying so.
        /// </summary>
        private void WarnIfPlacementLooksWrong()
        {
            if (avatarRoot == null) return;

            var animator = avatarRoot.GetComponent<Animator>();
            if (animator == null || !animator.isHuman) return;

            var leftEye = animator.GetBoneTransform(HumanBodyBones.LeftEye);
            if (leftEye == null) return;

            float distance = Vector3.Distance(leftEye.position, leftEyeCenter);
            if (distance > 0.15f)
            {
                Debug.LogWarning(string.Format(
                    "[NekoEye] Converted eye placement is {0:0.000} m from the LeftEye bone. " +
                    "The axis conversion may be wrong for this FBX - check the placement in the " +
                    "Scene view before installing.", distance));
            }
        }

        /// <summary>Falls back to a guess in front of the head when there is nothing to measure.</summary>
        private void SeedPlacementFromHead()
        {
            if (avatarRoot == null) return;

            var animator = avatarRoot.GetComponent<Animator>();
            Transform head = animator != null && animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.Head)
                : null;

            if (head == null)
            {
                // No humanoid mapping either: put them at the top of the renderer bounds.
                Bounds bounds = faceMesh != null
                    ? faceMesh.bounds
                    : new Bounds(avatarRoot.transform.position + Vector3.up * 1.4f, Vector3.one * 0.2f);

                Vector3 guess = new Vector3(bounds.center.x, bounds.max.y - bounds.size.y * 0.25f, bounds.center.z);
                leftEyeCenter = guess + avatarRoot.transform.right * -0.035f;
                rightEyeCenter = guess + avatarRoot.transform.right * 0.035f;
            }
            else
            {
                Vector3 eyeLine = head.position + avatarRoot.transform.up * 0.06f + avatarRoot.transform.forward * 0.06f;
                leftEyeCenter = eyeLine + avatarRoot.transform.right * -0.035f;
                rightEyeCenter = eyeLine + avatarRoot.transform.right * 0.035f;
            }

            leftEyeRadius = rightEyeRadius = 0.03f;
            hasPlacement = true;
            manualPlacement = true;
            SceneView.RepaintAll();
        }

        private void OnAvatarChanged()
        {
            faceMesh = null;
            leftEyeBoneOverride = string.Empty;
            rightEyeBoneOverride = string.Empty;
            detectionSummary = string.Empty;
            hasPlacement = false;
            manualPlacement = false;

            AutoDetectFaceMesh();
            AutoFillEyeBones();
            SceneView.RepaintAll();
        }

        /// <summary>True when the rig has no eye bones and placement must be done by hand.</summary>
        private bool HasEyeBones()
        {
            if (avatarRoot == null) return false;

            if (!string.IsNullOrWhiteSpace(leftEyeBoneOverride) &&
                !string.IsNullOrWhiteSpace(rightEyeBoneOverride))
                return true;

            var animator = avatarRoot.GetComponent<Animator>();
            if (animator != null && animator.isHuman &&
                animator.GetBoneTransform(HumanBodyBones.LeftEye) != null)
                return true;

            return faceMesh != null
                   && faceMesh.bones != null
                   && faceMesh.bones.Any(LooksLikeEyeBone);
        }

        private void RunJob(bool detectOnly)
        {
            if (busy) return;
            busy = true;
            Repaint();

            // Blender runs synchronously with a progress bar, so keep it off the IMGUI pass.
            EditorApplication.delayCall += () =>
            {
                try
                {
                    ExecuteJob(detectOnly);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    EditorUtility.DisplayDialog("Neko Eye Installer", ex.Message, "OK");
                }
                finally
                {
                    busy = false;
                    Repaint();
                }
            };
        }

        private void ExecuteJob(bool detectOnly)
        {
            string sourceFbx = ResolveSourceFbx();
            if (string.IsNullOrEmpty(sourceFbx))
                throw new InvalidOperationException("Could not find the avatar's source FBX from its skinned meshes.");

            string executable = NekoBlenderBootstrap.EnsureBlender();
            if (string.IsNullOrEmpty(executable))
                return;

            RememberSource(sourceFbx);

            string scriptPath = FindPythonScript();
            string folder = EnsureGeneratedFolder();

            string outputFbx = Path.Combine(
                NekoBlenderBootstrap.ProjectRoot,
                folder + "/" + SafeAvatarName() + "_NekoEyes.fbx").Replace('\\', '/');

            string reportPath = Path.Combine(
                NekoBlenderBootstrap.ProjectRoot,
                folder + "/" + SafeAvatarName() + "_NekoEyes.report.json").Replace('\\', '/');

            var arguments = new List<string>
            {
                "--input=" + Path.GetFullPath(sourceFbx),
                "--output=" + outputFbx,
                "--report=" + reportPath,
                "--expected-version=" + ScriptVersion,
                "--eye-style=" + (eyeStyle == EyeStyle.Sphere ? "sphere" : "flat"),
                "--segments=" + segments,
                "--rings=" + rings,
                "--scale=" + eyeScale.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "--depth-offset=" + depthOffset.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "--iris-scale=" + irisScale.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "--weight-threshold=" + weightThreshold.ToString(System.Globalization.CultureInfo.InvariantCulture)
            };

            if (!string.IsNullOrWhiteSpace(leftEyeBoneOverride))
                arguments.Add("--left-eye-bone=" + leftEyeBoneOverride.Trim());
            if (!string.IsNullOrWhiteSpace(rightEyeBoneOverride))
                arguments.Add("--right-eye-bone=" + rightEyeBoneOverride.Trim());

            // Restrict the search to the face mesh, so a body and an outfit that share an FBX
            // cannot be confused for one another.
            var targetMeshes = ResolveTargetMeshNames();
            if (targetMeshes.Count > 0)
                arguments.Add("--target-meshes=" + string.Join(";", targetMeshes.ToArray()));

            arguments.Add("--delete-mode=" + RemovalModeArgument(removalMode));
            arguments.Add("--delete-radius-scale=" + Invariant(deleteRadiusScale));

            if (removalMode == RemovalMode.EyeMaterials && !string.IsNullOrWhiteSpace(eyeMaterialNames))
                arguments.Add("--eye-materials=" + eyeMaterialNames.Trim());

            // Placement travels as an offset from a bone both sides can find, so the scene
            // avatar's own transform never leaks into the Blender coordinates.
            var anchor = ResolveAnchorBone();
            if (anchor != null)
                arguments.Add("--anchor-bone=" + anchor.name);

            if (manualPlacement && hasPlacement)
            {
                if (anchor == null)
                {
                    throw new InvalidOperationException(
                        "Manual placement needs a Hips bone (or a root bone on the face mesh) to measure " +
                        "from, and this avatar has neither. Set the rig to Humanoid, or turn Manual " +
                        "Placement off to measure from the eye bones instead.");
                }

                float scale = AvatarScale();

                arguments.Add("--left-eye-center=" + VectorArgument(WorldToBlenderOffset(leftEyeCenter, anchor)));
                arguments.Add("--right-eye-center=" + VectorArgument(WorldToBlenderOffset(rightEyeCenter, anchor)));
                arguments.Add("--left-eye-radius=" + Invariant(leftEyeRadius / scale));
                arguments.Add("--right-eye-radius=" + Invariant(rightEyeRadius / scale));
            }

            if (removeUnusedSlots) arguments.Add("--remove-unused-slots");
            if (detectOnly) arguments.Add("--detect-only");

            var result = NekoBlenderBootstrap.RunPythonJob(
                executable,
                scriptPath,
                arguments,
                detectOnly ? "Detecting Eyes" : "Building Eyes",
                avatarRoot.name);

            lastLogPath = result.LogPath;

            if (result.Cancelled)
            {
                Debug.Log("[NekoEye] Cancelled.");
                return;
            }

            if (!result.Succeeded)
            {
                string message = string.IsNullOrEmpty(result.ScriptError)
                    ? "Blender exited with code " + result.ExitCode + "."
                    : result.ScriptError;

                EditorUtility.DisplayDialog(
                    "Neko Eye Installer",
                    message + "\n\nFull log:\n" + result.LogPath,
                    "OK");
                return;
            }

            detectionSummary = SummariseReport(reportPath);

            if (detectOnly)
            {
                EditorUtility.DisplayDialog("Detection Complete", detectionSummary, "OK");
                return;
            }

            AssetDatabase.Refresh();

            string assetPath = folder + "/" + SafeAvatarName() + "_NekoEyes.fbx";
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            ApplyResult(assetPath);
        }

        private static string Invariant(float value)
        {
            return value.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string VectorArgument(Vector3 v)
        {
            return Invariant(v.x) + "," + Invariant(v.y) + "," + Invariant(v.z);
        }

        private static string RemovalModeArgument(RemovalMode mode)
        {
            switch (mode)
            {
                case RemovalMode.PlacementSpheres: return "sphere";
                case RemovalMode.EyeMaterials: return "material";
                case RemovalMode.KeepOriginal: return "none";
                default: return "bones";
            }
        }

        private string FindPythonScript()
        {
            foreach (string guid in AssetDatabase.FindAssets("neko_eye_tool"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("neko_eye_tool.py", StringComparison.OrdinalIgnoreCase))
                    return Path.GetFullPath(path);
            }

            throw new FileNotFoundException(
                "neko_eye_tool.py was not found in the project. Reimport the Neko Eye Installer package.");
        }

        private string EnsureGeneratedFolder()
        {
            string folder = GeneratedRoot + "/" + SafeAvatarName();

            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }

            return folder;
        }

        private string SafeAvatarName()
        {
            string name = avatarRoot != null ? avatarRoot.name : "Avatar";
            return string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        }

        private string SummariseReport(string reportPath)
        {
            try
            {
                if (!File.Exists(reportPath))
                    return "Blender finished but wrote no report.";

                var report = JsonUtility.FromJson<EyeReport>(File.ReadAllText(reportPath));
                if (report == null)
                    return "Report could not be parsed.";

                // Seed the draggable placement from whatever Blender measured, unless the user
                // has already positioned the eyes by hand.
                if (!manualPlacement)
                    ApplyPlacementFromReport(report);

                if (!report.hasEyeBones)
                {
                    return string.Format(
                        "No eye bones on this rig.\nEyes will be skinned to: {0}\nPlace them by hand in the Scene view.",
                        string.IsNullOrEmpty(report.skinBoneLeft) ? "(none found)" : report.skinBoneLeft);
                }

                return string.Format(
                    "Eye bones: {0} / {1}\nLeft: {2} verts, radius {3:0.0000} m\nRight: {4} verts, radius {5:0.0000} m\nRemoved: {6} vertices",
                    report.leftEyeBone,
                    report.rightEyeBone,
                    report.eyes != null && report.eyes.left != null ? report.eyes.left.vertexCount : 0,
                    report.eyes != null && report.eyes.left != null ? report.eyes.left.radius : 0f,
                    report.eyes != null && report.eyes.right != null ? report.eyes.right.vertexCount : 0,
                    report.eyes != null && report.eyes.right != null ? report.eyes.right.radius : 0f,
                    report.deletedVertices);
            }
            catch (Exception ex)
            {
                return "Report could not be read: " + ex.Message;
            }
        }

        // ------------------------------------------------------------------
        // Applying the Blender output back onto the scene avatar
        // ------------------------------------------------------------------

        private void ApplyResult(string fbxAssetPath)
        {
            var generated = AssetDatabase.LoadAssetAtPath<GameObject>(fbxAssetPath);
            if (generated == null)
                throw new InvalidOperationException("Unity could not import the generated FBX at " + fbxAssetPath);

            var generatedRenderers = generated.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            var eyeSource = generatedRenderers.FirstOrDefault(
                r => r.name.Equals(EyeObjectName, StringComparison.OrdinalIgnoreCase));

            if (eyeSource == null)
                throw new InvalidOperationException(
                    "The generated FBX has no '" + EyeObjectName + "' mesh. Check the Blender log.");

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Install Neko Eyes");

            try
            {
                var material = eyeMaterial != null ? eyeMaterial : CreateEyeMaterial();

                // 1. Swap each edited body mesh onto the matching renderer already on the avatar.
                int swapped = 0;
                foreach (var sourceRenderer in generatedRenderers)
                {
                    if (sourceRenderer == eyeSource)
                        continue;

                    var target = FindRendererByName(avatarRoot.transform, sourceRenderer.name);
                    if (target == null)
                        continue;

                    Undo.RecordObject(target, "Swap Eye-Edited Mesh");
                    RebindRenderer(sourceRenderer, target);
                    swapped++;
                }

                // 2. Drop the new eyes in as their own renderer, so no body shape key is touched.
                var existing = avatarRoot.transform.Find(EyeObjectName);
                if (existing != null)
                    Undo.DestroyObjectImmediate(existing.gameObject);

                var eyeObject = new GameObject(EyeObjectName);
                Undo.RegisterCreatedObjectUndo(eyeObject, "Create Neko Eyes");
                eyeObject.transform.SetParent(avatarRoot.transform, false);

                var eyeRenderer = Undo.AddComponent<SkinnedMeshRenderer>(eyeObject);
                RebindRenderer(eyeSource, eyeRenderer);
                eyeRenderer.sharedMaterials = Enumerable
                    .Repeat(material, Mathf.Max(1, eyeSource.sharedMesh.subMeshCount))
                    .ToArray();

                Selection.activeGameObject = eyeObject;
                EditorUtility.SetDirty(avatarRoot);
                Undo.CollapseUndoOperations(group);

                EditorUtility.DisplayDialog(
                    "Neko Eyes Installed",
                    string.Format(
                        "Done.\n\nNew eye mesh: {0}\nBody meshes updated: {1}\n\n{2}\n\nUndo (Ctrl+Z) reverts the whole install.",
                        EyeObjectName,
                        swapped,
                        detectionSummary),
                    "Nice!");
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                throw;
            }
        }

        /// <summary>
        /// Copies mesh + bone binding from an FBX renderer onto a scene renderer, remapping the
        /// bone array by name. Copying the mesh alone is not enough - the bone order the exporter
        /// produced has to be mapped back onto the avatar's own transforms or the mesh explodes.
        /// </summary>
        private void RebindRenderer(SkinnedMeshRenderer source, SkinnedMeshRenderer target)
        {
            var lookup = BuildTransformLookup(avatarRoot.transform);

            var bones = new Transform[source.bones.Length];
            var missing = new List<string>();

            for (int i = 0; i < source.bones.Length; i++)
            {
                var sourceBone = source.bones[i];
                if (sourceBone == null)
                    continue;

                Transform mapped;
                if (lookup.TryGetValue(sourceBone.name, out mapped))
                    bones[i] = mapped;
                else
                    missing.Add(sourceBone.name);
            }

            if (missing.Count > 0)
            {
                Debug.LogWarning(
                    "[NekoEye] " + missing.Count + " bone(s) from the generated FBX have no match on the avatar: " +
                    string.Join(", ", missing.Take(8).ToArray()) + (missing.Count > 8 ? ", ..." : ""));
            }

            target.sharedMesh = source.sharedMesh;
            target.bones = bones;

            if (source.rootBone != null)
            {
                Transform root;
                target.rootBone = lookup.TryGetValue(source.rootBone.name, out root) ? root : avatarRoot.transform;
            }

            target.updateWhenOffscreen = false;
            target.localBounds = source.localBounds;
            EditorUtility.SetDirty(target);
        }

        private static Dictionary<string, Transform> BuildTransformLookup(Transform root)
        {
            var lookup = new Dictionary<string, Transform>();
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                // First match wins: duplicated bone names are rare and the shallower one is
                // almost always the real skeleton.
                if (!lookup.ContainsKey(transform.name))
                    lookup[transform.name] = transform;
            }
            return lookup;
        }

        private static SkinnedMeshRenderer FindRendererByName(Transform root, string name)
        {
            return root.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .FirstOrDefault(r => r.name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        private Material CreateEyeMaterial()
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null)
                shader = Shader.Find("Standard");

            var material = new Material(shader) { name = SafeAvatarName() + "_NekoEye" };

            if (irisTexture != null && material.HasProperty("_IrisTex"))
                material.SetTexture("_IrisTex", irisTexture);
            else if (irisTexture != null && material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", irisTexture);

            string path = EnsureGeneratedFolder() + "/" + material.name + ".mat";
            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();

            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        private void RemoveInstalledEyes()
        {
            if (avatarRoot == null)
                return;

            var existing = avatarRoot.transform.Find(EyeObjectName);
            if (existing == null)
            {
                EditorUtility.DisplayDialog("Neko Eye Installer", "No installed eyes found on this avatar.", "OK");
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "Remove Neko Eyes",
                "Delete the " + EyeObjectName + " object?\n\n" +
                "Body meshes will stay pointed at the edited version. To restore the original body, " +
                "undo the install, or reassign the meshes from your source FBX.",
                "Remove",
                "Cancel");

            if (!confirmed)
                return;

            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        // ------------------------------------------------------------------
        // Report shape, matching neko_eye_tool.py
        // ------------------------------------------------------------------

        [Serializable]
        private class EyeReport
        {
            public int scriptVersion;
            public string armature;
            public string leftEyeBone;
            public string rightEyeBone;
            public string skinBoneLeft;
            public string skinBoneRight;
            public string headBone;
            public bool hasEyeBones;
            public string deleteMode;
            public int deletedVertices;
            public string anchorBone;
            public float[] anchorPosition;
            public EyePair eyes;
        }

        [Serializable]
        private class EyePair
        {
            public EyeEntry left;
            public EyeEntry right;
        }

        [Serializable]
        private class EyeEntry
        {
            public string bone;
            public string mesh;
            public string skinBone;
            public int vertexCount;
            public float radius;
            public float newRadius;
            public float[] centre;
            public float[] forward;
        }
    }
}
#endif
