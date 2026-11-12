using UnityEditor;
using UnityEngine;
using S = NekoUnityEssentials.EssentialsStyles;
using G = NekoUnityEssentials.EssentialsGUI;

namespace NekoUnityEssentials
{
    /// <summary>
    /// The Unity Essentials window: header with version chip, a tab per tool (Organizer, Animation
    /// Toolkit, Animation Studio), the update banner and the NekoSuneVR footer.
    /// </summary>
    public class NekoEssentialsWindow : EditorWindow
    {
        internal enum Tab { Organizer, Toolkit, Studio, Frames }

        private const string MenuRoot = "Tools/NekoSuneVR/Unity Essentials/";
        private const string TabPref = "NekoSune.UnityEssentials.Tab";
        private const string HeaderIcon = "UnityEditor.SceneHierarchyWindow";

        [SerializeField] private Tab tab;
        [SerializeField] private NekoOrganizerPanel organizer = new NekoOrganizerPanel();
        [SerializeField] private NekoAnimationToolkit toolkit = new NekoAnimationToolkit();
        [SerializeField] private NekoAnimationStudio studio = new NekoAnimationStudio();
        [SerializeField] private AnimationFrameCalculator frames = new AnimationFrameCalculator();
        private Vector2 scroll;

        private static readonly (Tab tab, string icon, string label, string tip)[] Tabs =
        {
            (Tab.Organizer, "Folder Icon", "Organizer", "Organizer: Hierarchy and Project window colours, badges and icons"),
            (Tab.Toolkit, "AnimationClip Icon", "Toolkit", "Animation Toolkit: new clips, start → end transform keys, path repair"),
            (Tab.Studio, "AnimatorController Icon", "Studio", "Animation Studio: reverse, loop, simplify, toggles, blendshapes, controllers"),
            (Tab.Frames, "d_UnityEditor.AnimationWindow", "Frames", "Frame Calculator: recorder start/end frames for a clip at your recording FPS"),
        };

        [MenuItem(MenuRoot + "Organizer", false, 0)]
        public static void ShowWindow() => Open(Tab.Organizer);

        [MenuItem(MenuRoot + "Animation Toolkit", false, 1)]
        public static void ShowToolkit() => Open(Tab.Toolkit);

        [MenuItem(MenuRoot + "Animation Studio", false, 2)]
        public static void ShowStudio() => Open(Tab.Studio);

        [MenuItem(MenuRoot + "Frame Calculator", false, 3)]
        public static void ShowFrames() => Open(Tab.Frames);

        [MenuItem(MenuRoot + "Create Settings Asset", false, 20)]
        public static void CreateSettings()
        {
            var settings = NekoEssentialsSettings.CreateOrLoad();
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        internal static void Open(Tab which)
        {
            EditorPrefs.SetInt(TabPref, (int)which);
            var w = GetWindow<NekoEssentialsWindow>();
            w.titleContent = new GUIContent("Unity Essentials", EditorGUIUtility.IconContent(HeaderIcon).image);
            w.minSize = new Vector2(420f, 480f);
            w.tab = which;
            if (which == Tab.Frames) w.frames.OnEnable();
            w.Show();
            w.Focus();
        }

        private void OnEnable()
        {
            tab = (Tab)Mathf.Clamp(EditorPrefs.GetInt(TabPref, (int)tab), 0, Tabs.Length - 1);
            titleContent = new GUIContent("Unity Essentials", EditorGUIUtility.IconContent(HeaderIcon).image);
            UpdateChecker.Changed += Repaint;
            if (organizer == null) organizer = new NekoOrganizerPanel();
            if (toolkit == null) toolkit = new NekoAnimationToolkit();
            if (studio == null) studio = new NekoAnimationStudio();
            if (frames == null) frames = new AnimationFrameCalculator();
            studio.OnEnable();
            frames.OnEnable();
        }

        private void OnDisable() => UpdateChecker.Changed -= Repaint;

        private void OnGUI()
        {
            S.Ensure();
            // Some built-in icons are 64px; without this, layout sizes buttons/foldouts to the full texture.
            EditorGUIUtility.SetIconSize(new Vector2(16f, 16f));
            EditorGUIUtility.labelWidth = Mathf.Clamp(position.width * 0.38f, 140f, 220f);

            DrawHeader();
            DrawTabs();

            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.Space(4);
            DrawUpdateBanner();
            switch (tab)
            {
                case Tab.Organizer: organizer.OnGUI(this); break;
                case Tab.Toolkit: toolkit.OnGUI(this); break;
                case Tab.Studio: studio.OnGUI(this); break;
                case Tab.Frames: frames.OnGUI(this); break;
            }
            EditorGUILayout.Space(4);
            EditorGUILayout.EndScrollView();

            EditorGUIUtility.SetIconSize(Vector2.zero);
            NekoAboutFooter.DrawWithSeparator();
        }

        private void DrawHeader()
        {
            var rect = GUILayoutUtility.GetRect(1, 58, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, S.HeaderBg);

            var icon = EditorGUIUtility.IconContent(HeaderIcon).image;
            if (icon != null) GUI.DrawTexture(new Rect(rect.x + 14, rect.y + 13, 30, 30), icon, ScaleMode.ScaleToFit);

            GUI.Label(new Rect(rect.x + 54, rect.y + 9, rect.width - 190, 24), "Unity Essentials", S.Title);
            GUI.Label(new Rect(rect.x + 55, rect.y + 32, rect.width - 70, 16), "Hierarchy & folder styling plus non-destructive animation tools", S.Subtitle);

            DrawVersionChip(new Rect(rect.x, rect.y + 11, rect.width - 12, 18));
        }

        private void DrawTabs()
        {
            var bar = GUILayoutUtility.GetRect(1, 30, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(bar, S.HeaderBg);
            EditorGUI.DrawRect(new Rect(bar.x, bar.yMax - 1, bar.width, 1), S.CardBorder);

            // Labels drop to icons only when the window is too narrow for them.
            bool labels = bar.width >= 380f;
            float w = (bar.width - 16f) / Tabs.Length;
            for (int i = 0; i < Tabs.Length; i++)
            {
                var t = Tabs[i];
                var r = new Rect(bar.x + 8 + w * i, bar.y, w, bar.height - 1);
                bool active = tab == t.tab;
                var content = new GUIContent(labels ? " " + t.label : "", EditorGUIUtility.IconContent(t.icon).image, t.tip);
                if (GUI.Button(r, content, active ? S.TabActive : S.Tab) && !active)
                {
                    tab = t.tab;
                    EditorPrefs.SetInt(TabPref, (int)tab);
                    scroll = Vector2.zero;
                    GUI.FocusControl(null);
                }
                EditorGUIUtility.AddCursorRect(r, MouseCursor.Link);
                if (active) EditorGUI.DrawRect(new Rect(r.x + 6, r.yMax - 3, r.width - 12, 2), S.Accent);
            }
        }

        /// <summary>Right-aligned version chip; amber "v1.0.0 → v1.1.0" when an update is out. Click to check now.</summary>
        private static void DrawVersionChip(Rect area)
        {
            string current = "v" + UpdateChecker.CurrentVersion;
            string text = current, tip = "Click to check for updates";
            bool update = false;
            switch (UpdateChecker.Status)
            {
                case UpdateChecker.State.Checking:
                    text = current + "  ···";
                    tip = "Checking for updates…";
                    break;
                case UpdateChecker.State.UpdateAvailable:
                    update = true;
                    text = current + "  →  v" + UpdateChecker.LatestVersion;
                    tip = "Version " + UpdateChecker.LatestVersion + " is available.\nClick to check again.";
                    break;
                case UpdateChecker.State.UpToDate:
                    tip = "Up to date.\nClick to check again.";
                    break;
                case UpdateChecker.State.Failed:
                    tip = "Couldn't check for updates. Click to try again.";
                    break;
            }

            var content = new GUIContent(text, tip);
            var size = S.Chip.CalcSize(content);
            var r = new Rect(area.xMax - size.x, area.y, size.x, area.height);
            var style = S.Chip;
            if (update)
            {
                style = new GUIStyle(S.Chip) { fontStyle = FontStyle.Bold };
                style.normal.textColor = S.Warning;
            }
            EditorGUIUtility.AddCursorRect(r, MouseCursor.Link);
            if (GUI.Button(r, content, style)) UpdateChecker.Check();
        }

        private static void DrawUpdateBanner()
        {
            if (!UpdateChecker.ShowBanner) return;

            G.BeginCard("console.infoicon", "Unity Essentials v" + UpdateChecker.LatestVersion + " is available");
            EditorGUILayout.LabelField("You have v" + UpdateChecker.CurrentVersion + ". Update it in the VRChat Creator Companion.", EditorStyles.wordWrappedLabel);
            if (!string.IsNullOrEmpty(UpdateChecker.Notes))
                EditorGUILayout.LabelField(UpdateChecker.Notes, S.PathLabel);
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                var old = GUI.backgroundColor;
                GUI.backgroundColor = S.Warning;
                if (GUILayout.Button("Download update", GUILayout.Height(24))) UpdateChecker.OpenDownload();
                GUI.backgroundColor = old;
                if (GUILayout.Button("Later", GUILayout.Height(24), GUILayout.ExpandWidth(false))) UpdateChecker.RemindLater();
                if (GUILayout.Button("Skip this version", GUILayout.Height(24), GUILayout.ExpandWidth(false))) UpdateChecker.SkipThisVersion();
            }
            G.EndCard();
        }
    }
}
