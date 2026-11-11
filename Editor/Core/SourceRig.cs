using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MotionConverter
{
    /// <summary>
    /// A throwaway skeleton rebuilt from a Generic clip's transform bindings, so the clip can be
    /// played back without the original model being in the project.
    ///
    /// Hierarchy:  Root (pose origin) → Upright (orientation/scale fix) → clip's top-level objects...
    /// The Upright node is never animated, and HumanPoseHandler measures relative to Root, so the
    /// fix has to live in between.
    /// </summary>
    public sealed class SourceRig : IDisposable
    {
        public Transform Root { get; }
        public Transform Upright { get; }
        public IReadOnlyList<TrackedBone> Bones => bones;
        public int CurveCount { get; }

        private readonly List<TrackedBone> bones;
        private readonly Dictionary<string, Transform> byName = new Dictionary<string, Transform>();

        public SourceRig(AnimationClip clip)
        {
            var bindings = AnimationUtility.GetCurveBindings(clip)
                .Where(b => b.type == typeof(Transform) && !string.IsNullOrEmpty(b.path))
                .ToArray();
            if (bindings.Length == 0)
                throw new Exception(L.T("The clip has no Transform curves. Is it already a Humanoid clip?"));
            CurveCount = bindings.Length;

            Root = NewNode("MotionConverter_Root", null);
            Upright = NewNode("MotionConverter_Upright", Root);

            var nodes = new Dictionary<string, Transform> { [""] = Upright };
            var tracked = new Dictionary<string, TrackedBone>();
            foreach (var binding in bindings)
            {
                if (!tracked.TryGetValue(binding.path, out var bone))
                    tracked[binding.path] = bone = new TrackedBone(GetOrCreate(binding.path, nodes));
                bone.Assign(binding.propertyName, AnimationUtility.GetEditorCurve(clip, binding));
            }

            // Parents before children.
            bones = tracked.Values.OrderBy(b => Depth(b.Transform)).ToList();
        }

        /// <summary>First transform with this exact name, or null.</summary>
        public Transform Find(string name) => byName.TryGetValue(name, out var t) ? t : null;

        public void Sample(float time)
        {
            foreach (var bone in bones) bone.Apply(time);
        }

        public void Dispose()
        {
            if (Root != null) UnityEngine.Object.DestroyImmediate(Root.gameObject);
        }

        private Transform GetOrCreate(string path, Dictionary<string, Transform> nodes)
        {
            if (nodes.TryGetValue(path, out var existing)) return existing;

            int slash = path.LastIndexOf('/');
            var parent = GetOrCreate(slash < 0 ? "" : path.Substring(0, slash), nodes);
            string name = slash < 0 ? path : path.Substring(slash + 1);

            // Avatar skeletons need unique names. Duplicates are never human bones, so suffix them.
            string unique = name;
            for (int i = 1; byName.ContainsKey(unique); i++) unique = $"{name}__{i}";

            var node = NewNode(unique, parent);
            byName[unique] = node;
            nodes[path] = node;
            return node;
        }

        private static Transform NewNode(string name, Transform parent)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            if (parent != null) go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static int Depth(Transform t)
        {
            int depth = 0;
            for (; t.parent != null; t = t.parent) depth++;
            return depth;
        }
    }

    /// <summary>One transform plus whichever of its local position/rotation/scale curves exist.</summary>
    public sealed class TrackedBone
    {
        public Transform Transform { get; }

        private AnimationCurve px, py, pz;
        private AnimationCurve qx, qy, qz, qw;
        private AnimationCurve ex, ey, ez;
        private AnimationCurve sx, sy, sz;

        public TrackedBone(Transform transform) => Transform = transform;

        public void Assign(string property, AnimationCurve curve)
        {
            switch (property)
            {
                case "m_LocalPosition.x": px = curve; break;
                case "m_LocalPosition.y": py = curve; break;
                case "m_LocalPosition.z": pz = curve; break;
                case "m_LocalRotation.x": qx = curve; break;
                case "m_LocalRotation.y": qy = curve; break;
                case "m_LocalRotation.z": qz = curve; break;
                case "m_LocalRotation.w": qw = curve; break;
                case "localEulerAngles.x": case "localEulerAnglesRaw.x": case "localEulerAnglesBaked.x": ex = curve; break;
                case "localEulerAngles.y": case "localEulerAnglesRaw.y": case "localEulerAnglesBaked.y": ey = curve; break;
                case "localEulerAngles.z": case "localEulerAnglesRaw.z": case "localEulerAnglesBaked.z": ez = curve; break;
                case "m_LocalScale.x": sx = curve; break;
                case "m_LocalScale.y": sy = curve; break;
                case "m_LocalScale.z": sz = curve; break;
            }
        }

        public void Apply(float time)
        {
            var t = Transform;

            if (px != null || py != null || pz != null)
            {
                var p = t.localPosition;
                t.localPosition = new Vector3(Eval(px, time, p.x), Eval(py, time, p.y), Eval(pz, time, p.z));
            }

            // Prefer the quaternion curves; the euler ones are only an editor view of the same data.
            if (qx != null && qy != null && qz != null && qw != null)
            {
                var q = new Quaternion(qx.Evaluate(time), qy.Evaluate(time), qz.Evaluate(time), qw.Evaluate(time));
                float mag = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
                if (mag > 1e-6f) t.localRotation = new Quaternion(q.x / mag, q.y / mag, q.z / mag, q.w / mag);
            }
            else if (ex != null || ey != null || ez != null)
            {
                var e = t.localEulerAngles;
                t.localRotation = Quaternion.Euler(Eval(ex, time, e.x), Eval(ey, time, e.y), Eval(ez, time, e.z));
            }

            if (sx != null || sy != null || sz != null)
            {
                var s = t.localScale;
                t.localScale = new Vector3(Eval(sx, time, s.x), Eval(sy, time, s.y), Eval(sz, time, s.z));
            }
        }

        private static float Eval(AnimationCurve curve, float time, float fallback) =>
            curve != null ? curve.Evaluate(time) : fallback;
    }
}
