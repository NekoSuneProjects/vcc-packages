using UnityEditor;

namespace NekoSune.WorldUiAssistant
{
    /// <summary>How much help the Assistant shows. Lowered as the user learns.</summary>
    internal enum HintLevel { FullHelp, SomeHelp, Expert }

    /// <summary>Per-machine help settings (EditorPrefs).</summary>
    internal static class Help
    {
        private const string LevelPref = "NekoSune.WorldUiAssistant.HintLevel";
        private const string ExplainPref = "NekoSune.WorldUiAssistant.ExplainMode";

        public static HintLevel Level
        {
            get => (HintLevel)EditorPrefs.GetInt(LevelPref, (int)HintLevel.FullHelp);
            set => EditorPrefs.SetInt(LevelPref, (int)value);
        }

        /// <summary>Shows which Unity component and property each field changes.</summary>
        public static bool Explain
        {
            get => EditorPrefs.GetBool(ExplainPref, false);
            set => EditorPrefs.SetBool(ExplainPref, value);
        }

        public static bool ShowHints => Level != HintLevel.Expert;
        public static bool ShowHowItWorks => Level == HintLevel.FullHelp;

        public static readonly string[] LevelNames = { "Full help", "Some help", "Expert" };
        public static readonly string[] LevelTips =
        {
            "Every hint and a \"How it works in Unity\" note on each item.",
            "Short hints only. Good once you know the basics.",
            "No hints – just the tools. Warnings are still shown.",
        };
    }
}
