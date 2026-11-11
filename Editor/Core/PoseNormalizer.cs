using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MotionConverter
{
    /// <summary>
    /// Gets the source rig into the shape Unity's humanoid builder expects: standing on +Y,
    /// facing +Z, about human size, and in a T-pose.
    /// </summary>
    public static class PoseNormalizer
    {
        /// <summary>Rotate and scale the rig's Upright node. Call with the rig posed (e.g. frame 0).</summary>
        public static void StandUpright(SourceRig rig, Dictionary<HumanBodyBones, Transform> human, ConversionLog log)
        {
            rig.Upright.localRotation = Quaternion.identity;
            rig.Upright.localScale = Vector3.one;

            // Snap "up" to the nearest axis so a leaning first frame doesn't tilt the whole clip.
            Vector3 up = SnapToAxis(human[HumanBodyBones.Head].position - human[HumanBodyBones.Hips].position);
            Vector3 right = Vector3.ProjectOnPlane(
                human[HumanBodyBones.RightUpperLeg].position - human[HumanBodyBones.LeftUpperLeg].position, up);
            if (right.sqrMagnitude < 1e-12f) right = Vector3.Cross(up, Vector3.forward);
            Vector3 forward = Vector3.Cross(right.normalized, up);

            rig.Upright.rotation = Quaternion.Inverse(Quaternion.LookRotation(forward, up));
            log.Info(L.T("Upright fix: {0}", rig.Upright.eulerAngles));

            // UE rips often arrive at 1/100 scale. Bring the hips to ~1 m so the solver isn't
            // working with sub-millimetre bones.
            float hipHeight = human[HumanBodyBones.Hips].position.y -
                              Mathf.Min(human[HumanBodyBones.LeftFoot].position.y, human[HumanBodyBones.RightFoot].position.y);
            if (hipHeight > 1e-6f && (hipHeight < 0.2f || hipHeight > 5f))
            {
                rig.Upright.localScale = Vector3.one / hipHeight;
                log.Info(L.T("Rig was {0:0.####} units to the hips; rescaled x{1:0.#}.", hipHeight, 1f / hipHeight));
            }
        }

        /// <summary>Straighten limbs into a T-pose. Only used to define the avatar; sampling overwrites it.</summary>
        public static void ForceTPose(Dictionary<HumanBodyBones, Transform> human)
        {
            Transform G(HumanBodyBones b) => human.TryGetValue(b, out var t) ? t : null;

            // Hips square to the front, spine straight up, chest square to the front.
            Twist(G(HumanBodyBones.Hips), G(HumanBodyBones.LeftUpperLeg), G(HumanBodyBones.RightUpperLeg), Vector3.up, Vector3.right);

            var spine = new[] { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Neck, HumanBodyBones.Head }
                .Select(G).Where(t => t != null).ToList();
            for (int i = 0; i < spine.Count - 1; i++) Align(spine[i], spine[i + 1], Vector3.up);

            var chestTop = G(HumanBodyBones.UpperChest) ?? G(HumanBodyBones.Chest);
            Twist(chestTop, G(HumanBodyBones.LeftUpperArm), G(HumanBodyBones.RightUpperArm), Vector3.up, Vector3.right);

            foreach (bool left in new[] { true, false })
            {
                StraightenLeg(G, left);
                StraightenArm(G, left);
            }
        }

        private static void StraightenLeg(System.Func<HumanBodyBones, Transform> G, bool left)
        {
            var upper = G(left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg);
            var lower = G(left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
            var foot = G(left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
            var toes = G(left ? HumanBodyBones.LeftToes : HumanBodyBones.RightToes);

            Align(upper, lower, Vector3.down);
            Align(lower, foot, Vector3.down);
            Twist(foot, foot, toes, Vector3.up, Vector3.forward);
        }

        private static void StraightenArm(System.Func<HumanBodyBones, Transform> G, bool left)
        {
            Vector3 outward = left ? Vector3.left : Vector3.right;
            var upper = G(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
            var lower = G(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            var hand = G(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);

            Align(upper, lower, outward);
            Align(lower, hand, outward);
            Align(hand, G(BoneMap.Finger(left, 2, 0)), outward);                       // middle knuckle
            Twist(hand, G(BoneMap.Finger(left, 4, 0)), G(BoneMap.Finger(left, 1, 0)),  // little → index
                outward, Vector3.forward);                                              // = palm down

            // Thumb (f = 0) keeps its natural angle.
            for (int f = 1; f < BoneMap.UnityFingers.Length; f++)
            {
                Align(G(BoneMap.Finger(left, f, 0)), G(BoneMap.Finger(left, f, 1)), outward);
                Align(G(BoneMap.Finger(left, f, 1)), G(BoneMap.Finger(left, f, 2)), outward);
            }
        }

        /// <summary>Rotate `bone` so the direction to `towards` points along `dir`.</summary>
        private static void Align(Transform bone, Transform towards, Vector3 dir)
        {
            if (bone == null || towards == null) return;
            Vector3 current = towards.position - bone.position;
            if (current.sqrMagnitude < 1e-12f) return;
            bone.rotation = Quaternion.FromToRotation(current, dir) * bone.rotation;
        }

        /// <summary>Spin `bone` about `axis` until (b - a), flattened onto the axis plane, points along `target`.</summary>
        private static void Twist(Transform bone, Transform a, Transform b, Vector3 axis, Vector3 target)
        {
            if (bone == null || a == null || b == null) return;
            Vector3 current = Vector3.ProjectOnPlane(b.position - a.position, axis);
            if (current.sqrMagnitude < 1e-12f) return;
            bone.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(current, target, axis), axis) * bone.rotation;
        }

        private static Vector3 SnapToAxis(Vector3 v)
        {
            var a = new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
            if (a.x >= a.y && a.x >= a.z) return new Vector3(Mathf.Sign(v.x), 0, 0);
            if (a.y >= a.z) return new Vector3(0, Mathf.Sign(v.y), 0);
            return new Vector3(0, 0, Mathf.Sign(v.z));
        }
    }
}
