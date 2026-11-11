using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace MotionConverter.Mmd
{
    internal sealed class VmdBoneFrame { public string name; public uint frame; public Vector3 pos; public Quaternion rot; public byte[] interp; }
    internal sealed class VmdMorphFrame { public string name; public uint frame; public float weight; }
    internal sealed class VmdCameraFrame { public uint frame; public float distance; public Vector3 pos; public Vector3 euler; public uint fov; public bool perspective; }

    /// <summary>Parsed Vocaloid Motion Data (.vmd): bone, morph and camera keys.</summary>
    internal sealed class VmdFile
    {
        public readonly List<VmdBoneFrame> bones = new List<VmdBoneFrame>();
        public readonly List<VmdMorphFrame> morphs = new List<VmdMorphFrame>();
        public readonly List<VmdCameraFrame> cameras = new List<VmdCameraFrame>();

        public static bool IsVmd(string path) => path != null && path.EndsWith(".vmd", StringComparison.OrdinalIgnoreCase);

        public static VmdFile Read(string path)
        {
            if (!IsVmd(path)) throw new InvalidDataException(L.T("{0} is not a .vmd file.", path));
            var result = new VmdFile();
            using (var reader = new BinaryReader(File.OpenRead(path)))
            {
                var signature = Encoding.ASCII.GetString(reader.ReadBytes(30));
                if (!signature.StartsWith("Vocaloid Motion Data")) throw new InvalidDataException(L.T("Not a valid VMD: {0}", path));
                reader.ReadBytes(20); // model name
                Encoding japanese = Encoding.GetEncoding(932);

                int n = CheckedCount(reader.ReadUInt32(), 2000000);
                for (int i = 0; i < n; i++)
                {
                    var name = DecodeName(reader.ReadBytes(15), japanese);
                    var f = new VmdBoneFrame { name = name, frame = reader.ReadUInt32(), pos = V3(reader) };
                    var q = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    float mag = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
                    f.rot = mag > 1e-6f ? new Quaternion(q.x / mag, q.y / mag, q.z / mag, q.w / mag) : Quaternion.identity;
                    f.interp = reader.ReadBytes(64);
                    result.bones.Add(f);
                }

                n = CheckedCount(reader.ReadUInt32(), 2000000);
                for (int i = 0; i < n; i++)
                    result.morphs.Add(new VmdMorphFrame { name = DecodeName(reader.ReadBytes(15), japanese), frame = reader.ReadUInt32(), weight = reader.ReadSingle() });

                if (reader.BaseStream.Position + 4 <= reader.BaseStream.Length)
                {
                    n = CheckedCount(reader.ReadUInt32(), 2000000);
                    for (int i = 0; i < n; i++)
                    {
                        var f = new VmdCameraFrame { frame = reader.ReadUInt32(), distance = reader.ReadSingle(), pos = V3(reader), euler = V3(reader) * Mathf.Rad2Deg };
                        reader.ReadBytes(24); f.fov = reader.ReadUInt32(); f.perspective = reader.ReadByte() == 0;
                        result.cameras.Add(f);
                    }
                }
            }
            return result;
        }

        // VMD names are NUL-terminated; anything after the first NUL is padding garbage.
        private static string DecodeName(byte[] bytes, Encoding enc)
        {
            int len = Array.IndexOf(bytes, (byte)0);
            if (len < 0) len = bytes.Length;
            return enc.GetString(bytes, 0, len);
        }

        private static int CheckedCount(uint v, uint max)
        {
            if (v > max) throw new InvalidDataException(L.T("VMD count exceeds maximum: {0}", v));
            return (int)v;
        }

        private static Vector3 V3(BinaryReader br) => new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
    }
}
