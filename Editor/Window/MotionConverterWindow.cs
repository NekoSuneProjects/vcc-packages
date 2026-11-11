using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using S = MotionConverter.MotionConverterStyles;
using G = MotionConverter.MotionConverterGUI;

namespace MotionConverter
{
    /// <summary>
    /// Window shell only: header, tab bar and footer. Each tab is a <see cref="MotionSourceModule"/>,
    /// found automatically, so adding a source never needs changes here.
    /// </summary>
    public class MotionConverterWindow : EditorWindow
    {
        [SerializeReference] private List<MotionSourceModule> modules = new List<MotionSourceModule>();
        [SerializeField] private string selectedId;

        private Vector2 scroll;

        [MenuItem("Tools/Motion Converter")]
        private static void Open()
        {
            var window = GetWindow<MotionConverterWindow>();
            window.titleContent = new GUIContent(L.T("Motion Converter"), EditorGUIUtility.IconContent("AnimationClip Icon").image);
            window.minSize = new Vector2(380, 520);
        }

        private void OnEnable()
        {
            MotionSourceModules.Sync(modules);
            UpdateChecker.Changed += Repaint;
        }

        private void OnDisable()
        {
            UpdateChecker.Changed -= Repaint;
            foreach (var m in modules) m?.OnDisable();
        }

        private void OnGUI()
        {
            S.Ensure();
            if (modules.Count == 0 || modules.Any(m => m == null)) MotionSourceModules.Sync(modules);

            var current = modules.FirstOrDefault(m => m.Id == selectedId) ?? modules.FirstOrDefault();

            DrawHeader(current);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.Space(4);
            DrawUpdateBanner();

            if (current == null)
            {
                EditorGUILayout.HelpBox(L.T("No conversion modules found."), MessageType.Error);
            }
            else
            {
                DrawModuleSelector(current);
                current.DrawGUI();
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.EndScrollView();
            DrawFooter();
        }

        private void DrawHeader(MotionSourceModule current)
        {
            var rect = GUILayoutUtility.GetRect(1, 58, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, S.HeaderBg);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 2, rect.width, 2), S.Accent);

            var icon = EditorGUIUtility.IconContent("AnimationClip Icon").image;
            if (icon != null) GUI.DrawTexture(new Rect(rect.x + 14, rect.y + 13, 30, 30), icon, ScaleMode.ScaleToFit);

            GUI.Label(new Rect(rect.x + 54, rect.y + 9, rect.width - 200, 24), L.T("Motion Converter"), S.Title);
            GUI.Label(new Rect(rect.x + 55, rect.y + 32, rect.width - 200, 16), current != null ? current.Subtitle : "", S.Subtitle);

            DrawVersionChip(new Rect(rect.x, rect.y + 9, rect.width - 12, 18));

            // Language picker under the version chip.
            const float langW = 130f;
            var langRect = new Rect(rect.xMax - langW - 12, rect.y + 31, langW, 18);
            if (L.Popup(langRect))
            {
                titleContent.text = L.T("Motion Converter");
                Repaint();
            }
        }

        /// <summary>
        /// Version chip, right-aligned in <paramref name="area"/>. Shows "v2.0.0 → v2.1.0" in orange when an update
        /// is out (like lilToon's red "[Update]" label). Click it to check for updates now.
        /// </summary>
        private static void DrawVersionChip(Rect area)
        {
            string current = "v" + UpdateChecker.CurrentVersion;
            string text, tip;
            bool update = UpdateChecker.Status == UpdateChecker.State.UpdateAvailable;
            switch (UpdateChecker.Status)
            {
                case UpdateChecker.State.Checking:
                    text = current + "  ···";
                    tip = L.T("Checking for updates...");
                    break;
                case UpdateChecker.State.UpdateAvailable:
                    text = current + "  →  v" + UpdateChecker.LatestVersion;
                    tip = L.T("Update available: v{0}", UpdateChecker.LatestVersion) + "\n" + L.T("Click to check for updates.");
                    break;
                case UpdateChecker.State.UpToDate:
                    text = current;
                    tip = L.T("You're up to date.") + "\n" + L.T("Click to check for updates.");
                    break;
                case UpdateChecker.State.Failed:
                    text = current;
                    tip = L.T("Couldn't check for updates (offline?). Click to try again.");
                    break;
                default:
                    text = current;
                    tip = L.T("Click to check for updates.");
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

        /// <summary>Card shown at the top when a newer version is out.</summary>
        private static void DrawUpdateBanner()
        {
            if (!UpdateChecker.ShowBanner) return;

            G.BeginCard("console.infoicon", L.T("Update available: v{0}", UpdateChecker.LatestVersion));

            var bar = GUILayoutUtility.GetRect(1, 3, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(bar, S.Warning);
            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField(
                L.T("You have v{0}. Update to get the latest fixes and features.", UpdateChecker.CurrentVersion),
                EditorStyles.wordWrappedLabel);
            if (!string.IsNullOrEmpty(UpdateChecker.Notes))
                EditorGUILayout.LabelField(UpdateChecker.Notes, S.PathLabel);

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                var oldColor = GUI.backgroundColor;
                GUI.backgroundColor = S.Warning;
                if (GUILayout.Button(L.T("Download update"), GUILayout.Height(24))) UpdateChecker.OpenDownload();
                GUI.backgroundColor = oldColor;

                if (GUILayout.Button(L.T("Later"), GUILayout.Height(24), GUILayout.ExpandWidth(false))) UpdateChecker.RemindLater();
                if (GUILayout.Button(L.T("Skip this version"), GUILayout.Height(24), GUILayout.ExpandWidth(false))) UpdateChecker.SkipThisVersion();
            }
            G.EndCard();
        }

        private void DrawModuleSelector(MotionSourceModule current)
        {
            G.BeginCard("AnimationClip Icon", L.T("Animation source"));
            int index = modules.IndexOf(current);
            int picked = GUILayout.Toolbar(index, modules.Select(m => m.TabName).ToArray(), GUILayout.Height(27));
            if (picked != index && picked >= 0)
            {
                selectedId = modules[picked].Id;
                GUI.FocusControl(null);
            }
            EditorGUILayout.LabelField(modules[picked >= 0 ? picked : index].Description, S.PathLabel);
            G.EndCard();
        }

        private static void DrawFooter()
        {
            S.Line();
            MotionConverterAbout.Draw();
        }
    }
}
