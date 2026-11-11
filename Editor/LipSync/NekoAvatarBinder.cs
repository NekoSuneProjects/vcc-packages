using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace NekoSune.LipSync.Editor
{
    internal enum NekoBindSource
    {
        None,
        VrcDescriptor,
        AutoDetected,
        Manual
    }

    internal class NekoAvatarBinding
    {
        public GameObject Root;

        // Viseme blendshapes
        public SkinnedMeshRenderer VisemeRenderer;
        public string VisemeRendererPath;
        public string[] ShapeNames = new string[NekoVisemes.Count];
        public NekoBindSource VisemeSource = NekoBindSource.None;

        // Jaw bone
        public Transform Jaw;
        public string JawPath;

        // Single "mouth open" shape
        public SkinnedMeshRenderer SingleRenderer;
        public string SingleRendererPath;
        public string SingleShapeName;

        public readonly List<string> Notes = new List<string>();

        public int MappedVisemeCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < ShapeNames.Length; i++)
                    if (!string.IsNullOrEmpty(ShapeNames[i])) n++;
                return n;
            }
        }

        public bool HasVisemes { get { return VisemeRenderer != null && MappedVisemeCount > 0; } }
        public bool HasJaw { get { return Jaw != null; } }
        public bool HasSingleShape { get { return SingleRenderer != null && !string.IsNullOrEmpty(SingleShapeName); } }
        public bool CanBake { get { return HasVisemes || HasJaw || HasSingleShape; } }
    }

    /// <summary>
    /// Works out where a lip-sync animation should write on an arbitrary avatar.
    ///
    /// First choice is whatever the VRChat avatar descriptor already declares — that is the
    /// mapping the avatar author tested in-game. When there is no descriptor (or no SDK in the
    /// project at all) the blendshapes are matched by name across the naming conventions used by
    /// Booth, Gumroad, VRoid, CATS and ARKit avatars. The VRC SDK is reached purely through
    /// reflection, so this package compiles and runs in projects without it.
    /// </summary>
    internal static class NekoAvatarBinder
    {
        public static NekoAvatarBinding Bind(GameObject avatar)
        {
            var b = new NekoAvatarBinding { Root = avatar };
            if (avatar == null) return b;

            if (TryBindFromDescriptor(avatar, b))
                b.VisemeSource = NekoBindSource.VrcDescriptor;

            if (!b.HasVisemes && TryAutoDetectVisemes(avatar, b))
                b.VisemeSource = NekoBindSource.AutoDetected;

            if (b.Jaw == null) b.Jaw = FindJaw(avatar);
            if (b.Jaw != null) b.JawPath = AnimationUtility.CalculateTransformPath(b.Jaw, avatar.transform);

            if (!b.HasSingleShape) TryFindSingleShape(avatar, b);

            if (!b.CanBake)
                b.Notes.Add(NekoLoc.T("bind.noneFound"));

            return b;
        }

        // ---------------------------------------------------------------- VRC descriptor

        static bool TryBindFromDescriptor(GameObject avatar, NekoAvatarBinding b)
        {
            Component descriptor = FindDescriptor(avatar);
            if (descriptor == null) return false;

            Type t = descriptor.GetType();

            var renderer = GetMember(descriptor, t, "VisemeSkinnedMesh") as SkinnedMeshRenderer;
            var shapes = GetMember(descriptor, t, "VisemeBlendShapes") as string[];
            var jaw = GetMember(descriptor, t, "lipSyncJawBone") as Transform;
            var mouthOpen = GetMember(descriptor, t, "MouthOpenBlendShapeName") as string;
            object lipSyncMode = GetMember(descriptor, t, "lipSync");
            string mode = lipSyncMode != null ? lipSyncMode.ToString() : "";

            bool bound = false;

            if (renderer != null && shapes != null && shapes.Length > 0)
            {
                var mesh = renderer.sharedMesh;
                int copied = 0;
                for (int i = 0; i < NekoVisemes.Count && i < shapes.Length; i++)
                {
                    string name = shapes[i];
                    if (string.IsNullOrEmpty(name) || name == "-none-") continue;
                    if (mesh != null && mesh.GetBlendShapeIndex(name) < 0) continue;
                    b.ShapeNames[i] = name;
                    copied++;
                }

                if (copied > 0)
                {
                    b.VisemeRenderer = renderer;
                    b.VisemeRendererPath = AnimationUtility.CalculateTransformPath(renderer.transform, avatar.transform);
                    b.Notes.Add(NekoLoc.T("bind.fromDescriptor", copied));
                    bound = true;
                }
            }

            if (jaw != null)
            {
                b.Jaw = jaw;
                b.Notes.Add(NekoLoc.T("bind.jawFromDescriptor", jaw.name));
            }

            if (!string.IsNullOrEmpty(mouthOpen) && mouthOpen != "-none-")
            {
                SkinnedMeshRenderer target = renderer;
                if (target == null || target.sharedMesh == null || target.sharedMesh.GetBlendShapeIndex(mouthOpen) < 0)
                    target = FindRendererWithShape(avatar, mouthOpen);

                if (target != null)
                {
                    b.SingleRenderer = target;
                    b.SingleRendererPath = AnimationUtility.CalculateTransformPath(target.transform, avatar.transform);
                    b.SingleShapeName = mouthOpen;
                }
            }

            if (!string.IsNullOrEmpty(mode) && mode.IndexOf("Viseme", StringComparison.OrdinalIgnoreCase) < 0 && bound == false)
                b.Notes.Add(NekoLoc.T("bind.descriptorMode", mode));

            return bound;
        }

        static Component FindDescriptor(GameObject avatar)
        {
            Component[] comps = avatar.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < comps.Length; i++)
            {
                Component c = comps[i];
                if (c == null) continue;
                string n = c.GetType().Name;
                if (n == "VRCAvatarDescriptor" || n == "VRC_AvatarDescriptor") return c;
            }
            return null;
        }

        static object GetMember(object instance, Type t, string name)
        {
            FieldInfo f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            if (f != null) return f.GetValue(instance);
            PropertyInfo p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            if (p != null && p.CanRead) return p.GetValue(instance, null);
            return null;
        }

        // ---------------------------------------------------------------- name matching

        static readonly string[] KnownPrefixes =
        {
            "vrcv", "vrc", "visemesil", "viseme", "vis", "fclmth", "fcl", "mth", "mouth",
            "lipsync", "lip", "ls", "v", "blendshape", "bs"
        };

        // Per-viseme accepted suffix tokens, checked after prefix stripping.
        static readonly string[][] Tokens =
        {
            new[] { "sil", "silence", "neutral", "rest", "closed", "n" },              // sil
            new[] { "pp", "p", "mbp", "bm", "m", "b" },                                // PP
            new[] { "ff", "f", "fv" },                                                 // FF
            new[] { "th", "t" },                                                       // TH
            new[] { "dd", "d" },                                                       // DD
            new[] { "kk", "k", "g" },                                                  // kk
            new[] { "ch", "sh", "j" },                                                 // CH
            new[] { "ss", "s", "z" },                                                  // SS
            new[] { "nn", "l" },                                                       // nn
            new[] { "rr", "r" },                                                       // RR
            new[] { "aa", "a", "ah" },                                                 // aa
            new[] { "e", "eh", "ee" },                                                 // E
            new[] { "ih", "i", "iy" },                                                 // ih
            new[] { "oh", "o" },                                                       // oh
            new[] { "ou", "u", "uw", "oo" },                                           // ou
        };

        // Japanese kana used by VRoid / MMD-derived avatars.
        static readonly Dictionary<string, int> Kana = new Dictionary<string, int>
        {
            { "あ", NekoVisemes.AA }, // a
            { "い", NekoVisemes.IH }, // i
            { "う", NekoVisemes.OU }, // u
            { "え", NekoVisemes.E  }, // e
            { "お", NekoVisemes.OH }, // o
        };

        static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new System.Text.StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        static int ClassifyShapeName(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return -1;

            foreach (var kv in Kana)
                if (rawName.IndexOf(kv.Key, StringComparison.Ordinal) >= 0) return kv.Value;

            string n = Normalize(rawName);
            if (n.Length == 0) return -1;

            // Strip the longest known prefix, if any.
            string body = n;
            int bestLen = 0;
            for (int i = 0; i < KnownPrefixes.Length; i++)
            {
                string p = KnownPrefixes[i];
                if (n.Length > p.Length && n.StartsWith(p, StringComparison.Ordinal) && p.Length > bestLen)
                    bestLen = p.Length;
            }
            if (bestLen > 0) body = n.Substring(bestLen);

            for (int v = 0; v < Tokens.Length; v++)
            {
                string[] toks = Tokens[v];
                for (int i = 0; i < toks.Length; i++)
                    if (body == toks[i]) return v;
            }

            // Fall back to the un-stripped name for shapes literally called "aa", "ou", ...
            if (bestLen > 0)
            {
                for (int v = 0; v < Tokens.Length; v++)
                {
                    string[] toks = Tokens[v];
                    for (int i = 0; i < toks.Length; i++)
                        if (n == toks[i]) return v;
                }
            }

            return -1;
        }

        static bool TryAutoDetectVisemes(GameObject avatar, NekoAvatarBinding b)
        {
            SkinnedMeshRenderer[] renderers = avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            SkinnedMeshRenderer best = null;
            string[] bestMap = null;
            int bestScore = 0;

            for (int r = 0; r < renderers.Length; r++)
            {
                SkinnedMeshRenderer smr = renderers[r];
                if (smr == null || smr.sharedMesh == null) continue;
                Mesh mesh = smr.sharedMesh;
                int count = mesh.blendShapeCount;
                if (count == 0) continue;

                var map = new string[NekoVisemes.Count];
                int score = 0;
                for (int i = 0; i < count; i++)
                {
                    string shape = mesh.GetBlendShapeName(i);
                    int v = ClassifyShapeName(shape);
                    if (v < 0) continue;
                    // Prefer the first match; explicit "vrc." names win over bare letters.
                    bool preferNew = string.IsNullOrEmpty(map[v]) ||
                                     (shape.IndexOf("vrc", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                      map[v].IndexOf("vrc", StringComparison.OrdinalIgnoreCase) < 0);
                    if (!preferNew) continue;
                    if (string.IsNullOrEmpty(map[v])) score++;
                    map[v] = shape;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMap = map;
                    best = smr;
                }
            }

            // Require at least a few vowels before claiming a match.
            if (best == null || bestScore < 3) return false;

            b.VisemeRenderer = best;
            b.VisemeRendererPath = AnimationUtility.CalculateTransformPath(best.transform, avatar.transform);
            b.ShapeNames = bestMap;
            b.Notes.Add(NekoLoc.T("bind.autoDetected", bestScore, best.name));
            return true;
        }

        static readonly string[] SingleShapeCandidates =
        {
            "vrc.v_aa", "vrc.v.aa", "viseme_aa", "jawOpen", "JawOpen", "mouthOpen", "MouthOpen",
            "Fcl_MTH_A", "MTH_A", "mouth_open", "Mouth_Open", "A", "aa"
        };

        static void TryFindSingleShape(GameObject avatar, NekoAvatarBinding b)
        {
            // Reuse whatever the viseme mesh already offers before searching the whole avatar.
            if (b.VisemeRenderer != null && !string.IsNullOrEmpty(b.ShapeNames[NekoVisemes.AA]))
            {
                b.SingleRenderer = b.VisemeRenderer;
                b.SingleRendererPath = b.VisemeRendererPath;
                b.SingleShapeName = b.ShapeNames[NekoVisemes.AA];
                return;
            }

            for (int c = 0; c < SingleShapeCandidates.Length; c++)
            {
                SkinnedMeshRenderer smr = FindRendererWithShape(avatar, SingleShapeCandidates[c]);
                if (smr == null) continue;
                b.SingleRenderer = smr;
                b.SingleRendererPath = AnimationUtility.CalculateTransformPath(smr.transform, avatar.transform);
                b.SingleShapeName = SingleShapeCandidates[c];
                return;
            }
        }

        public static SkinnedMeshRenderer FindRendererWithShape(GameObject avatar, string shapeName)
        {
            if (avatar == null || string.IsNullOrEmpty(shapeName)) return null;
            SkinnedMeshRenderer[] renderers = avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                SkinnedMeshRenderer smr = renderers[i];
                if (smr == null || smr.sharedMesh == null) continue;
                if (smr.sharedMesh.GetBlendShapeIndex(shapeName) >= 0) return smr;
            }
            return null;
        }

        static Transform FindJaw(GameObject avatar)
        {
            var animator = avatar.GetComponent<Animator>();
            if (animator != null && animator.isHuman)
            {
                Transform jaw = animator.GetBoneTransform(HumanBodyBones.Jaw);
                if (jaw != null) return jaw;
            }

            Transform[] all = avatar.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                string n = all[i].name.ToLowerInvariant();
                if (n == "jaw" || n.EndsWith("_jaw") || n.EndsWith(".jaw") || n.Contains("jaw"))
                    return all[i];
            }
            return null;
        }

        /// <summary>All blendshape names on the avatar, formatted as "RendererName / shapeName".</summary>
        public static List<KeyValuePair<SkinnedMeshRenderer, string>> AllBlendShapes(GameObject avatar)
        {
            var list = new List<KeyValuePair<SkinnedMeshRenderer, string>>();
            if (avatar == null) return list;
            SkinnedMeshRenderer[] renderers = avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                SkinnedMeshRenderer smr = renderers[r];
                if (smr == null || smr.sharedMesh == null) continue;
                Mesh m = smr.sharedMesh;
                for (int i = 0; i < m.blendShapeCount; i++)
                    list.Add(new KeyValuePair<SkinnedMeshRenderer, string>(smr, m.GetBlendShapeName(i)));
            }
            return list;
        }
    }
}
