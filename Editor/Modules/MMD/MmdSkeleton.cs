using System;
using System.Collections.Generic;
using UnityEngine;

namespace MotionConverter.Mmd
{
    /// <summary>Standard MMD bone structure, coordinate conversion and VMD sampling (with Bezier interpolation).</summary>
    internal static class MmdSkeleton
    {
        // ---- Coordinate conversion -------------------------------------------------------
        // MMD and Unity are BOTH left-handed, Y-up. MMD models face -Z, Unity avatars face +Z,
        // so the conversion is a pure 180 degree rotation about Y: (x,y,z) -> (-x,y,-z).
        // Quaternions conjugate the same way: (x,y,z,w) -> (-x,y,-z,w). A Z-only flip is a MIRROR
        // (it swaps left/right and inverts arm lifts), which is what broke the old versions.
        public static Vector3 ConvertPos(Vector3 p, float scale) => new Vector3(-p.x, p.y, -p.z) * scale;
        public static Quaternion ConvertRot(Quaternion q) => new Quaternion(-q.x, q.y, -q.z, q.w);

        // Humanoid bone -> MMD bone whose WORLD rotation drives it. Toes/eyes are intentionally absent
        // (MMD toe is a tip bone; eyes are handled as blendshapes).
        public static readonly Dictionary<string, HumanBodyBones> BodyMap = new Dictionary<string, HumanBodyBones>
        {
            {"下半身",HumanBodyBones.Hips}, {"上半身",HumanBodyBones.Spine}, {"上半身2",HumanBodyBones.Chest}, {"首",HumanBodyBones.Neck}, {"頭",HumanBodyBones.Head},
            {"左肩",HumanBodyBones.LeftShoulder}, {"左腕",HumanBodyBones.LeftUpperArm}, {"左ひじ",HumanBodyBones.LeftLowerArm}, {"左手首",HumanBodyBones.LeftHand},
            {"右肩",HumanBodyBones.RightShoulder}, {"右腕",HumanBodyBones.RightUpperArm}, {"右ひじ",HumanBodyBones.RightLowerArm}, {"右手首",HumanBodyBones.RightHand},
            {"左足",HumanBodyBones.LeftUpperLeg}, {"左ひざ",HumanBodyBones.LeftLowerLeg}, {"左足首",HumanBodyBones.LeftFoot},
            {"右足",HumanBodyBones.RightUpperLeg}, {"右ひざ",HumanBodyBones.RightLowerLeg}, {"右足首",HumanBodyBones.RightFoot}
        };

        public static readonly Dictionary<string, string[]> MorphAliases = new Dictionary<string, string[]>
        {
            {"あ",new[]{"あ","A","a","vrc.v_aa","Mouth_A"}}, {"い",new[]{"い","I","i","vrc.v_ih","Mouth_I"}},
            {"う",new[]{"う","U","u","vrc.v_ou","Mouth_U"}}, {"え",new[]{"え","E","e","vrc.v_e","Mouth_E"}},
            {"お",new[]{"お","O","o","vrc.v_oh","Mouth_O"}}, {"まばたき",new[]{"まばたき","Blink","blink","EyeBlink","Eye_Close"}},
            {"笑い",new[]{"笑い","Smile","smile","Happy"}}, {"にやり",new[]{"にやり","Smirk","smirk"}}
        };

        public static HumanBodyBones FingerBone(string name)
        {
            if (name.Length < 4) return HumanBodyBones.LastBone;
            bool left = name.StartsWith("左"), right = name.StartsWith("右"); if (!left && !right) return HumanBodyBones.LastBone;
            string side = left ? "Left" : "Right";
            string finger = name.Contains("親指") ? "Thumb" : name.Contains("人指") ? "Index" : name.Contains("中指") ? "Middle" : name.Contains("薬指") ? "Ring" : name.Contains("小指") ? "Little" : null;
            if (finger == null) return HumanBodyBones.LastBone;
            // MMD thumb starts at 0; other fingers start at 1.
            char n = name[name.Length - 1]; int idx = (n >= '０' && n <= '９') ? n - '０' : (n >= '0' && n <= '9' ? n - '0' : -1);
            if (idx < 0) return HumanBodyBones.LastBone;
            int segment = finger == "Thumb" ? idx + 1 : idx;
            if (segment < 1 || segment > 3) return HumanBodyBones.LastBone;
            return Enum.TryParse(side + finger + (segment == 1 ? "Proximal" : segment == 2 ? "Intermediate" : "Distal"), out HumanBodyBones b) ? b : HumanBodyBones.LastBone;
        }

        // Parent of each MMD bone. World rotation of a bone = parent world * local (the PMX rest
        // orientation of standard bones is identity in model space, so no rest term is needed).
        // Bones with no keys are identity, so twist/helper bones that a model lacks cost nothing.
        public static readonly Dictionary<string, string> Parent = BuildParents();
        public static readonly Dictionary<HumanBodyBones, string> NameForBone = BuildNames();

        private static Dictionary<string, string> BuildParents()
        {
            var d = new Dictionary<string, string>
            {
                {"センター","全ての親"}, {"グルーブ","センター"}, {"腰","グルーブ"},
                {"下半身","腰"}, {"上半身","腰"}, {"上半身2","上半身"}, {"首","上半身2"}, {"頭","首"}
            };
            foreach (string s in new[] { "左", "右" })
            {
                d[s + "足"] = "下半身"; d[s + "ひざ"] = s + "足"; d[s + "足首"] = s + "ひざ";
                d[s + "足ＩＫ"] = "全ての親";
                d[s + "肩P"] = "上半身2"; d[s + "肩"] = s + "肩P";
                d[s + "腕"] = s + "肩"; d[s + "腕捩"] = s + "腕"; d[s + "ひじ"] = s + "腕捩";
                d[s + "手捩"] = s + "ひじ"; d[s + "手首"] = s + "手捩";
                string[] digits = { "０", "１", "２", "３" };
                foreach (string f in new[] { "親指", "人指", "中指", "薬指", "小指" })
                {
                    int first = f == "親指" ? 0 : 1; string prev = s + "手首";
                    for (int i = first; i < first + 3; i++) { string nm = s + f + digits[i]; d[nm] = prev; prev = nm; }
                }
            }
            return d;
        }

        private static Dictionary<HumanBodyBones, string> BuildNames()
        {
            var d = new Dictionary<HumanBodyBones, string>();
            foreach (var kv in BodyMap) d[kv.Value] = kv.Key;
            string[] digits = { "０", "１", "２", "３" };
            foreach (string s in new[] { "左", "右" })
                foreach (string f in new[] { "親指", "人指", "中指", "薬指", "小指" })
                    for (int i = 0; i < 4; i++)
                    {
                        string nm = s + f + digits[i]; var hb = FingerBone(nm);
                        if (hb != HumanBodyBones.LastBone) d[hb] = nm;
                    }
            return d;
        }

        // ---- VMD sampling ------------------------------------------------------------------
        public sealed class Track { public List<VmdBoneFrame> frames; public int cursor; }

        // Channel: 0=X 1=Y 2=Z 3=Rotation. Control points live at c, c+4, c+8, c+12 (x1,y1,x2,y2).
        private static float Bezier(byte[] ip, int c, float t)
        {
            if (ip == null || ip.Length < 16) return t;
            float x1 = ip[c] / 127f, y1 = ip[c + 4] / 127f, x2 = ip[c + 8] / 127f, y2 = ip[c + 12] / 127f;
            if (Mathf.Approximately(x1, y1) && Mathf.Approximately(x2, y2)) return t;
            float lo = 0f, hi = 1f, s = t;
            for (int i = 0; i < 18; i++)
            {
                s = (lo + hi) * 0.5f;
                float inv = 1f - s;
                float x = 3f * inv * inv * s * x1 + 3f * inv * s * s * x2 + s * s * s;
                if (x < t) lo = s; else hi = s;
            }
            float iv = 1f - s;
            return 3f * iv * iv * s * y1 + 3f * iv * s * s * y2 + s * s * s;
        }

        public static void Sample(Track t, float frame, out Vector3 pos, out Quaternion rot)
        {
            pos = Vector3.zero; rot = Quaternion.identity;
            if (t == null || t.frames.Count == 0) return;
            var fr = t.frames;
            if (fr.Count == 1 || frame <= fr[0].frame) { pos = fr[0].pos; rot = fr[0].rot; return; }
            var last = fr[fr.Count - 1];
            if (frame >= last.frame) { pos = last.pos; rot = last.rot; return; }
            if (t.cursor < 0 || t.cursor >= fr.Count - 1 || fr[t.cursor].frame > frame) t.cursor = 0;
            while (t.cursor + 1 < fr.Count - 1 && fr[t.cursor + 1].frame <= frame) t.cursor++;
            var a = fr[t.cursor]; var b = fr[t.cursor + 1];
            float span = (float)b.frame - a.frame;
            float u = span > 0f ? Mathf.Clamp01((frame - a.frame) / span) : 1f;
            pos = new Vector3(
                Mathf.Lerp(a.pos.x, b.pos.x, Bezier(b.interp, 0, u)),
                Mathf.Lerp(a.pos.y, b.pos.y, Bezier(b.interp, 1, u)),
                Mathf.Lerp(a.pos.z, b.pos.z, Bezier(b.interp, 2, u)));
            rot = Quaternion.Slerp(a.rot, b.rot, Bezier(b.interp, 3, u));
        }

        /// <summary>Absolute (Unity-converted) world rotation of an MMD bone at `frame`, cached per frame.</summary>
        public static Quaternion World(string name, Dictionary<string, Track> tracks, Dictionary<string, Quaternion> cache, float frame, int guard = 0)
        {
            if (cache.TryGetValue(name, out Quaternion w)) return w;
            Quaternion local = Quaternion.identity;
            if (tracks.TryGetValue(name, out Track t)) { Sample(t, frame, out _, out Quaternion r); local = ConvertRot(r); }
            w = (guard < 16 && Parent.TryGetValue(name, out string parent)) ? World(parent, tracks, cache, frame, guard + 1) * local : local;
            cache[name] = w;
            return w;
        }

        /// <summary>Translation of the root chain (全ての親 + センター + グルーブ) in MMD space.</summary>
        public static Vector3 RootChainPos(Track all, Track centre, Track groove, float frame)
        {
            Sample(all, frame, out Vector3 pa, out Quaternion ra);
            Sample(centre, frame, out Vector3 pc, out _);
            Sample(groove, frame, out Vector3 pg, out _);
            return pa + ra * (pc + pg);
        }
    }
}
