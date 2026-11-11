#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace NekoSune.Tools
{
    /// <summary>One VRChat account the tool can upload to.</summary>
    [Serializable]
    internal sealed class SavedAccount
    {
        public string userId = "";        // usr_... (the real identity, used for every check)
        public string displayName = "";
        public string loginName = "";     // what you type to sign in (username or email)
        public string label = "";         // your own nickname, e.g. "Main", "Alt"
        public bool enabled = true;       // included in "Upload to all"

        // SDK credential fields (same shape the VRChat SDK stores). Tokens are DPAPI-encrypted.
        public string humanName = "";
        public string provider = "vrchat";
        public string providerUserId = "";
        public string authProtected = "";
        public string twoFactorProtected = "";

        public string lastCheckedUtc = "";
        public bool sessionExpired;

        public string Title => string.IsNullOrWhiteSpace(label) ? displayName : label + "  (" + displayName + ")";
        public bool HasSession => !string.IsNullOrEmpty(authProtected) && !sessionExpired;

        public VrcSession.Snapshot ToSnapshot() => new VrcSession.Snapshot
        {
            humanName = humanName,
            provider = string.IsNullOrEmpty(provider) ? "vrchat" : provider,
            providerUserId = providerUserId,
            auth = TokenProtector.Unprotect(authProtected),
            twoFactor = TokenProtector.Unprotect(twoFactorProtected)
        };

        public void StoreSession(VrcSession.Snapshot s)
        {
            humanName = s.humanName;
            provider = string.IsNullOrEmpty(s.provider) ? "vrchat" : s.provider;
            providerUserId = s.providerUserId;
            authProtected = TokenProtector.Protect(s.auth);
            twoFactorProtected = TokenProtector.Protect(s.twoFactor);
            sessionExpired = false;
        }
    }

    /// <summary>Per-avatar settings: the Blueprint ID on each account plus info used for first-time uploads.</summary>
    [Serializable]
    internal sealed class AvatarRecord
    {
        [Serializable]
        public sealed class AccountId
        {
            public string userId = "";
            public string blueprintId = "";
        }

        public string avatarKey = "";
        public List<AccountId> ids = new List<AccountId>();

        // Same content info as the SDK builder panel.
        public string name = "";
        public string description = "";
        public string releaseStatus = "private";
        public string primaryStyle = "";              // style name, e.g. "Anime" ("" = Not Specified)
        public string secondaryStyle = "";
        public List<string> contentWarnings = new List<string>();   // content_sex, content_adult, ...
        public List<string> tags = new List<string>();              // your own tags, shown without the author_tag_ prefix
        public string thumbnailPath = "";             // absolute path to a PNG/JPG
        public string thumbnailGuid = "";             // v2.0 setting, converted to thumbnailPath on load

        // Platforms to upload to.
        public bool windows = true;
        public bool android;
        public bool ios;

        public bool applyInfoToExisting;
        public bool updateThumbnailOnExisting;

        public List<string> Platforms()
        {
            var list = new List<string>();
            if (windows) list.Add(PublishPlatforms.Windows);
            if (android) list.Add(PublishPlatforms.Android);
            if (ios) list.Add(PublishPlatforms.IOS);
            return list;
        }

        /// <summary>Tags in the form VRChat stores them: content warnings + author_tag_*.</summary>
        public List<string> ApiTags()
        {
            var list = new List<string>(contentWarnings.Where(c => !string.IsNullOrEmpty(c)));
            foreach (var t in tags)
            {
                if (string.IsNullOrWhiteSpace(t)) continue;
                var formatted = "author_tag_" + t.Trim().ToLowerInvariant().Replace(' ', '_');
                if (!list.Contains(formatted)) list.Add(formatted);
            }
            return list;
        }

        public string GetId(string userId) => ids.FirstOrDefault(i => i.userId == userId)?.blueprintId ?? "";

        public void SetId(string userId, string blueprintId)
        {
            var entry = ids.FirstOrDefault(i => i.userId == userId);
            if (entry == null) ids.Add(entry = new AccountId { userId = userId });
            entry.blueprintId = blueprintId ?? "";
        }
    }

    /// <summary>
    /// Storage:
    ///  - Accounts (with encrypted session tokens): %LOCALAPPDATA%/NekoSuneVR/MultiPublisher/accounts.json,
    ///    outside the project so they are never committed, shared or packaged by accident.
    ///  - Avatar Blueprint IDs: &lt;project&gt;/UserSettings/NekoSuneMultiPublisher/avatars.json (not secret; back it up).
    /// </summary>
    internal static class PublisherData
    {
        [Serializable] private sealed class AccountsFile { public int version = 2; public List<SavedAccount> accounts = new List<SavedAccount>(); }
        [Serializable] private sealed class AvatarsFile { public int version = 2; public List<AvatarRecord> avatars = new List<AvatarRecord>(); }

        // v1 format (main/alternate slots), read once for migration.
        [Serializable] private sealed class LegacySlot { public string label = ""; public string avatarId = ""; public string confirmedSdkUsername = ""; }
        [Serializable] private sealed class LegacyRecord { public string avatarKey = ""; public LegacySlot main = new LegacySlot(); public LegacySlot alternate = new LegacySlot(); }
        [Serializable] private sealed class LegacyStore { public LegacyRecord[] avatars = new LegacyRecord[0]; }

        public static List<SavedAccount> Accounts { get; private set; } = new List<SavedAccount>();
        public static List<AvatarRecord> Avatars { get; private set; } = new List<AvatarRecord>();
        private static LegacyStore legacy;

        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        private static string AccountsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NekoSuneVR", "MultiPublisher", "accounts.json");

        private static string AvatarsPath => Path.Combine(ProjectRoot, "UserSettings", "NekoSuneMultiPublisher", "avatars.json");

        private static string LegacyPath
        {
            get
            {
                var key = Hash128.Compute(ProjectRoot.Replace('\\', '/').ToLowerInvariant()).ToString();
                return Path.Combine(ProjectRoot, "UserSettings", "NekoSuneMultiPublisher-" + key + ".json");
            }
        }

        public static void Load()
        {
            Accounts = Read<AccountsFile>(AccountsPath)?.accounts ?? new List<SavedAccount>();
            Avatars = Read<AvatarsFile>(AvatarsPath)?.avatars ?? new List<AvatarRecord>();
            legacy = Read<LegacyStore>(LegacyPath);
            Loaded = true;

            foreach (var a in Avatars)
            {
                a.contentWarnings ??= new List<string>();
                a.tags ??= new List<string>();
                if (string.IsNullOrEmpty(a.thumbnailPath) && !string.IsNullOrEmpty(a.thumbnailGuid))
                {
                    var assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(a.thumbnailGuid);
                    if (!string.IsNullOrEmpty(assetPath)) a.thumbnailPath = Path.GetFullPath(assetPath);
                    a.thumbnailGuid = "";
                }
            }
        }

        public static bool Loaded { get; private set; }

        public static void EnsureLoaded()
        {
            if (!Loaded) Load();
        }

        public static string ThumbnailFolder => Path.Combine(ProjectRoot, "UserSettings", "NekoSuneMultiPublisher", "Thumbnails");

        public static void SaveAccounts() => Write(AccountsPath, new AccountsFile { accounts = Accounts });
        public static void SaveAvatars() => Write(AvatarsPath, new AvatarsFile { avatars = Avatars });

        /// <summary>Finds the record by scene/hierarchy key, or by any known Blueprint ID (survives renames).</summary>
        public static AvatarRecord FindOrCreate(string key, string liveBlueprintId, string defaultName)
        {
            var record = Avatars.FirstOrDefault(a => a.avatarKey == key);
            if (record == null && !string.IsNullOrEmpty(liveBlueprintId))
            {
                record = Avatars.FirstOrDefault(a => a.ids.Any(i => i.blueprintId == liveBlueprintId));
                if (record != null) record.avatarKey = key;
            }
            if (record == null)
            {
                record = new AvatarRecord { avatarKey = key, name = defaultName };
                Avatars.Add(record);
                MigrateLegacy(record);
                SaveAvatars();
            }
            return record;
        }

        /// <summary>Copies v1 Blueprint IDs onto accounts whose name matches the v1 "expected username".</summary>
        public static void MigrateLegacy(AvatarRecord record)
        {
            var old = legacy?.avatars?.FirstOrDefault(a => a.avatarKey == record.avatarKey);
            if (old == null) return;
            foreach (var slot in new[] { old.main, old.alternate })
            {
                if (slot == null || string.IsNullOrEmpty(slot.avatarId) || string.IsNullOrWhiteSpace(slot.confirmedSdkUsername)) continue;
                var who = slot.confirmedSdkUsername.Trim();
                var account = Accounts.FirstOrDefault(a =>
                    string.Equals(a.displayName, who, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(a.loginName, who, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(a.humanName, who, StringComparison.OrdinalIgnoreCase));
                if (account != null && string.IsNullOrEmpty(record.GetId(account.userId)))
                    record.SetId(account.userId, slot.avatarId);
            }
        }

        private static T Read<T>(string path) where T : class
        {
            try { return File.Exists(path) ? JsonUtility.FromJson<T>(File.ReadAllText(path)) : null; }
            catch (Exception e) { Debug.LogWarning("[Multi-Publisher] Could not read " + path + ": " + e.Message); return null; }
        }

        private static void Write(string path, object data)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                // Write to a temp file first so a crash never leaves a half-written file, then copy over.
                // (File.Replace fails on some PCs when antivirus/indexing has the file open.)
                var tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(data, true));
                File.Copy(tmp, path, true);
                File.Delete(tmp);
            }
            catch (Exception e) { Debug.LogError("[Multi-Publisher] Could not save " + path + ": " + e.Message); }
        }
    }
}
#endif
