using System;
using UnityEditor;
using UnityEngine;
using G = MotionConverter.MotionConverterGUI;

namespace MotionConverter.Fortnite
{
    /// <summary>Fortnite emotes from a UEFormat .ueanim file. (Generic FBX + .anim clips have their own tab.)</summary>
    [Serializable]
    public sealed class FortniteModule : ClipPipelineModule
    {
        private const string AcceptedPref = "MotionConverter.Fortnite.DisclaimerAccepted";

        public static string Disclaimer => L.T(DisclaimerText);

        private const string DisclaimerText =
            "I am not responsible for your actions in ripping assets or emotes from Fortnite, whether with FortnitePorting, " +
            "FModel or any other form of Fortnite asset ripping.\n\n" +
            "This tool only converts motions into working Humanoid avatar animations. It has nothing to do with Fortnite " +
            "ripping or any other such actions. You are responsible for having the rights to any animation you convert and use.\n\n" +
            "Fortnite is a trademark of Epic Games. This tool is not affiliated with or endorsed by Epic Games.";

        [SerializeField] private DefaultAsset ueFile;
        [NonSerialized] private AnimationClip ueClip;
        [NonSerialized] private DefaultAsset ueLoadedFor;
        [NonSerialized] private UeAnimImporter.Info ueInfo;
        [NonSerialized] private string ueError;

        public override string Id => "fortnite";
        public override string TabName => L.T("Fortnite (.ueanim)");
        public override int Order => 10;
        public override string Subtitle => L.T("Fortnite .ueanim  →  Humanoid");
        public override string Description => L.T("Convert a Fortnite .ueanim emote (UEFormat, no FBX needed) into a Humanoid motion. Any of the CLF / CLM / CMF / CMM body variants works.");

        protected override string SourceCardTitle => L.T("Source & naming  (.ueanim)");
        protected override string MissingSourceHint => L.T("Pick a .ueanim file to start.");
        protected override string BlockReason => G.IsAccepted(AcceptedPref) ? null : L.T("Read and accept the disclaimer at the top to convert.");

        // ------------------------------------------------------------------ GUI

        protected override void DrawBeforeSource() => G.Disclaimer(L.T("Disclaimer"), Disclaimer, AcceptedPref);

        protected override void DrawSourceInput()
        {
            ueFile = (DefaultAsset)EditorGUILayout.ObjectField(L.T("UEFormat animation"), ueFile, typeof(DefaultAsset), false);
            if (ueFile != null && !UeAnimImporter.IsUeAnim(AssetDatabase.GetAssetPath(ueFile)))
                EditorGUILayout.HelpBox(L.T("That file is not a .ueanim. Drop the .ueanim animation file from your Project window."), MessageType.Warning);
            else if (ueFile == null)
                EditorGUILayout.HelpBox(L.T("Copy the .ueanim into your Assets folder and drag it in here."), MessageType.None);

            if (!string.IsNullOrEmpty(ueError))
                EditorGUILayout.HelpBox(ueError, MessageType.Error);

            if (ueClip != null && ueInfo != null)
                G.ChipRow($"{ueClip.length:0.##}s", $"{ueInfo.fps:0.##} fps", L.T("{0} frames", ueInfo.frames), L.T("{0}/{1} bones used", ueInfo.bonesUsed, ueInfo.tracksInFile));
        }

        // ------------------------------------------------------------------ source

        protected override void PrepareSource()
        {
            SyncUeClip();
            settings.sourceClip = ueClip;
            settings.sourceAssetPath = ueFile != null ? AssetDatabase.GetAssetPath(ueFile) : null;
        }

        /// <summary>Parses the selected .ueanim into an in-memory Generic clip for the shared pipeline.</summary>
        private void SyncUeClip()
        {
            if (ueFile == null)
            {
                FreeUeClip();
                ueLoadedFor = null; ueError = null; ueInfo = null;
                return;
            }

            if (ueLoadedFor == ueFile && (ueClip != null || ueError != null)) return;

            FreeUeClip();
            ueError = null; ueInfo = null; ueLoadedFor = ueFile;
            settings.sampleRate = 0;
            try { ueClip = UeAnimImporter.Load(AssetDatabase.GetAssetPath(ueFile), out ueInfo); }
            catch (Exception e) { ueError = e.Message; Debug.LogException(e); }
        }

        private void FreeUeClip()
        {
            if (ueClip == null) return;
            if (settings.sourceClip == ueClip) settings.sourceClip = null;
            UnityEngine.Object.DestroyImmediate(ueClip);
            ueClip = null;
        }

        public override void OnDisable() => FreeUeClip();
    }
}
