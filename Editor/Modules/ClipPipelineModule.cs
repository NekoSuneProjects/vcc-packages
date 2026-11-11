using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using S = MotionConverter.MotionConverterStyles;
using G = MotionConverter.MotionConverterGUI;

namespace MotionConverter
{
    /// <summary>
    /// Base for any source that can provide a Generic (Transform-curve) AnimationClip.
    /// Subclasses only supply the clip; naming, timing, conversion to Humanoid, avatar setup and the log are shared.
    /// </summary>
    [Serializable]
    public abstract class ClipPipelineModule : MotionSourceModule
    {
        [SerializeField] protected ConversionSettings settings = new ConversionSettings();
        [SerializeField] private float matchLength;
        [SerializeField] private GameObject avatarModel;
        [SerializeField] private AnimationClip lastResult;
        [SerializeField] private bool showOutputOptions = true;
        [SerializeField] private bool showLog = true;

        // Cached clip facts; GetCurveBindings on a large clip is too slow to run every repaint.
        [NonSerialized] private AnimationClip infoFor;
        [NonSerialized] private int infoBones, infoCurves;
        [NonSerialized] private bool infoIsGeneric;

        protected int ClipBones => infoBones;
        protected int ClipCurves => infoCurves;
        protected bool ClipIsGeneric => infoIsGeneric;

        // ------------------------------------------------------------------ hooks

        /// <summary>Called first every repaint: make sure settings.sourceClip is up to date.</summary>
        protected virtual void PrepareSource() { }

        /// <summary>Cards drawn above the source card (input selector, warnings, ...).</summary>
        protected virtual void DrawBeforeSource() { }

        /// <summary>Source fields at the top of the "Source &amp; naming" card. Set settings.sourceClip.</summary>
        protected abstract void DrawSourceInput();

        protected virtual string SourceCardTitle => L.T("Source & naming");
        protected virtual string MissingSourceHint => L.T("Pick a Generic source clip to start.");

        /// <summary>Extra conditions before Convert is allowed. Return a reason to block, or null.</summary>
        protected virtual string BlockReason => null;

        protected void ResetResult()
        {
            Log.Clear();
            lastResult = null;
        }

        // ------------------------------------------------------------------ layout

        public override void DrawGUI()
        {
            PrepareSource();
            RefreshClipInfo();

            DrawBeforeSource();
            DrawSourceCard();
            DrawTimingCard();
            DrawConvertCard();
            DrawAvatarCard();
            G.LogCard(Log, ref showLog);
        }

        private void DrawSourceCard()
        {
            G.BeginCard("AnimationClip Icon", SourceCardTitle);

            DrawSourceInput();

            EditorGUILayout.Space(6);
            G.NamingFields(ref settings.artist, ref settings.title);
            if (settings.sourceClip != null) G.OutputPaths(OutputLocation.PathFor(settings));

            EditorGUILayout.Space(4);
            showOutputOptions = EditorGUILayout.Foldout(showOutputOptions, L.T("Output options"), true);
            if (showOutputOptions)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    if (settings.sourceClip != null && settings.sampleRate <= 0) settings.sampleRate = settings.sourceClip.frameRate;
                    settings.sampleRate = EditorGUILayout.FloatField(L.T("Sample rate (fps)"), settings.sampleRate);
                    settings.includeFingers = EditorGUILayout.Toggle(L.T("Include fingers"), settings.includeFingers);
                    settings.bakeRootMotion = EditorGUILayout.Toggle(L.C("Bake root motion",
                        "Keeps the avatar in place (recommended for VRChat emotes)."), settings.bakeRootMotion);
                    settings.loop = EditorGUILayout.Toggle(L.T("Loop"), settings.loop);
                }
            }

            G.EndCard();
        }

        /// <summary>Length / fps / bones / curves chips plus a warning for non-Generic clips.</summary>
        protected void DrawClipFacts()
        {
            if (settings.sourceClip == null) return;
            G.ChipRow($"{settings.SourceLength:0.##}s", $"{settings.sourceClip.frameRate:0.##} fps", L.T("{0} bones", infoBones), L.T("{0} curves", infoCurves));
            if (!infoIsGeneric)
                EditorGUILayout.HelpBox(L.T("This clip has no Transform curves. It may already be Humanoid."), MessageType.Warning);
        }

        private void DrawTimingCard()
        {
            G.BeginCard("AudioSource Icon", L.T("Timing  ·  sync to audio"));

            settings.speed = EditorGUILayout.Slider(L.C("Speed",
                "1 = original. Above 1 plays faster (shorter clip), below 1 slower."),
                settings.speed, ConversionSettings.MinSpeed, ConversionSettings.MaxSpeed);
            settings.trimStart = Mathf.Max(0, EditorGUILayout.FloatField(L.C("Trim start (s)",
                "Skip this many seconds of the source motion (motion is late vs audio)."), settings.trimStart));
            settings.delayStart = Mathf.Max(0, EditorGUILayout.FloatField(L.C("Delay start (s)",
                "Hold the first pose this many seconds before moving (motion is early vs audio)."), settings.delayStart));

            using (new EditorGUILayout.HorizontalScope())
            {
                matchLength = EditorGUILayout.FloatField(L.C("Match length (s)",
                    "Enter the audio part's length and press Fit to work out the speed."), matchLength);
                using (new EditorGUI.DisabledScope(settings.sourceClip == null || matchLength <= settings.delayStart))
                {
                    if (GUILayout.Button(L.T("Fit speed"), EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                        settings.speed = settings.SpeedForLength(matchLength);
                }
                if (GUILayout.Button(L.T("Reset"), EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                {
                    settings.speed = 1f;
                    settings.trimStart = 0f;
                    settings.delayStart = 0f;
                    GUI.FocusControl(null);
                }
            }

            if (settings.sourceClip != null)
            {
                EditorGUILayout.Space(8);
                DrawTimeline();
            }

            G.EndCard();
        }

        private void DrawConvertCard()
        {
            G.BeginCard(null, null);

            string block = BlockReason;
            bool ready = settings.sourceClip != null && infoIsGeneric && block == null;
            if (G.BigButton(L.T("Convert to Humanoid"), ready)) Convert();

            if (!ready)
                EditorGUILayout.LabelField(block ?? MissingSourceHint, S.PathLabel);

            if (lastResult != null)
            {
                EditorGUILayout.Space(6);
                G.ResultRow(lastResult, Log.Entries.Any(e => e.level == LogLevel.Error));
            }

            G.EndCard();
        }

        private void DrawAvatarCard()
        {
            G.BeginCard("Avatar Icon", L.T("Avatar model"));

            EditorGUILayout.LabelField(
                L.T("Humanoid clips only play on a model imported as Rig → Humanoid. Drop the avatar FBX here to switch it and check the bone mapping."),
                S.PathLabel);
            EditorGUILayout.Space(2);

            using (new EditorGUILayout.HorizontalScope())
            {
                avatarModel = (GameObject)EditorGUILayout.ObjectField(L.T("Model FBX"), avatarModel, typeof(GameObject), false);
                using (new EditorGUI.DisabledScope(avatarModel == null))
                {
                    if (GUILayout.Button(L.T("Make Humanoid"), GUILayout.ExpandWidth(false)))
                    {
                        Log.Clear();
                        showLog = true;
                        ModelRigSetup.MakeHumanoid(avatarModel, Log);
                    }
                }
            }

            G.EndCard();
        }

        // ------------------------------------------------------------------ timeline

        /// <summary>Source bar on top, output bar below (hold + motion), both on the same seconds scale.</summary>
        private void DrawTimeline()
        {
            float src = settings.SourceLength, output = settings.OutputLength;
            float scale = Mathf.Max(src, output, 0.01f);

            var area = GUILayoutUtility.GetRect(1, 44, GUILayout.ExpandWidth(true));
            const float labelW = 52f;
            var track = new Rect(area.x + labelW, area.y, area.width - labelW - 44, area.height);

            Row(0, L.T("Source"), src);
            {
                var r = RowRect(0);
                EditorGUI.DrawRect(r, S.TrackBg);
                float trimW = r.width * (settings.trimStart / scale);
                EditorGUI.DrawRect(new Rect(r.x, r.y, r.width * (src / scale), r.height), S.Muted * new Color(1, 1, 1, 0.55f));
                if (trimW > 0) EditorGUI.DrawRect(new Rect(r.x, r.y, Mathf.Min(trimW, r.width), r.height), S.Error * new Color(1, 1, 1, 0.6f));
            }

            Row(1, L.T("Output"), output);
            {
                var r = RowRect(1);
                EditorGUI.DrawRect(r, S.TrackBg);
                float holdW = r.width * (settings.delayStart / scale);
                float motionW = r.width * (settings.UsedSourceLength / Mathf.Max(0.01f, settings.speed) / scale);
                if (holdW > 0) EditorGUI.DrawRect(new Rect(r.x, r.y, holdW, r.height), S.Warning * new Color(1, 1, 1, 0.7f));
                EditorGUI.DrawRect(new Rect(r.x + holdW, r.y, Mathf.Min(motionW, r.width - holdW), r.height), S.Accent);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(labelW);
                G.Legend(S.Accent, L.T("motion"));
                if (settings.delayStart > 0) G.Legend(S.Warning, L.T("hold"));
                if (settings.trimStart > 0) G.Legend(S.Error, L.T("trimmed"));
                GUILayout.FlexibleSpace();
                float diff = settings.OutputLength - settings.SourceLength;
                string speedText = Mathf.Abs(settings.speed - 1f) < 0.0005f ? L.T("original speed")
                    : settings.speed > 1f ? L.T("x{0:0.###} faster", settings.speed) : L.T("x{0:0.###} slower", settings.speed);
                GUILayout.Label(speedText + "  ·  " + L.T("{0}{1:0.##}s vs source", diff < 0 ? "−" : "+", Mathf.Abs(diff)), S.PathLabel);
            }

            Rect RowRect(int row) => new Rect(track.x, track.y + 4 + row * 20, track.width, 12);

            void Row(int row, string label, float seconds)
            {
                var y = track.y + 1 + row * 20;
                GUI.Label(new Rect(area.x, y, labelW, 18), label, EditorStyles.miniLabel);
                GUI.Label(new Rect(track.xMax + 6, y, 40, 18), $"{seconds:0.#}s", EditorStyles.miniLabel);
            }
        }

        // ------------------------------------------------------------------ actions

        private void Convert()
        {
            Log.Clear();
            showLog = true;
            try
            {
                lastResult = MotionConversion.Run(settings, Log);
                EditorGUIUtility.PingObject(lastResult);
            }
            catch (Exception e)
            {
                Log.Error(e.Message);
                Debug.LogException(e);
            }
            GUIUtility.ExitGUI();
        }

        private void RefreshClipInfo()
        {
            if (infoFor == settings.sourceClip) return;
            infoFor = settings.sourceClip;
            infoBones = infoCurves = 0;
            infoIsGeneric = false;
            if (infoFor == null) return;

            var bindings = AnimationUtility.GetCurveBindings(infoFor).Where(b => b.type == typeof(Transform)).ToArray();
            infoCurves = bindings.Length;
            infoBones = bindings.Select(b => b.path).Distinct().Count();
            infoIsGeneric = infoCurves > 0;
        }
    }
}
