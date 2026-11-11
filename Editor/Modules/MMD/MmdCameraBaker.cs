using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MotionConverter.Mmd
{
    /// <summary>VMD camera keys → transform + field-of-view clip for a Camera GameObject in a Unity scene.</summary>
    internal static class MmdCameraBaker
    {
        /// <summary>Returns an unsaved clip, or null when the VMD has no camera keys.</summary>
        public static AnimationClip Bake(VmdFile vmd, GameObject avatar, MmdSettings s, ConversionLog log)
        {
            if (vmd.cameras.Count == 0)
            {
                log.Warn(L.T("The camera VMD has no camera frames. Pick a camera motion file."));
                return null;
            }

            var clip = new AnimationClip { name = "MMD_Camera", frameRate = MmdSettings.Fps, legacy = false };
            var keys = vmd.cameras.OrderBy(c => c.frame).GroupBy(c => c.frame).Select(g => g.Last()).ToList();
            var positions = new List<KeyValuePair<float, Vector3>>();
            var rotations = new List<KeyValuePair<float, Quaternion>>();
            var fovs = new List<Keyframe>();

            // Use the same metres-per-MMD-unit as the body so the camera and dancer stay in proportion.
            float scale = MmdHumanoidBaker.BodyScale(avatar != null ? avatar.GetComponentInChildren<Animator>() : null, s);
            foreach (var f in keys)
            {
                // MMD camera defines a look-at centre and boom distance; convert into transform of the camera object.
                var euler = new Vector3(-f.euler.x, -f.euler.y, f.euler.z);
                Quaternion q = Quaternion.Euler(euler);
                var worldPosition = new Vector3(f.pos.x, f.pos.y, -f.pos.z) * scale + q * new Vector3(0f, 0f, -f.distance * scale);
                // The body is converted with a 180-degree turn about Y instead of a Z flip, which is
                // the old convention reflected across X. Reflect the camera the same way to stay aligned.
                worldPosition.x = -worldPosition.x;
                q = new Quaternion(q.x, -q.y, -q.z, q.w);
                float time = f.frame / MmdSettings.Fps;
                positions.Add(new KeyValuePair<float, Vector3>(time, worldPosition));
                rotations.Add(new KeyValuePair<float, Quaternion>(time, q));
                fovs.Add(new Keyframe(time, Mathf.Clamp(f.fov, 1, 179)));
            }

            // Bindings target the Camera GameObject itself (empty path); the Camera component must be on it for FOV.
            MmdCurves.AddPos(clip, "", positions);
            MmdCurves.AddQuat(clip, "", rotations);
            if (s.cameraUsePerspective) MmdCurves.SetCurve(clip, "", typeof(Camera), "field of view", fovs);

            log.Info(L.T("Camera: {0} frames. Play it with an Animator on a Camera GameObject in a Unity scene.", keys.Count));
            return clip;
        }
    }
}
