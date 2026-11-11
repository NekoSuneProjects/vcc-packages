using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MotionConverter.Mmd
{
    /// <summary>AnimationClip curve writing helpers for the MMD bakers.</summary>
    internal static class MmdCurves
    {
        public static void SetCurve(AnimationClip clip, string path, Type type, string property, List<Keyframe> keys)
        {
            if (keys.Count == 0) return;
            var curve = new AnimationCurve(keys.OrderBy(x => x.time).GroupBy(x => x.time).Select(g => g.Last()).ToArray());
            for (int i = 0; i < curve.length; i++) AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            for (int i = 0; i < curve.length; i++) AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, type, property), curve);
        }

        public static void SetHumanoidCurve(AnimationClip clip, string property, List<Keyframe> keys) =>
            SetCurve(clip, "", typeof(Animator), property, keys);

        public static void AddQuat(AnimationClip clip, string path, IEnumerable<KeyValuePair<float, Quaternion>> frames)
        {
            var a = frames.OrderBy(k => k.Key).ToList(); if (a.Count == 0) return;
            // Keep neighbouring quaternions on the same hemisphere.
            for (int i = 1; i < a.Count; ++i)
                if (Quaternion.Dot(a[i - 1].Value, a[i].Value) < 0f)
                { var q = a[i].Value; a[i] = new KeyValuePair<float, Quaternion>(a[i].Key, new Quaternion(-q.x, -q.y, -q.z, -q.w)); }
            SetCurve(clip, path, typeof(Transform), "m_LocalRotation.x", a.Select(f => new Keyframe(f.Key, f.Value.x)).ToList());
            SetCurve(clip, path, typeof(Transform), "m_LocalRotation.y", a.Select(f => new Keyframe(f.Key, f.Value.y)).ToList());
            SetCurve(clip, path, typeof(Transform), "m_LocalRotation.z", a.Select(f => new Keyframe(f.Key, f.Value.z)).ToList());
            SetCurve(clip, path, typeof(Transform), "m_LocalRotation.w", a.Select(f => new Keyframe(f.Key, f.Value.w)).ToList());
        }

        public static void AddPos(AnimationClip clip, string path, IEnumerable<KeyValuePair<float, Vector3>> frames)
        {
            var a = frames.OrderBy(k => k.Key).ToList();
            SetCurve(clip, path, typeof(Transform), "m_LocalPosition.x", a.Select(f => new Keyframe(f.Key, f.Value.x)).ToList());
            SetCurve(clip, path, typeof(Transform), "m_LocalPosition.y", a.Select(f => new Keyframe(f.Key, f.Value.y)).ToList());
            SetCurve(clip, path, typeof(Transform), "m_LocalPosition.z", a.Select(f => new Keyframe(f.Key, f.Value.z)).ToList());
        }

        /// <summary>Unity uses different curve binding names for finger muscles than HumanTrait.MuscleName.</summary>
        public static string MuscleBindingName(string name)
        {
            foreach (string side in new[] { "Left", "Right" })
            foreach (string finger in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
            {
                string prefix = side + " " + finger + " ";
                if (name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    string suffix = name.Substring(prefix.Length);
                    if (suffix == "Spread") return side + "Hand." + finger + " Spread";
                    if (suffix.Length >= 1 && suffix[0] >= '1' && suffix[0] <= '3')
                        return side + "Hand." + finger + "." + suffix;
                }
            }
            return name;
        }

        /// <summary>Transform path of `child` relative to `root`, or null if it isn't under it.</summary>
        public static string PathFor(Transform child, Transform root)
        {
            if (child == root) return "";
            var parts = new List<string>(); var p = child;
            while (p != null && p != root) { parts.Add(p.name); p = p.parent; }
            if (p != root) return null;
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
