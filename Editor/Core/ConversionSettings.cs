using System;
using UnityEngine;

namespace MotionConverter
{
    /// <summary>Everything one conversion needs. Serializable so the window keeps it across recompiles.</summary>
    [Serializable]
    public class ConversionSettings
    {
        public AnimationClip sourceClip;

        /// <summary>Project path the clip came from when it is built in memory (e.g. from a .ueanim). Not saved.</summary>
        [NonSerialized] public string sourceAssetPath;

        [Header("Naming")]
        public string artist = "";
        public string title = "";

        [Header("Output")]
        public float sampleRate;          // 0 = use the source clip's frame rate
        public bool includeFingers = true;
        public bool bakeRootMotion = true;
        public bool loop;

        [Header("Timing")]
        public float speed = 1f;
        public float trimStart;
        public float delayStart;

        public const float MinSpeed = 0.25f;
        public const float MaxSpeed = 3f;

        public float SourceLength => sourceClip != null ? sourceClip.length : 0f;

        /// <summary>Seconds of source motion actually used after trimming.</summary>
        public float UsedSourceLength => Mathf.Max(0f, SourceLength - trimStart);

        public float OutputLength => delayStart + UsedSourceLength / Mathf.Max(0.01f, speed);

        public float FrameRate => sampleRate > 0f ? sampleRate : (sourceClip != null ? sourceClip.frameRate : 30f);

        /// <summary>Output time → source time: hold during the delay, then play from trimStart at `speed`.</summary>
        public float ToSourceTime(float outputTime) =>
            Mathf.Clamp(trimStart + Mathf.Max(0f, outputTime - delayStart) * speed, 0f, SourceLength);

        /// <summary>Speed that makes the output last `targetSeconds`.</summary>
        public float SpeedForLength(float targetSeconds) =>
            Mathf.Clamp(UsedSourceLength / Mathf.Max(0.01f, targetSeconds - delayStart), MinSpeed, MaxSpeed);
    }
}
