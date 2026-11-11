using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MotionConverter
{
    /// <summary>
    /// Reads a UEFormat animation (.ueanim, as exported by FModel / CUE4Parse) and turns it into the
    /// same kind of Generic transform clip the FBX route produces, so the existing Fortnite pipeline
    /// (SourceRig → T-pose → Humanoid bake) can run on it unchanged.
    ///
    /// A .ueanim holds only per-bone local keys (no hierarchy, no mesh). The hierarchy is rebuilt from
    /// the standard Fortnite / UE mannequin skeleton by bone name; only the bones a Humanoid needs
    /// (body, spine, neck/head, arms, fingers, legs, toes) are kept. Twist, IK, face, prop and cloth
    /// bones are ignored.
    ///
    /// Axes: UE is left-handed, Z-up, X-forward, Y-right. Unity is left-handed, Y-up, Z-forward,
    /// X-right. (x,y,z) → (y,z,x) is a pure rotation between them, so positions AND quaternion vector
    /// parts are just permuted, with no sign flips. Centimetres → metres.
    /// </summary>
    public static class UeAnimImporter
    {
        public sealed class Info
        {
            public string name;
            public int frames;
            public float fps;
            public int tracksInFile;
            public int bonesUsed;
        }

        private struct VecKey { public int frame; public Vector3 v; }
        private struct QuatKey { public int frame; public Quaternion q; }
        private sealed class Track
        {
            public string name;
            public List<VecKey> pos = new List<VecKey>();
            public List<QuatKey> rot = new List<QuatKey>();
        }

        private static readonly Dictionary<string, string> Parent = BuildParents();

        private static Dictionary<string, string> BuildParents()
        {
            var d = new Dictionary<string, string>
            {
                { "pelvis", "root" },
                { "spine_01", "pelvis" }, { "spine_02", "spine_01" }, { "spine_03", "spine_02" },
                { "spine_04", "spine_03" }, { "spine_05", "spine_04" },
                { "neck_01", "spine_05" }, { "neck_02", "neck_01" }, { "head", "neck_02" },
            };
            foreach (string s in new[] { "l", "r" })
            {
                d["clavicle_" + s] = "spine_05";
                d["upperarm_" + s] = "clavicle_" + s;
                d["lowerarm_" + s] = "upperarm_" + s;
                d["hand_" + s] = "lowerarm_" + s;
                d["thumb_01_" + s] = "hand_" + s;
                d["thumb_02_" + s] = "thumb_01_" + s;
                d["thumb_03_" + s] = "thumb_02_" + s;
                foreach (string f in new[] { "index", "middle", "ring", "pinky" })
                {
                    d[f + "_metacarpal_" + s] = "hand_" + s;
                    d[f + "_01_" + s] = f + "_metacarpal_" + s;
                    d[f + "_02_" + s] = f + "_01_" + s;
                    d[f + "_03_" + s] = f + "_02_" + s;
                }
                d["thigh_" + s] = "pelvis";
                d["calf_" + s] = "thigh_" + s;
                d["foot_" + s] = "calf_" + s;
                d["ball_" + s] = "foot_" + s;
            }
            return d;
        }

        public static bool IsUeAnim(string assetPath) =>
            !string.IsNullOrEmpty(assetPath) && assetPath.EndsWith(".ueanim", StringComparison.OrdinalIgnoreCase);

        public static AnimationClip Load(string assetPath, out Info info)
        {
            if (!IsUeAnim(assetPath)) throw new InvalidDataException(L.T("That is not a .ueanim file."));
            byte[] bytes = File.ReadAllBytes(assetPath);
            using (var r = new BinaryReader(new MemoryStream(bytes)))
            {
                string magic = Encoding.ASCII.GetString(r.ReadBytes(8));
                if (magic != "UEFORMAT") throw new InvalidDataException(L.T("Not a UEFormat file (missing UEFORMAT header)."));
                string type = ReadFString(r);
                if (type != "UEANIM") throw new InvalidDataException(L.T("This is a UEFormat '{0}' file, not an animation (UEANIM).", type));
                int version = r.ReadByte();
                string objectName = ReadFString(r);
                ReadFString(r); // object path
                if (r.ReadByte() != 0)
                    throw new InvalidDataException(L.T("This .ueanim is compressed. Re-export it from FModel with compression set to None."));

                // Some versions put a small int here before the first chunk; skip it if the bytes aren't a chunk name.
                long chunkStart = r.BaseStream.Position;
                if (!LooksLikeChunkName(r)) { r.BaseStream.Position = chunkStart + 4; }
                else r.BaseStream.Position = chunkStart;

                int frames = 0; float fps = 30f;
                var tracks = new List<Track>();
                while (r.BaseStream.Position + 8 <= r.BaseStream.Length)
                {
                    string chunk = ReadFString(r);
                    int size = r.ReadInt32();
                    long next = r.BaseStream.Position + size;
                    if (size < 0 || next > r.BaseStream.Length) break;
                    if (chunk == "METADATA")
                    {
                        frames = r.ReadInt32();
                        fps = r.ReadSingle();
                    }
                    else if (chunk == "TRACKS")
                    {
                        int count = r.ReadInt32();
                        for (int i = 0; i < count; i++) tracks.Add(ReadTrack(r));
                    }
                    r.BaseStream.Position = next;
                }

                if (tracks.Count == 0) throw new InvalidDataException(L.T("No bone tracks found in this .ueanim."));
                if (fps < 1f || fps > 240f || float.IsNaN(fps)) fps = 30f;

                var clip = Build(tracks, objectName, fps, out int used);
                info = new Info { name = objectName, frames = frames, fps = fps, tracksInFile = tracks.Count, bonesUsed = used };
                return clip;
            }
        }

        private static bool LooksLikeChunkName(BinaryReader r)
        {
            long pos = r.BaseStream.Position;
            try
            {
                int n = r.ReadInt32();
                if (n < 3 || n > 32) return false;
                byte[] b = r.ReadBytes(n);
                if (b.Length != n) return false;
                foreach (byte c in b) if (!(c >= 'A' && c <= 'Z') && c != '_') return false;
                return true;
            }
            finally { r.BaseStream.Position = pos; }
        }

        private static string ReadFString(BinaryReader r)
        {
            int n = r.ReadInt32();
            if (n == 0) return "";
            if (n < 0)
            {
                byte[] w = r.ReadBytes(-n * 2);
                return Encoding.Unicode.GetString(w).TrimEnd('\0');
            }
            byte[] b = r.ReadBytes(n);
            return Encoding.UTF8.GetString(b).TrimEnd('\0');
        }

        private static Track ReadTrack(BinaryReader r)
        {
            var t = new Track { name = ReadFString(r) };
            int n = r.ReadInt32();
            for (int i = 0; i < n; i++)
            {
                int f = r.ReadInt32();
                var v = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                // UE (x fwd, y right, z up) → Unity (x right, y up, z fwd), cm → m.
                t.pos.Add(new VecKey { frame = f, v = new Vector3(v.y, v.z, v.x) * 0.01f });
            }
            n = r.ReadInt32();
            for (int i = 0; i < n; i++)
            {
                int f = r.ReadInt32();
                float x = r.ReadSingle(), y = r.ReadSingle(), z = r.ReadSingle(), w = r.ReadSingle();
                float m = Mathf.Sqrt(x * x + y * y + z * z + w * w);
                if (m < 1e-6f) { x = y = z = 0f; w = 1f; m = 1f; }
                t.rot.Add(new QuatKey { frame = f, q = new Quaternion(y / m, z / m, x / m, w / m) });
            }
            n = r.ReadInt32();                       // scale keys (ignored)
            r.BaseStream.Position += (long)n * 16;
            return t;
        }

        private static AnimationClip Build(List<Track> tracks, string name, float fps, out int used)
        {
            var byName = new Dictionary<string, Track>();
            foreach (var t in tracks) if (!byName.ContainsKey(t.name)) byName[t.name] = t;

            var clip = new AnimationClip { name = name, frameRate = Mathf.Max(1f, Mathf.Round(fps)), legacy = false };
            clip.hideFlags = HideFlags.HideAndDontSave;
            used = 0;
            foreach (var kv in Parent)
            {
                if (!byName.TryGetValue(kv.Key, out var t)) continue;
                string path = PathOf(kv.Key);
                if (t.pos.Count > 0)
                {
                    SetCurve(clip, path, "m_LocalPosition.x", t.pos.Select(k => new Keyframe(k.frame / fps, k.v.x)));
                    SetCurve(clip, path, "m_LocalPosition.y", t.pos.Select(k => new Keyframe(k.frame / fps, k.v.y)));
                    SetCurve(clip, path, "m_LocalPosition.z", t.pos.Select(k => new Keyframe(k.frame / fps, k.v.z)));
                }
                if (t.rot.Count > 0)
                {
                    var q = t.rot.OrderBy(k => k.frame).Select(k => new KeyValuePair<float, Quaternion>(k.frame / fps, k.q)).ToList();
                    for (int i = 1; i < q.Count; i++)   // keep neighbouring quaternions on one hemisphere
                        if (Quaternion.Dot(q[i - 1].Value, q[i].Value) < 0f)
                        { var v = q[i].Value; q[i] = new KeyValuePair<float, Quaternion>(q[i].Key, new Quaternion(-v.x, -v.y, -v.z, -v.w)); }
                    SetCurve(clip, path, "m_LocalRotation.x", q.Select(k => new Keyframe(k.Key, k.Value.x)));
                    SetCurve(clip, path, "m_LocalRotation.y", q.Select(k => new Keyframe(k.Key, k.Value.y)));
                    SetCurve(clip, path, "m_LocalRotation.z", q.Select(k => new Keyframe(k.Key, k.Value.z)));
                    SetCurve(clip, path, "m_LocalRotation.w", q.Select(k => new Keyframe(k.Key, k.Value.w)));
                }
                used++;
            }
            if (used == 0)
                throw new InvalidDataException(L.T("None of the expected Fortnite/UE bones (pelvis, spine_01, thigh_l...) were found in this .ueanim."));
            return clip;
        }

        private static string PathOf(string bone)
        {
            var parts = new List<string> { bone };
            string p = bone;
            while (Parent.TryGetValue(p, out string up)) { parts.Add(up); p = up; }
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static void SetCurve(AnimationClip clip, string path, string property, IEnumerable<Keyframe> keys)
        {
            var arr = keys.OrderBy(k => k.time).GroupBy(k => k.time).Select(g => g.Last()).ToArray();
            if (arr.Length == 0) return;
            var curve = new AnimationCurve(arr);
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), curve);
        }
    }
}
