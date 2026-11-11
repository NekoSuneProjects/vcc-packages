namespace NekoSune.LipSync.Editor
{
    /// <summary>
    /// The 15 VRChat visemes, in the exact order VRCAvatarDescriptor.VisemeBlendShapes uses.
    /// Never reorder these — index is the contract with the SDK.
    /// </summary>
    internal static class NekoVisemes
    {
        public const int Sil = 0;
        public const int PP  = 1;
        public const int FF  = 2;
        public const int TH  = 3;
        public const int DD  = 4;
        public const int KK  = 5;
        public const int CH  = 6;
        public const int SS  = 7;
        public const int NN  = 8;
        public const int RR  = 9;
        public const int AA  = 10;
        public const int E   = 11;
        public const int IH  = 12;
        public const int OH  = 13;
        public const int OU  = 14;

        public const int Count = 15;

        public static readonly string[] Names =
        {
            "sil", "PP", "FF", "TH", "DD", "kk", "CH", "SS", "nn", "RR", "aa", "E", "ih", "oh", "ou"
        };

        /// <summary>How wide the mouth is for each viseme; used to drive jaw / single-shape output.</summary>
        public static readonly float[] Openness =
        {
            0.00f, // sil
            0.02f, // PP  (lips pressed)
            0.10f, // FF
            0.18f, // TH
            0.30f, // DD
            0.28f, // kk
            0.26f, // CH
            0.14f, // SS
            0.16f, // nn
            0.30f, // RR
            1.00f, // aa
            0.65f, // E
            0.45f, // ih
            0.70f, // oh
            0.40f  // ou
        };
    }
}
