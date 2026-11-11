using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace MotionConverter
{
    /// <summary>
    /// Builds a humanoid Avatar for the (T-posed) source rig, then plays the source clip on it
    /// frame by frame and records the humanoid muscle values into a new clip.
    /// </summary>
    public static class HumanoidBaker
    {
        public static Avatar BuildAvatar(SourceRig rig, Dictionary<HumanBodyBones, Transform> human)
        {
            var description = new HumanDescription
            {
                human = human.Select(kv => new HumanBone
                {
                    humanName = HumanTrait.BoneName[(int)kv.Key],
                    boneName = kv.Value.name,
                    limit = new HumanLimit { useDefaultValues = true }
                }).ToArray(),

                skeleton = rig.Root.GetComponentsInChildren<Transform>(true).Select(t => new SkeletonBone
                {
                    name = t.name,
                    position = t.localPosition,
                    rotation = t.localRotation,
                    scale = t.localScale
                }).ToArray(),

                upperArmTwist = 0.5f,
                lowerArmTwist = 0.5f,
                upperLegTwist = 0.5f,
                lowerLegTwist = 0.5f,
                armStretch = 0.05f,
                legStretch = 0.05f,
                feetSpacing = 0f,
                hasTranslationDoF = false
            };

            var avatar = AvatarBuilder.BuildHumanAvatar(rig.Root.gameObject, description);
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
                throw new Exception(L.T("Unity rejected the generated humanoid avatar (see the Console for details)."));
            avatar.hideFlags = HideFlags.HideAndDontSave;
            return avatar;
        }

        public static AnimationClip Bake(SourceRig rig, Avatar avatar, ConversionSettings settings, ConversionLog log)
        {
            float fps = settings.FrameRate;
            float outLength = settings.OutputLength;
            int frames = Mathf.Max(2, Mathf.RoundToInt(outLength * fps) + 1);
            int muscleCount = HumanTrait.MuscleCount;

            var times = new float[frames];
            var rootT = new Vector3[frames];
            var rootQ = new Quaternion[frames];
            var muscles = new float[muscleCount][];
            for (int m = 0; m < muscleCount; m++) muscles[m] = new float[frames];

            using (var handler = new HumanPoseHandler(avatar, rig.Root))
            {
                var pose = new HumanPose();
                for (int f = 0; f < frames; f++)
                {
                    if (f % 30 == 0)
                        EditorUtility.DisplayProgressBar("Motion Converter", L.T("Sampling frame {0}/{1}", f, frames), (float)f / frames);

                    times[f] = Mathf.Min(f / fps, outLength);
                    rig.Sample(settings.ToSourceTime(times[f]));
                    handler.GetHumanPose(ref pose);

                    rootT[f] = pose.bodyPosition;
                    var q = pose.bodyRotation;
                    // Keep quaternion signs continuous or interpolation takes the long way round.
                    if (f > 0 && Quaternion.Dot(q, rootQ[f - 1]) < 0) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                    rootQ[f] = q;
                    for (int m = 0; m < muscleCount; m++) muscles[m][f] = pose.muscles[m];
                }
            }

            var clip = new AnimationClip { frameRate = fps };
            var bindings = new List<EditorCurveBinding>();
            var curves = new List<AnimationCurve>();
            void Add(string property, Func<int, float> value)
            {
                bindings.Add(EditorCurveBinding.FloatCurve("", typeof(Animator), property));
                curves.Add(BuildCurve(times, value));
            }

            Add("RootT.x", i => rootT[i].x);
            Add("RootT.y", i => rootT[i].y);
            Add("RootT.z", i => rootT[i].z);
            Add("RootQ.x", i => rootQ[i].x);
            Add("RootQ.y", i => rootQ[i].y);
            Add("RootQ.z", i => rootQ[i].z);
            Add("RootQ.w", i => rootQ[i].w);

            for (int m = 0; m < muscleCount; m++)
            {
                string muscle = HumanTrait.MuscleName[m];
                if (!settings.includeFingers && FingerMuscle.IsMatch(muscle)) continue;
                var values = muscles[m];
                Add(ToClipProperty(muscle), i => values[i]);
            }

            AnimationUtility.SetEditorCurves(clip, bindings.ToArray(), curves.ToArray());
            ApplyClipSettings(clip, settings);

            log.Info(L.T("Sampled {0} frames @ {1} fps → {2:0.##}s (speed x{3:0.###}, trim {4:0.##}s, delay {5:0.##}s), {6} curves.",
                     frames, fps, outLength, settings.speed, settings.trimStart, settings.delayStart, bindings.Count));
            WarnOnBrokenMuscles(muscles, log);
            return clip;
        }

        private static void ApplyClipSettings(AnimationClip clip, ConversionSettings settings)
        {
            var s = AnimationUtility.GetAnimationClipSettings(clip);
            s.loopTime = settings.loop;
            s.loopBlendOrientation = settings.bakeRootMotion;
            s.loopBlendPositionY = settings.bakeRootMotion;
            s.loopBlendPositionXZ = settings.bakeRootMotion;
            s.keepOriginalOrientation = true;
            s.keepOriginalPositionY = true;
            s.keepOriginalPositionXZ = true;
            AnimationUtility.SetAnimationClipSettings(clip, s);
        }

        // Healthy retargets stay roughly within -1..1; big values mean the T-pose or mapping is off.
        private static void WarnOnBrokenMuscles(float[][] muscles, ConversionLog log)
        {
            var worst = Enumerable.Range(0, muscles.Length)
                .Select(m => (name: HumanTrait.MuscleName[m], max: muscles[m].Max(v => Mathf.Abs(v))))
                .Where(x => x.max > 1.5f)
                .OrderByDescending(x => x.max)
                .Take(5)
                .ToList();
            if (worst.Count > 0)
                log.Warn(L.T("Muscles far out of range, the pose may look broken: {0}",
                         string.Join(", ", worst.Select(x => $"{x.name} {x.max:0.##}"))));
        }

        /// <summary>Keys at every sample with Catmull-Rom tangents: smooth, and far faster than per-key tangent modes.</summary>
        private static AnimationCurve BuildCurve(float[] times, Func<int, float> value)
        {
            int n = times.Length;
            var keys = new Keyframe[n];
            for (int i = 0; i < n; i++) keys[i] = new Keyframe(times[i], value(i));
            for (int i = 0; i < n; i++)
            {
                int a = Mathf.Max(0, i - 1), b = Mathf.Min(n - 1, i + 1);
                float dt = keys[b].time - keys[a].time;
                float slope = dt > 1e-6f ? (keys[b].value - keys[a].value) / dt : 0f;
                keys[i].inTangent = slope;
                keys[i].outTangent = slope;
            }
            return new AnimationCurve(keys);
        }

        private static readonly Regex FingerMuscle =
            new Regex(@"^(Left|Right) (Thumb|Index|Middle|Ring|Little) (1 Stretched|2 Stretched|3 Stretched|Spread)$");

        // HumanTrait finger names differ from clip property names:
        // "Left Index 1 Stretched" → "LeftHand.Index.1 Stretched", "Left Index Spread" → "LeftHand.Index.Spread".
        private static string ToClipProperty(string muscle)
        {
            var m = FingerMuscle.Match(muscle);
            return m.Success ? $"{m.Groups[1].Value}Hand.{m.Groups[2].Value}.{m.Groups[3].Value}" : muscle;
        }
    }
}
