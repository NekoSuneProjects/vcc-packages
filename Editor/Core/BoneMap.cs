using System;
using System.Collections.Generic;
using UnityEngine;

namespace MotionConverter
{
    /// <summary>
    /// Maps source bone names (UE / Fortnite-style: pelvis, spine_01, thigh_l, index_01_l...) to
    /// Unity human bones.
    /// </summary>
    public static class BoneMap
    {
        // First name found wins.
        private static readonly (HumanBodyBones bone, string[] names)[] Body =
        {
            (HumanBodyBones.Hips,          new[] { "pelvis", "hips", "Hips" }),
            (HumanBodyBones.Spine,         new[] { "spine_01", "Spine" }),
            (HumanBodyBones.Chest,         new[] { "spine_03", "spine_02", "Chest" }),
            (HumanBodyBones.UpperChest,    new[] { "spine_05", "spine_04", "UpperChest" }),
            (HumanBodyBones.Neck,          new[] { "neck_01", "Neck" }),
            (HumanBodyBones.Head,          new[] { "head", "Head" }),

            (HumanBodyBones.LeftShoulder,  new[] { "clavicle_l" }),
            (HumanBodyBones.LeftUpperArm,  new[] { "upperarm_l" }),
            (HumanBodyBones.LeftLowerArm,  new[] { "lowerarm_l" }),
            (HumanBodyBones.LeftHand,      new[] { "hand_l" }),
            (HumanBodyBones.RightShoulder, new[] { "clavicle_r" }),
            (HumanBodyBones.RightUpperArm, new[] { "upperarm_r" }),
            (HumanBodyBones.RightLowerArm, new[] { "lowerarm_r" }),
            (HumanBodyBones.RightHand,     new[] { "hand_r" }),

            (HumanBodyBones.LeftUpperLeg,  new[] { "thigh_l" }),
            (HumanBodyBones.LeftLowerLeg,  new[] { "calf_l" }),
            (HumanBodyBones.LeftFoot,      new[] { "foot_l" }),
            (HumanBodyBones.LeftToes,      new[] { "ball_l" }),
            (HumanBodyBones.RightUpperLeg, new[] { "thigh_r" }),
            (HumanBodyBones.RightLowerLeg, new[] { "calf_r" }),
            (HumanBodyBones.RightFoot,     new[] { "foot_r" }),
            (HumanBodyBones.RightToes,     new[] { "ball_r" }),
        };

        public static readonly string[] SourceFingers = { "thumb", "index", "middle", "ring", "pinky" };
        public static readonly string[] UnityFingers = { "Thumb", "Index", "Middle", "Ring", "Little" };
        private static readonly string[] Segments = { "Proximal", "Intermediate", "Distal" };

        public static readonly HumanBodyBones[] Required =
        {
            HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Head,
            HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
            HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
            HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot,
        };

        public static Dictionary<HumanBodyBones, Transform> Resolve(SourceRig rig, bool includeFingers)
        {
            var map = new Dictionary<HumanBodyBones, Transform>();
            foreach (var (bone, names) in Body)
            {
                foreach (var name in names)
                {
                    var t = rig.Find(name);
                    if (t != null) { map[bone] = t; break; }
                }
            }

            if (includeFingers)
            {
                foreach (bool left in new[] { true, false })
                {
                    for (int f = 0; f < SourceFingers.Length; f++)
                    {
                        for (int s = 0; s < Segments.Length; s++)
                        {
                            var t = rig.Find($"{SourceFingers[f]}_0{s + 1}{(left ? "_l" : "_r")}");
                            if (t != null) map[Finger(left, f, s)] = t;
                        }
                    }
                }
            }

            foreach (var req in Required)
            {
                if (!map.ContainsKey(req))
                    throw new Exception(L.T("No source bone found for {0}. This rig's naming isn't supported.", req));
            }

            return map;
        }

        /// <summary>HumanBodyBones for finger index f (0 = thumb) and segment s (0 = proximal).</summary>
        public static HumanBodyBones Finger(bool left, int f, int s) =>
            (HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), $"{(left ? "Left" : "Right")}{UnityFingers[f]}{Segments[s]}");
    }
}
