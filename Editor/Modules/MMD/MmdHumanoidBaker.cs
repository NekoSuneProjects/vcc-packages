using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static MotionConverter.Mmd.MmdSkeleton;

namespace MotionConverter.Mmd
{
    /// <summary>
    /// VMD body/face motion → Humanoid muscle clip for a specific avatar.
    /// Every bone is rebuilt from absolute MMD world rotations, rest poses are aligned (source A-pose vs avatar),
    /// foot IK is solved with a two-bone solver, then the pose is read back as Humanoid muscles each frame.
    /// </summary>
    internal static class MmdHumanoidBaker
    {
        private sealed class BoneRig
        {
            public HumanBodyBones bone; public Transform t; public string mmdName;
            public Quaternion alignedRest; // root-local: (rest alignment) * (avatar rest rotation)
            public int depth;
        }

        private sealed class LegRig
        {
            public Transform upper, knee, foot;
            public Quaternion restUpperWorld, restKneeWorld;
            public Vector3 restAnkleLocal;
            public float l1, l2;
            public string ikName; public Track ikTrack;
            public BoneRig footRig;
        }

        /// <summary>Metres per MMD unit, from the avatar's leg length. Also used by the camera so both stay in proportion.</summary>
        public static float BodyScale(Animator a, MmdSettings s)
        {
            if (a == null) return s.fallbackUnitsPerMmd;
            Transform up = a.GetBoneTransform(HumanBodyBones.LeftUpperLeg), kn = a.GetBoneTransform(HumanBodyBones.LeftLowerLeg), ft = a.GetBoneTransform(HumanBodyBones.LeftFoot);
            if (up == null || kn == null || ft == null) return s.fallbackUnitsPerMmd;
            Transform r = a.transform;
            Vector3 pu = r.InverseTransformPoint(up.position), pk = r.InverseTransformPoint(kn.position), pf = r.InverseTransformPoint(ft.position);
            float len = Vector3.Distance(pu, pk) + Vector3.Distance(pk, pf);
            return len > 1e-4f ? len / Mathf.Max(0.1f, s.sourceLegLength) : s.fallbackUnitsPerMmd;
        }

        /// <summary>Returns an unsaved clip. Throws on invalid input or cancel.</summary>
        public static AnimationClip Bake(GameObject avatar, VmdFile vmd, MmdSettings s, ConversionLog log)
        {
            if (avatar == null) throw new InvalidOperationException(L.T("Choose a target Humanoid avatar first."));
            var sourceAnimator = avatar.GetComponentInChildren<Animator>();
            if (sourceAnimator == null || sourceAnimator.avatar == null || !sourceAnimator.avatar.isHuman || !sourceAnimator.avatar.isValid)
                throw new InvalidOperationException(L.T("The target must have an Animator with a VALID Humanoid Avatar."));
            if (vmd.bones.Count == 0) throw new InvalidDataException(L.T("This VMD contains no bone motion. Select a body motion VMD."));

            // Work on a hidden clone so the user's avatar and prefab are never touched.
            GameObject clone = null;
            try
            {
                clone = UnityEngine.Object.Instantiate(sourceAnimator.gameObject);
                clone.name = "__NekoSune_MMD_HumanoidBake_Temporary__";
                clone.hideFlags = HideFlags.HideAndDontSave;
                var animator = clone.GetComponent<Animator>();
                if (animator == null) throw new InvalidOperationException(L.T("Failed to create a Humanoid avatar bake clone."));
                animator.enabled = false;
                Transform root = animator.transform;
                Quaternion rootRot = root.rotation;
                Quaternion rootRotInv = Quaternion.Inverse(rootRot);
                float scale = BodyScale(animator, s);

                var tracks = new Dictionary<string, Track>();
                foreach (var g in vmd.bones.GroupBy(b => b.name)) tracks[g.Key] = new Track { frames = g.OrderBy(f => f.frame).ToList() };
                tracks.TryGetValue("全ての親", out Track allParent);
                tracks.TryGetValue("センター", out Track centreTrack);
                tracks.TryGetValue("グルーブ", out Track grooveTrack);

                var align = RestAlignment(animator, rootRotInv, s.armAPoseAngle);
                Func<HumanBodyBones, Transform> T = hb => animator.GetBoneTransform(hb);

                // ---- bone rigs (every mapped humanoid bone the avatar actually has) ------
                var rigs = new List<BoneRig>();
                foreach (var kv in NameForBone)
                {
                    if (IsFinger(kv.Key) && !s.includeFingers) continue;
                    var tr = T(kv.Key); if (tr == null) continue;
                    Quaternion c = Quaternion.identity;
                    if (IsFinger(kv.Key)) { if (align.TryGetValue(IsLeft(kv.Key) ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand, out Quaternion hc)) c = hc; }
                    else if (align.TryGetValue(kv.Key, out Quaternion ac)) c = ac;
                    rigs.Add(new BoneRig { bone = kv.Key, t = tr, mmdName = kv.Value, alignedRest = c * (rootRotInv * tr.rotation), depth = Depth(tr, root) });
                }
                rigs = rigs.OrderBy(r => r.depth).ToList();
                if (rigs.Count == 0) throw new InvalidOperationException(L.T("No compatible Humanoid bones were found on the avatar."));

                // ---- legs ----------------------------------------------------------------
                LegRig leftLeg = s.solveFootIk ? MakeLeg(animator, tracks, rigs, true) : null;
                LegRig rightLeg = s.solveFootIk ? MakeLeg(animator, tracks, rigs, false) : null;
                if (s.solveFootIk && (leftLeg == null || rightLeg == null))
                    log.Warn(L.T("Foot IK keys or humanoid leg bones missing for at least one leg. That leg uses FK only."));

                Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                Vector3 hipsRestLocal = hips != null ? root.InverseTransformPoint(hips.position) : Vector3.zero;
                Vector3 startOffset = Vector3.zero;
                if (s.hipsTranslation && s.startAtOrigin) { Vector3 c0 = RootChainPos(allParent, centreTrack, grooveTrack, 0f); startOffset = new Vector3(c0.x, 0f, c0.z); }

                // Capture the complete bind pose; every frame starts from it (no accumulation).
                var allTransforms = clone.GetComponentsInChildren<Transform>(true);
                var bindRotations = allTransforms.Select(t => t.localRotation).ToArray();
                var bindPositions = allTransforms.Select(t => t.localPosition).ToArray();

                using (var handler = new HumanPoseHandler(animator.avatar, animator.transform))
                {
                    var pose = new HumanPose { muscles = new float[HumanTrait.MuscleCount] };
                    handler.GetHumanPose(ref pose);
                    int muscleCount = HumanTrait.MuscleCount;
                    var muscles = new List<Keyframe>[muscleCount];
                    for (int m = 0; m < muscleCount; m++) muscles[m] = new List<Keyframe>();
                    var rootT = new List<Keyframe>[3]; var rootQ = new List<Keyframe>[4];
                    for (int i = 0; i < 3; i++) rootT[i] = new List<Keyframe>();
                    for (int i = 0; i < 4; i++) rootQ[i] = new List<Keyframe>();

                    log.Info(L.T("Scale {0:0.0000} m/unit (avatar legs {1} m / source {2:0.0} units); arm A-pose {3:0}°; bones {4}; foot IK L={5} R={6}.",
                        scale, leftLeg != null ? (leftLeg.l1 + leftLeg.l2).ToString("0.000") : "?", s.sourceLegLength, s.armAPoseAngle, rigs.Count, leftLeg != null, rightLeg != null));

                    uint maxFrame = vmd.bones.Max(f => f.frame);
                    if (vmd.morphs.Count > 0) maxFrame = Math.Max(maxFrame, vmd.morphs.Max(f => f.frame));
                    var cache = new Dictionary<string, Quaternion>();
                    Quaternion previousRoot = Quaternion.identity;
                    for (int frame = 0; frame <= (int)maxFrame; frame++)
                    {
                        for (int i = 0; i < allTransforms.Length; i++)
                        {
                            allTransforms[i].localRotation = bindRotations[i];
                            allTransforms[i].localPosition = bindPositions[i];
                        }
                        cache.Clear();

                        // 1. FK: absolute MMD world rotation -> avatar world rotation, parents first.
                        foreach (var rig in rigs)
                            rig.t.rotation = rootRot * (World(rig.mmdName, tracks, cache, frame) * rig.alignedRest);

                        // 2. Hips translation (centre + groove), same space/scale as foot targets.
                        Vector3 chain = RootChainPos(allParent, centreTrack, grooveTrack, frame);
                        if (s.hipsTranslation && hips != null)
                            hips.position = root.TransformPoint(hipsRestLocal + ConvertPos(chain - startOffset, scale));

                        // 3. Foot IK: ankle target = avatar rest ankle + scaled MMD IK offset.
                        foreach (var leg in new[] { leftLeg, rightLeg })
                        {
                            if (leg == null) continue;
                            Sample(leg.ikTrack, frame, out Vector3 ikPos, out _);
                            Sample(allParent, frame, out Vector3 allPos, out Quaternion allRotRaw);
                            Vector3 shift = allPos + allRotRaw * ikPos;
                            shift -= s.hipsTranslation ? startOffset : new Vector3(chain.x, 0f, chain.z);
                            Vector3 localTarget = leg.restAnkleLocal + ConvertPos(shift, scale);
                            if (s.keepFeetAboveFloor) localTarget.y = Mathf.Max(localTarget.y, leg.restAnkleLocal.y);
                            SolveLeg(root, leg, root.TransformPoint(localTarget), s.footIkStrength, s.maxFootReach);
                            // Foot orientation follows the IK bone (MMD's toe-IK does this in the original).
                            if (leg.footRig != null)
                                leg.foot.rotation = rootRot * (World(leg.ikName, tracks, cache, frame) * leg.footRig.alignedRest);
                        }

                        handler.GetHumanPose(ref pose);
                        float time = frame / MmdSettings.Fps;
                        for (int m = 0; m < muscleCount; m++)
                        {
                            float muscleValue = pose.muscles[m];
                            if (float.IsNaN(muscleValue) || float.IsInfinity(muscleValue)) muscleValue = 0f;
                            muscles[m].Add(new Keyframe(time, Mathf.Clamp(muscleValue, -1f, 1f)));
                        }
                        var tp = pose.bodyPosition;
                        rootT[0].Add(new Keyframe(time, tp.x)); rootT[1].Add(new Keyframe(time, tp.y)); rootT[2].Add(new Keyframe(time, tp.z));
                        var q = pose.bodyRotation;
                        if (frame > 0 && Quaternion.Dot(previousRoot, q) < 0) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                        rootQ[0].Add(new Keyframe(time, q.x)); rootQ[1].Add(new Keyframe(time, q.y));
                        rootQ[2].Add(new Keyframe(time, q.z)); rootQ[3].Add(new Keyframe(time, q.w));
                        previousRoot = q;
                        if (frame % 200 == 0 && EditorUtility.DisplayCancelableProgressBar(L.T("Baking MMD Humanoid"), L.T("Sampling pose frame {0} / {1}", frame, maxFrame), (float)frame / Mathf.Max(1, maxFrame)))
                            throw new OperationCanceledException(L.T("MMD conversion cancelled."));
                    }

                    var clip = new AnimationClip { name = "MMD_Body_Humanoid", frameRate = MmdSettings.Fps, legacy = false };
                    for (int m = 0; m < muscleCount; m++)
                        MmdCurves.SetHumanoidCurve(clip, MmdCurves.MuscleBindingName(HumanTrait.MuscleName[m]), muscles[m]);
                    for (int i = 0; i < 3; i++) MmdCurves.SetHumanoidCurve(clip, "RootT." + "xyz"[i], rootT[i]);
                    for (int i = 0; i < 4; i++) MmdCurves.SetHumanoidCurve(clip, "RootQ." + "xyzw"[i], rootQ[i]);

                    int morphMatches = s.includeExpressions ? AddMorphs(clip, avatar, sourceAnimator.transform, vmd) : 0;
                    log.Info(L.T("Body: {0} bones, {1} face blendshape bindings, {2} frames at {3:0} fps.", rigs.Count, morphMatches, maxFrame + 1, MmdSettings.Fps));
                    if (!clip.humanMotion)
                        log.Warn(L.T("Unity did not recognise the generated clip as Humanoid. Inspect the clip before use."));
                    return clip;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (clone != null) UnityEngine.Object.DestroyImmediate(clone);
            }
        }

        // ---- rest alignment (source A-pose vs avatar rest pose) -----------------------------
        private static Dictionary<HumanBodyBones, Quaternion> RestAlignment(Animator animator, Quaternion rootRotInv, float armAPoseAngle)
        {
            float a = armAPoseAngle * Mathf.Deg2Rad;
            Vector3 leftArmSrc = new Vector3(-Mathf.Cos(a), -Mathf.Sin(a), 0f);   // avatar-left is -X in Unity
            Vector3 rightArmSrc = new Vector3(Mathf.Cos(a), -Mathf.Sin(a), 0f);
            Func<HumanBodyBones, Transform> T = hb => animator.GetBoneTransform(hb);
            Func<Transform, Transform, Vector3> dirRL = (from, to) => (from == null || to == null) ? Vector3.zero : rootRotInv * (to.position - from.position);
            var align = new Dictionary<HumanBodyBones, Quaternion>();
            foreach (bool left in new[] { true, false })
            {
                string side = left ? "Left" : "Right";
                Func<string, HumanBodyBones> B = n => (HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), side + n);
                Vector3 armSrc = left ? leftArmSrc : rightArmSrc;
                align[B("UpperArm")] = Align(dirRL(T(B("UpperArm")), T(B("LowerArm"))), armSrc, 100f);
                align[B("LowerArm")] = Align(dirRL(T(B("LowerArm")), T(B("Hand"))), armSrc, 100f);
                Transform midProx = T(B("MiddleProximal"));
                Vector3 handDir = midProx != null ? dirRL(T(B("Hand")), midProx) : dirRL(T(B("LowerArm")), T(B("Hand")));
                align[B("Hand")] = Align(handDir, armSrc, 100f);
                align[B("UpperLeg")] = Align(dirRL(T(B("UpperLeg")), T(B("LowerLeg"))), Vector3.down, 45f);
                align[B("LowerLeg")] = Align(dirRL(T(B("LowerLeg")), T(B("Foot"))), Vector3.down, 45f);
            }
            Func<HumanBodyBones[], Transform> firstExisting = list => { foreach (var hb in list) { var t = T(hb); if (t != null) return t; } return null; };
            align[HumanBodyBones.Spine] = Align(dirRL(T(HumanBodyBones.Spine), firstExisting(new[] { HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Neck, HumanBodyBones.Head })), Vector3.up, 45f);
            align[HumanBodyBones.Chest] = Align(dirRL(T(HumanBodyBones.Chest), firstExisting(new[] { HumanBodyBones.UpperChest, HumanBodyBones.Neck, HumanBodyBones.Head })), Vector3.up, 45f);
            align[HumanBodyBones.Neck] = Align(dirRL(T(HumanBodyBones.Neck), T(HumanBodyBones.Head)), Vector3.up, 45f);
            return align;
        }

        // Rotation that turns the AVATAR's rest bone direction onto the SOURCE model's rest direction
        // (both in avatar-root space). Identity when the directions already agree or are degenerate.
        private static Quaternion Align(Vector3 avatarDir, Vector3 sourceDir, float maxAngle)
        {
            if (avatarDir.sqrMagnitude < 1e-10f || sourceDir.sqrMagnitude < 1e-10f) return Quaternion.identity;
            if (Vector3.Angle(avatarDir, sourceDir) > maxAngle) return Quaternion.identity;
            return Quaternion.FromToRotation(avatarDir.normalized, sourceDir.normalized);
        }

        // ---- face ------------------------------------------------------------------------
        // Face blendshapes are safe generic curves, since they are not humanoid bones.
        private static int AddMorphs(AnimationClip clip, GameObject avatar, Transform avatarRoot, VmdFile vmd)
        {
            int matches = 0;
            var renderers = avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var group in vmd.morphs.GroupBy(m => m.name))
            {
                if (!MorphAliases.TryGetValue(group.Key, out string[] aliases)) aliases = new[] { group.Key };
                foreach (var renderer in renderers)
                {
                    if (renderer.sharedMesh == null) continue;
                    string path = MmdCurves.PathFor(renderer.transform, avatarRoot);
                    if (path == null) continue;
                    for (int i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
                    {
                        var shape = renderer.sharedMesh.GetBlendShapeName(i);
                        if (!aliases.Any(al => string.Equals(al, shape, StringComparison.OrdinalIgnoreCase))) continue;
                        MmdCurves.SetCurve(clip, path, typeof(SkinnedMeshRenderer), "blendShape." + shape,
                            group.Select(f => new Keyframe(f.frame / MmdSettings.Fps, f.weight * 100f)).ToList());
                        matches++;
                    }
                }
            }
            return matches;
        }

        // ---- legs ------------------------------------------------------------------------

        // Two-bone leg solve in WORLD space. The pole (knee direction) comes from the FK thigh
        // orientation (hips twist + thigh keys), carried by the shortest swing onto the hip->ankle line;
        // then the thigh is twisted so its forward axis matches the pole. That keeps the knee a pure
        // hinge, so Mecanim's single knee DoF reproduces it instead of clamping. No frame-to-frame state.
        private static void SolveLeg(Transform root, LegRig leg, Vector3 targetWorld, float weight, float extLimit)
        {
            Vector3 hip = leg.upper.position;
            targetWorld = Vector3.Lerp(leg.foot.position, targetWorld, Mathf.Clamp01(weight));
            Vector3 off = targetWorld - hip; float dist = off.magnitude;
            if (dist < 1e-5f) return;
            Vector3 d = off / dist;
            float total = leg.l1 + leg.l2;
            float maxD = total * Mathf.Clamp(extLimit, 0.85f, 0.999f);
            float minD = Mathf.Abs(leg.l1 - leg.l2) + total * 0.02f;
            float D = Mathf.Clamp(dist, minD, maxD);

            Vector3 thighDir = leg.knee.position - hip;
            if (thighDir.sqrMagnitude < 1e-10f) return;
            thighDir.Normalize();
            Quaternion thighDelta = leg.upper.rotation * Quaternion.Inverse(leg.restUpperWorld);
            Vector3 fwdFk = thighDelta * root.forward;
            Quaternion swing = Quaternion.FromToRotation(thighDir, d);
            Vector3 pole = Vector3.ProjectOnPlane(swing * fwdFk, d);
            if (pole.sqrMagnitude < 1e-6f) pole = Vector3.ProjectOnPlane(swing * (thighDelta * root.up), d);
            if (pole.sqrMagnitude < 1e-6f) pole = Vector3.ProjectOnPlane(root.forward, d);
            if (pole.sqrMagnitude < 1e-6f) pole = Vector3.ProjectOnPlane(root.right, d);
            if (pole.sqrMagnitude < 1e-6f) return;
            pole.Normalize();

            float along = (leg.l1 * leg.l1 - leg.l2 * leg.l2 + D * D) / (2f * D);
            float h = Mathf.Sqrt(Mathf.Max(0f, leg.l1 * leg.l1 - along * along));
            Vector3 kneeGoal = hip + d * along + pole * h;
            Vector3 ankleGoal = hip + d * D;

            Vector3 kneeDir = (kneeGoal - hip).normalized;
            leg.upper.rotation = Quaternion.FromToRotation(thighDir, kneeDir) * leg.upper.rotation;
            Vector3 fwdNow = Vector3.ProjectOnPlane((leg.upper.rotation * Quaternion.Inverse(leg.restUpperWorld)) * root.forward, kneeDir);
            if (fwdNow.sqrMagnitude > 1e-6f)
                leg.upper.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(fwdNow, pole, kneeDir), kneeDir) * leg.upper.rotation;

            // Shin: keep its rest orientation relative to the thigh (no twist at the knee), then flex.
            leg.knee.rotation = leg.upper.rotation * Quaternion.Inverse(leg.restUpperWorld) * leg.restKneeWorld;
            Vector3 shinNow = leg.foot.position - leg.knee.position;
            Vector3 shinGoal = ankleGoal - leg.knee.position;
            if (shinNow.sqrMagnitude > 1e-10f && shinGoal.sqrMagnitude > 1e-10f)
                leg.knee.rotation = Quaternion.FromToRotation(shinNow, shinGoal) * leg.knee.rotation;
        }

        private static LegRig MakeLeg(Animator animator, Dictionary<string, Track> tracks, List<BoneRig> rigs, bool left)
        {
            Transform root = animator.transform;
            string s = left ? "Left" : "Right";
            var up = animator.GetBoneTransform((HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), s + "UpperLeg"));
            var kn = animator.GetBoneTransform((HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), s + "LowerLeg"));
            var ft = animator.GetBoneTransform((HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), s + "Foot"));
            if (up == null || kn == null || ft == null) return null;
            string ikName = left ? "左足ＩＫ" : "右足ＩＫ";
            tracks.TryGetValue(ikName, out Track ikt);
            if (ikt == null) return null;
            var leg = new LegRig
            {
                upper = up, knee = kn, foot = ft,
                restUpperWorld = up.rotation, restKneeWorld = kn.rotation,
                restAnkleLocal = root.InverseTransformPoint(ft.position),
                l1 = Vector3.Distance(up.position, kn.position), l2 = Vector3.Distance(kn.position, ft.position),
                ikName = ikName, ikTrack = ikt,
                footRig = rigs.FirstOrDefault(r => r.t == ft)
            };
            return leg.l1 > 1e-4f && leg.l2 > 1e-4f ? leg : null;
        }

        // ---- misc ------------------------------------------------------------------------
        private static int Depth(Transform t, Transform root) { int d = 0; while (t != null && t != root) { d++; t = t.parent; } return d; }

        private static bool IsFinger(HumanBodyBones b)
        {
            string n = b.ToString();
            return n.Contains("Thumb") || n.Contains("Index") || n.Contains("Middle") || n.Contains("Ring") || n.Contains("Little");
        }

        private static bool IsLeft(HumanBodyBones b) => b.ToString().StartsWith("Left");
    }
}
