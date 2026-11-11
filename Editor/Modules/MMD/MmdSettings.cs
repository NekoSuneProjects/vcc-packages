using System;

namespace MotionConverter.Mmd
{
    /// <summary>Options for one MMD / VMD conversion. Serializable so the window keeps them across recompiles.</summary>
    [Serializable]
    public class MmdSettings
    {
        public const float Fps = 30f;

        public string artist = "";
        public string title = "";

        public bool hipsTranslation = true;
        public bool startAtOrigin = true;
        public bool includeExpressions = true;
        public bool includeFingers = true;
        public bool cameraUsePerspective = true;

        public float fallbackUnitsPerMmd = 0.08f; // only used if the avatar has no usable legs
        public float sourceLegLength = 9.5f;      // MMD units, hip joint -> ankle joint of the model the VMD was made for
        public float armAPoseAngle = 40f;         // degrees below horizontal of the source model's rest arms

        public bool solveFootIk = true;
        public float footIkStrength = 1f;
        public float maxFootReach = 0.99f;
        public bool keepFeetAboveFloor = true;
    }
}
