using UnityEditor;
using UnityEngine;

namespace NekoSune.WorldAvatarSearch.Editor
{
    /// <summary>Update-checker chip and banner, drawn in the window's own NekoStyles look.</summary>
    internal static class NekoUpdateChrome
    {
        /// <summary>Version chip right of the header. Amber "v1.1.0 → v1.2.0" when an update is out; click to check now.</summary>
        public static void DrawVersionChip()
        {
            string current = "v" + UpdateChecker.CurrentVersion;
            string text = current, tip;
            bool update = UpdateChecker.Status == UpdateChecker.State.UpdateAvailable;
            switch (UpdateChecker.Status)
            {
                case UpdateChecker.State.Checking: text = current + "  ···"; tip = "Checking for updates..."; break;
                case UpdateChecker.State.UpdateAvailable:
                    text = current + "  →  v" + UpdateChecker.LatestVersion;
                    tip = "Update available: v" + UpdateChecker.LatestVersion + "\nClick to check for updates.";
                    break;
                case UpdateChecker.State.UpToDate: tip = "You're up to date.\nClick to check for updates."; break;
                case UpdateChecker.State.Failed: tip = "Couldn't check for updates (offline?). Click to try again."; break;
                default: tip = "Click to check for updates."; break;
            }

            var style = new GUIStyle(NekoStyles.Chip) { fontStyle = update ? FontStyle.Bold : FontStyle.Normal };
            if (update) style.normal.textColor = NekoStyles.Warn;

            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(false));
            GUILayout.Space(16f);
            var content = new GUIContent(text, tip);
            Rect r = GUILayoutUtility.GetRect(content, style, GUILayout.ExpandWidth(false));
            EditorGUIUtility.AddCursorRect(r, MouseCursor.Link);
            if (GUI.Button(r, content, style)) UpdateChecker.Check();
            EditorGUILayout.EndVertical();
            GUILayout.Space(8f);
        }

        /// <summary>Card shown at the top when a newer version is out.</summary>
        public static void DrawUpdateBanner()
        {
            if (!UpdateChecker.ShowBanner) return;

            EditorGUILayout.BeginVertical(NekoStyles.Card);
            GUILayout.Label("Update available: v" + UpdateChecker.LatestVersion, NekoStyles.SlotName);
            var bar = GUILayoutUtility.GetRect(1, 3, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(bar, NekoStyles.Warn);
            EditorGUILayout.LabelField("You have v" + UpdateChecker.CurrentVersion + ". Update to get the latest fixes and features.", NekoStyles.WrapLabel);
            if (!string.IsNullOrEmpty(UpdateChecker.Notes)) EditorGUILayout.LabelField(UpdateChecker.Notes, NekoStyles.WrapLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Download update", NekoStyles.PrimaryButton, GUILayout.Height(26f))) UpdateChecker.OpenDownload();
            if (GUILayout.Button("Later", GUILayout.Height(26f), GUILayout.ExpandWidth(false))) UpdateChecker.RemindLater();
            if (GUILayout.Button("Skip this version", GUILayout.Height(26f), GUILayout.ExpandWidth(false))) UpdateChecker.SkipThisVersion();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }
    }
}
