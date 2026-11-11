using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NekoSune.LipSync.Editor
{
    internal class NekoBakeReport
    {
        public int CurveCount;
        public int KeyCount;
        public int RawKeyCount;
        public float Duration;
        public string ClipPath;

        public float Reduction
        {
            get { return RawKeyCount > 0 ? 1f - (float)KeyCount / RawKeyCount : 0f; }
        }
    }

    /// <summary>Turns an analysed track into an AnimationClip asset.</summary>
    internal static class NekoAnimClipBuilder
    {
        public static AnimationClip Build(NekoLipSyncTrack track, NekoAvatarBinding bind,
                                          NekoLipSyncSettings s, string clipName, out NekoBakeReport report)
        {
            report = new NekoBakeReport();
            if (track == null || bind == null) return null;

            var clip = new AnimationClip
            {
                name = string.IsNullOrEmpty(clipName) ? "LipSync" : clipName,
                frameRate = track.Fps
            };

            float dt = 1f / track.Fps;
            report.Duration = (track.FrameCount - 1) * dt;

            bool wantVisemes = bind.HasVisemes &&
                               (s.target == NekoLipSyncTarget.Auto || s.target == NekoLipSyncTarget.VrcVisemes);

            bool wantJaw = bind.HasJaw &&
                           (s.target == NekoLipSyncTarget.JawBone ||
                            (s.driveJaw && s.target != NekoLipSyncTarget.SingleMouthOpen) ||
                            (s.target == NekoLipSyncTarget.Auto && !bind.HasVisemes));

            bool wantSingle = bind.HasSingleShape &&
                              (s.target == NekoLipSyncTarget.SingleMouthOpen ||
                               (s.driveSingleShape && s.target != NekoLipSyncTarget.JawBone) ||
                               (s.target == NekoLipSyncTarget.Auto && !bind.HasVisemes && !bind.HasJaw));

            if (wantVisemes) BuildVisemeCurves(clip, track, bind, s, dt, report);
            if (wantJaw)     BuildJawCurves(clip, track, bind, s, dt, report);
            if (wantSingle)  BuildSingleShapeCurve(clip, track, bind, s, dt, report);

            if (report.CurveCount == 0) return null;

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = s.loopTime;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            return clip;
        }

        static void BuildVisemeCurves(AnimationClip clip, NekoLipSyncTrack track, NekoAvatarBinding bind,
                                      NekoLipSyncSettings s, float dt, NekoBakeReport report)
        {
            string path = bind.VisemeRendererPath ?? "";
            var values = new float[track.FrameCount];

            for (int v = 0; v < NekoVisemes.Count; v++)
            {
                if (v == NekoVisemes.Sil && !s.writeSil) continue;
                string shape = bind.ShapeNames[v];
                if (string.IsNullOrEmpty(shape)) continue;

                bool nonZero = false;
                for (int f = 0; f < track.FrameCount; f++)
                {
                    float val = Mathf.Clamp01(track.Weights[f][v]) * 100f;
                    values[f] = val;
                    if (val > 0.01f) nonZero = true;
                }
                // sil is meaningful even when flat; other visemes that never fire are noise.
                if (!nonZero && v != NekoVisemes.Sil) continue;

                AnimationCurve curve = MakeCurve(values, dt, s, report);
                clip.SetCurve(path, typeof(SkinnedMeshRenderer), "blendShape." + shape, curve);
                report.CurveCount++;
            }
        }

        static void BuildJawCurves(AnimationClip clip, NekoLipSyncTrack track, NekoAvatarBinding bind,
                                   NekoLipSyncSettings s, float dt, NekoBakeReport report)
        {
            if (bind.Jaw == null) return;
            string path = bind.JawPath ?? "";
            Vector3 baseEuler = bind.Jaw.localEulerAngles;
            baseEuler = new Vector3(Wrap180(baseEuler.x), Wrap180(baseEuler.y), Wrap180(baseEuler.z));

            float sign = s.jawInvert ? -1f : 1f;
            int axis = (int)s.jawAxis;

            var comp = new float[3][];
            for (int a = 0; a < 3; a++) comp[a] = new float[track.FrameCount];

            for (int f = 0; f < track.FrameCount; f++)
            {
                float open = Mathf.Clamp01(track.Openness[f]);
                comp[0][f] = baseEuler.x;
                comp[1][f] = baseEuler.y;
                comp[2][f] = baseEuler.z;
                comp[axis][f] += sign * s.jawMaxAngle * open;
            }

            string[] props = { "localEulerAnglesRaw.x", "localEulerAnglesRaw.y", "localEulerAnglesRaw.z" };
            for (int a = 0; a < 3; a++)
            {
                AnimationCurve curve = a == axis
                    ? MakeCurve(comp[a], dt, s, report)
                    : ConstantCurve(comp[a][0], report.Duration, report);
                clip.SetCurve(path, typeof(Transform), props[a], curve);
                report.CurveCount++;
            }
        }

        static void BuildSingleShapeCurve(AnimationClip clip, NekoLipSyncTrack track, NekoAvatarBinding bind,
                                          NekoLipSyncSettings s, float dt, NekoBakeReport report)
        {
            var values = new float[track.FrameCount];
            for (int f = 0; f < track.FrameCount; f++)
                values[f] = Mathf.Clamp01(track.Openness[f]) * 100f;

            AnimationCurve curve = MakeCurve(values, dt, s, report);
            clip.SetCurve(bind.SingleRendererPath ?? "", typeof(SkinnedMeshRenderer),
                          "blendShape." + bind.SingleShapeName, curve);
            report.CurveCount++;
        }

        // ------------------------------------------------------------------ curve helpers

        static AnimationCurve ConstantCurve(float value, float duration, NekoBakeReport report)
        {
            var curve = new AnimationCurve(new Keyframe(0f, value, 0f, 0f), new Keyframe(Mathf.Max(duration, 1f / 60f), value, 0f, 0f));
            report.KeyCount += 2;
            report.RawKeyCount += 2;
            return curve;
        }

        static AnimationCurve MakeCurve(float[] values, float dt, NekoLipSyncSettings s, NekoBakeReport report)
        {
            report.RawKeyCount += values.Length;

            List<int> keep = s.keyReduction
                ? ReduceLinear(values, s.keyTolerance)
                : AllIndices(values.Length);

            var keys = new Keyframe[keep.Count];
            for (int i = 0; i < keep.Count; i++)
            {
                int idx = keep[i];
                keys[i] = new Keyframe(idx * dt, values[idx]);
            }

            // Exact piecewise-linear: a Hermite segment whose end tangents both equal the segment
            // slope is a straight line, so no easing creeps into fast consonants.
            for (int i = 0; i < keys.Length; i++)
            {
                float inSlope = 0f, outSlope = 0f;
                if (i > 0)
                {
                    float d = keys[i].time - keys[i - 1].time;
                    if (d > 1e-6f) inSlope = (keys[i].value - keys[i - 1].value) / d;
                }
                if (i < keys.Length - 1)
                {
                    float d = keys[i + 1].time - keys[i].time;
                    if (d > 1e-6f) outSlope = (keys[i + 1].value - keys[i].value) / d;
                }
                if (i == 0) inSlope = outSlope;
                if (i == keys.Length - 1) outSlope = inSlope;
                keys[i].inTangent = inSlope;
                keys[i].outTangent = outSlope;
            }

            report.KeyCount += keys.Length;
            return new AnimationCurve(keys);
        }

        static List<int> AllIndices(int n)
        {
            var l = new List<int>(n);
            for (int i = 0; i < n; i++) l.Add(i);
            return l;
        }

        /// <summary>
        /// Greedy linear simplification: keep a sample only when dropping it would push the
        /// straight-line approximation further than <paramref name="tolerance"/> off the original.
        /// </summary>
        static List<int> ReduceLinear(float[] values, float tolerance)
        {
            int n = values.Length;
            var keep = new List<int>(Mathf.Max(2, n / 4)) { 0 };
            if (n <= 2)
            {
                if (n == 2) keep.Add(1);
                return keep;
            }

            int anchor = 0;
            for (int i = 2; i < n; i++)
            {
                float v0 = values[anchor];
                float v1 = values[i];
                float span = i - anchor;
                bool ok = true;
                for (int j = anchor + 1; j < i; j++)
                {
                    float lerped = v0 + (v1 - v0) * ((j - anchor) / span);
                    if (Mathf.Abs(values[j] - lerped) > tolerance) { ok = false; break; }
                }
                if (!ok)
                {
                    keep.Add(i - 1);
                    anchor = i - 1;
                }
            }

            if (keep[keep.Count - 1] != n - 1) keep.Add(n - 1);
            return keep;
        }

        static float Wrap180(float deg)
        {
            deg %= 360f;
            if (deg > 180f) deg -= 360f;
            if (deg < -180f) deg += 360f;
            return deg;
        }

        // ------------------------------------------------------------------ saving

        public static string Save(AnimationClip clip, string folder, string fileName)
        {
            if (clip == null) return null;
            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
                folder = EnsureFolder("Assets/NekoSune/LipSync");

            string safe = Sanitize(fileName);
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + safe + ".anim");
            AssetDatabase.CreateAsset(clip, path);
            AssetDatabase.SaveAssets();
            return path;
        }

        public static string EnsureFolder(string assetFolder)
        {
            if (string.IsNullOrEmpty(assetFolder)) return "Assets";
            if (AssetDatabase.IsValidFolder(assetFolder)) return assetFolder;

            string[] parts = assetFolder.Split('/');
            string current = parts[0];
            if (current != "Assets") return "Assets";
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
            return current;
        }

        public static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name)) return "LipSync";
            char[] invalid = System.IO.Path.GetInvalidFileNameChars();
            var sb = new System.Text.StringBuilder(name.Length);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                bool bad = false;
                for (int j = 0; j < invalid.Length; j++) if (invalid[j] == c) { bad = true; break; }
                sb.Append(bad ? '_' : c);
            }
            string result = sb.ToString().Trim();
            return string.IsNullOrEmpty(result) ? "LipSync" : result;
        }
    }
}
