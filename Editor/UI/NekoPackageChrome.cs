#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using NekoSuneVR.VRChatIdleAudio.UI;

namespace NekoSuneVR.VRChatIdleAudio
{
    /// <summary>
    /// Header (with the live update badge) and update banner shared by the Hub, Intro and AFK windows,
    /// drawn in the existing NekoUI style.
    /// </summary>
    internal static class NekoPackageChrome
    {
        /// <summary>Gradient header whose version badge reflects the update check. Click it to check now.</summary>
        public static void Header(string title, string subtitle)
        {
            string current = "v" + UpdateChecker.CurrentVersion;
            string text = current, tip;
            bool update = UpdateChecker.Status == UpdateChecker.State.UpdateAvailable;

            switch (UpdateChecker.Status)
            {
                case UpdateChecker.State.Checking:
                    text = current + "  ···";
                    tip = "Checking for updates...";
                    break;
                case UpdateChecker.State.UpdateAvailable:
                    text = current + "  →  v" + UpdateChecker.LatestVersion;
                    tip = "Update available: v" + UpdateChecker.LatestVersion + "\nClick to check for updates.";
                    break;
                case UpdateChecker.State.UpToDate:
                    tip = "You're up to date.\nClick to check for updates.";
                    break;
                case UpdateChecker.State.Failed:
                    tip = "Couldn't check for updates (offline?). Click to try again.";
                    break;
                default:
                    tip = "Click to check for updates.";
                    break;
            }

            if (NekoUI.Header(title, subtitle, text, tip, update))
                UpdateChecker.Check();
        }

        /// <summary>Card shown under the header when a newer version is out.</summary>
        public static void UpdateBanner()
        {
            if (!UpdateChecker.ShowBanner) return;

            NekoUI.BeginCard("Update available: v" + UpdateChecker.LatestVersion);
            NekoUI.BeginCardBody();

            NekoUI.Notice(
                "You have v" + UpdateChecker.CurrentVersion + ". Update to get the latest fixes and features." +
                (string.IsNullOrEmpty(UpdateChecker.Notes) ? string.Empty : "\n" + UpdateChecker.Notes),
                NekoUI.Warn);

            GUILayout.Space(5f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (NekoUI.GhostButton("Download update", 140f, 26f)) UpdateChecker.OpenDownload();
                GUILayout.Space(5f);
                if (NekoUI.GhostButton("Later", 80f, 26f)) UpdateChecker.RemindLater();
                GUILayout.Space(5f);
                if (NekoUI.GhostButton("Skip this version", 130f, 26f)) UpdateChecker.SkipThisVersion();
                GUILayout.FlexibleSpace();
            }

            NekoUI.EndCardBody();
            NekoUI.EndCard();
        }
    }
}
#endif
