using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace NekoSune.LipSync.Editor
{
    /// <summary>
    /// Update check in the style of lilToon: download a small version file, compare it with the
    /// installed version and tell the user when a newer one is out.
    ///
    /// Releasing an update: bump the same version in package.json, <see cref="CurrentVersion"/> and
    /// version.json, then push to the lipsync branch. <see cref="VersionUrl"/> reads version.json from
    /// that branch, so everyone on an older version sees the update banner and a Console message.
    /// </summary>
    [InitializeOnLoad]
    internal static class UpdateChecker
    {
        public const string CurrentVersion = "1.1.0";

        /// <summary>Raw URL of version.json, e.g. a file on GitHub (raw.githubusercontent.com).</summary>
        public const string VersionUrl = "https://raw.githubusercontent.com/NekoSuneProjects/vcc-packages/lipsync/version.json";

        /// <summary>Opened by "Download update" when version.json has no "url".</summary>
        public const string DefaultDownloadUrl = "https://nekosuneprojects.github.io/vcc-packages/";

        private const double AutoCheckIntervalHours = 6;
        private const int TimeoutSeconds = 15;

        private const string PrefCachedJson = "NekoSune.LipSync.Update.CachedJson";
        private const string PrefLastCheck = "NekoSune.LipSync.Update.LastCheckUtc";
        private const string PrefSkipped = "NekoSune.LipSync.Update.SkippedVersion";
        private const string PrefAutoCheck = "NekoSune.LipSync.Update.AutoCheck";
        private const string SessionLater = "NekoSune.LipSync.Update.Later";
        private const string SessionLogged = "NekoSune.LipSync.Update.Logged.";

        [Serializable]
        private class VersionInfo
        {
            // Filled by JsonUtility.
            public string version = null;
            public string url = null;
            public string notes = null;
        }

        public enum State { Unknown, Checking, UpToDate, UpdateAvailable, Failed }

        public static State Status { get; private set; }
        public static string LatestVersion { get; private set; }
        public static string Notes { get; private set; }
        public static string DownloadUrl { get; private set; } = DefaultDownloadUrl;

        /// <summary>Raised when the status changes, so open windows can repaint.</summary>
        public static event Action Changed;

        private static UnityWebRequest request;

        public static bool AutoCheck
        {
            get => EditorPrefs.GetBool(PrefAutoCheck, true);
            set => EditorPrefs.SetBool(PrefAutoCheck, value);
        }

        /// <summary>Banner shown: an update exists, it wasn't skipped and "Later" wasn't pressed this session.</summary>
        public static bool ShowBanner =>
            Status == State.UpdateAvailable &&
            EditorPrefs.GetString(PrefSkipped, "") != LatestVersion &&
            !SessionState.GetBool(SessionLater, false);

        static UpdateChecker()
        {
            // Show the last known result straight away, then refresh in the background if it's stale.
            var cached = EditorPrefs.GetString(PrefCachedJson, "");
            if (!string.IsNullOrEmpty(cached)) Apply(Parse(cached), log: false);

            if (AutoCheck && IsCheckDue()) EditorApplication.delayCall += () => Check();
        }

        public static void Check()
        {
            if (request != null || string.IsNullOrEmpty(VersionUrl)) return;

            request = UnityWebRequest.Get(VersionUrl);
            request.timeout = TimeoutSeconds;
            request.SendWebRequest();
            Status = State.Checking;
            EditorApplication.update += Poll;
            Changed?.Invoke();
        }

        public static void OpenDownload() => Application.OpenURL(DownloadUrl);

        public static void SkipThisVersion()
        {
            EditorPrefs.SetString(PrefSkipped, LatestVersion ?? "");
            Changed?.Invoke();
        }

        public static void RemindLater()
        {
            SessionState.SetBool(SessionLater, true);
            Changed?.Invoke();
        }

        private static void Poll()
        {
            if (request == null || !request.isDone) return;
            EditorApplication.update -= Poll;

            EditorPrefs.SetString(PrefLastCheck, DateTime.UtcNow.ToString("o"));
            var info = request.result == UnityWebRequest.Result.Success ? Parse(request.downloadHandler.text) : null;
            request.Dispose();
            request = null;

            if (info == null)
            {
                Status = State.Failed;
            }
            else
            {
                EditorPrefs.SetString(PrefCachedJson, JsonUtility.ToJson(info));
                Apply(info, log: true);
            }
            Changed?.Invoke();
        }

        private static void Apply(VersionInfo info, bool log)
        {
            if (info == null) return;

            LatestVersion = info.version.Trim().TrimStart('v', 'V');
            Notes = info.notes ?? "";
            DownloadUrl = string.IsNullOrEmpty(info.url) ? DefaultDownloadUrl : info.url;
            Status = Compare(LatestVersion, CurrentVersion) > 0 ? State.UpdateAvailable : State.UpToDate;

            // One Console message per new version per editor session.
            if (log && Status == State.UpdateAvailable && !SessionState.GetBool(SessionLogged + LatestVersion, false))
            {
                SessionState.SetBool(SessionLogged + LatestVersion, true);
                Debug.LogWarning("[NekoSune Lip Sync] " + NekoLoc.T("update.log", LatestVersion, CurrentVersion));
            }
        }

        private static VersionInfo Parse(string json)
        {
            try
            {
                var info = JsonUtility.FromJson<VersionInfo>(json);
                return info != null && !string.IsNullOrWhiteSpace(info.version) ? info : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool IsCheckDue()
        {
            var last = EditorPrefs.GetString(PrefLastCheck, "");
            return !DateTime.TryParse(last, null, System.Globalization.DateTimeStyles.RoundtripKind, out var time) ||
                   (DateTime.UtcNow - time).TotalHours >= AutoCheckIntervalHours;
        }

        /// <summary>Compares dotted versions numerically ("2.10.0" &gt; "2.9.1"). Missing parts count as 0.</summary>
        public static int Compare(string a, string b)
        {
            var pa = a.TrimStart('v', 'V').Split('.', '-', '+');
            var pb = b.TrimStart('v', 'V').Split('.', '-', '+');
            for (int i = 0; i < Math.Max(pa.Length, pb.Length); i++)
            {
                int x = i < pa.Length && int.TryParse(pa[i], out var px) ? px : 0;
                int y = i < pb.Length && int.TryParse(pb[i], out var py) ? py : 0;
                if (x != y) return x.CompareTo(y);
            }
            return 0;
        }
    }
}
