using UnityEditor;
using UnityEngine;

namespace MotionConverter
{
    /// <summary>
    /// Generic transform clip → Humanoid muscle clip.
    ///
    /// 1. Rebuild the source skeleton from the clip (no source model needed).
    /// 2. Pose it at frame 0, stand it upright at human scale, force a T-pose.
    /// 3. Build a humanoid Avatar from that T-pose.
    /// 4. Play the clip on the rig and record muscles every frame (applying speed/trim/delay).
    /// 5. Save to the output folder.
    /// </summary>
    public static class MotionConversion
    {
        public static AnimationClip Run(ConversionSettings settings, ConversionLog log)
        {
            using (var rig = new SourceRig(settings.sourceClip))
            {
                log.Info(L.T("Rebuilt rig: {0} bones from {1} curves.", rig.Bones.Count, rig.CurveCount));

                var human = BoneMap.Resolve(rig, settings.includeFingers);

                rig.Sample(0f);
                PoseNormalizer.StandUpright(rig, human, log);
                PoseNormalizer.ForceTPose(human);

                var avatar = HumanoidBaker.BuildAvatar(rig, human);
                try
                {
                    var clip = HumanoidBaker.Bake(rig, avatar, settings, log);
                    string path = OutputLocation.PathFor(settings);
                    clip = OutputLocation.Save(clip, path);
                    log.Info(L.T("Saved: {0}", path));
                    return clip;
                }
                finally
                {
                    Object.DestroyImmediate(avatar);
                    EditorUtility.ClearProgressBar();
                }
            }
        }
    }
}
