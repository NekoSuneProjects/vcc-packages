#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using VRC.Core;
using VRC.SDK3.Avatars.Components;
using Uploader = NekoSune.Tools.MultiAccountUploader;
using S = NekoSune.Tools.PublisherStyles;

namespace NekoSune.Tools
{
    /// <summary>
    /// Upload one avatar to several VRChat accounts with one click.
    /// Sign in to each account once; after that "Upload to all accounts" switches between them,
    /// verifies each login with VRChat and uploads through the official SDK builder.
    /// </summary>
    public sealed class NekoSuneMultiPublisher : EditorWindow
    {
        private const string MenuPath = "Tools/NekoSune/VRChat Multi-Account Publisher";
        private static readonly Regex AvatarIdPattern =
            new Regex(@"^avtr_[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$", RegexOptions.Compiled);

        private VRCAvatarDescriptor avatar;
        private Vector2 scroll, logScroll;
        private bool showLog = true;

        // Account sign-in state (password is only kept in memory until sign-in finishes).
        private bool addingAccount;
        private string loginName = "";
        private string password = "";
        private string twoFactorCode = "";
        private string pendingCodeType;
        private VrcSession.Snapshot sessionBeforeLogin;
        private string accountMessage = "";
        private MessageType accountMessageType = MessageType.None;
        private bool busy;

        private GUIStyle titleStyle, cardStyle, sectionStyle, mutedStyle, bigButton, descriptionStyle, noImageStyle;

        private Texture2D previewTexture;
        private string previewPath;
        private DateTime previewStamp;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            var w = GetWindow<NekoSuneMultiPublisher>();
            w.titleContent = new GUIContent("Multi-Publisher", EditorGUIUtility.IconContent("d_CloudConnect").image);
            w.minSize = new Vector2(440, 560);
        }

        private void OnEnable()
        {
            PublisherData.EnsureLoaded();
            ContentInfo.LoadStyles().ContinueWith(_ => Repaint(), System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
            Selection.selectionChanged += SelectionChanged;
            Uploader.Changed += Repaint;
            UpdateChecker.Changed += Repaint;
            SelectionChanged();
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= SelectionChanged;
            Uploader.Changed -= Repaint;
            UpdateChecker.Changed -= Repaint;
            password = "";
            if (previewTexture != null) DestroyImmediate(previewTexture);
            if (sessionBeforeLogin != null) CancelSignIn(); // never leave the SDK on a half-finished login
        }

        private void SelectionChanged()
        {
            if (Uploader.Running) return;
            var candidate = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<VRCAvatarDescriptor>() : null;
            if (candidate != null) avatar = candidate;
            Repaint();
        }

        // ------------------------------------------------------------------
        // GUI
        // ------------------------------------------------------------------

        private void OnGUI()
        {
            EnsureStyles();
            DrawHeader();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.Space(4);
            DrawUpdateBanner();

            using (new EditorGUI.DisabledScope(Uploader.Running))
            {
                DrawAccounts();
                DrawAvatar(out var pipeline, out var record);
                DrawPublish(pipeline, record);
            }
            DrawProgress();

            EditorGUILayout.Space(4);
            EditorGUILayout.EndScrollView();
            S.Line();
            PublisherAbout.Draw();
        }

        private void DrawHeader()
        {
            var rect = GUILayoutUtility.GetRect(1, 58, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, S.HeaderBg);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 2, rect.width, 2), S.Accent);

            var icon = EditorGUIUtility.IconContent("d_CloudConnect").image;
            if (icon != null) GUI.DrawTexture(new Rect(rect.x + 14, rect.y + 13, 30, 30), icon, ScaleMode.ScaleToFit);

            GUI.Label(new Rect(rect.x + 54, rect.y + 9, rect.width - 200, 24), "VRChat Multi-Publisher", S.Title);
            GUI.Label(new Rect(rect.x + 55, rect.y + 32, rect.width - 200, 16), "One avatar  →  all your VRChat accounts & platforms", S.Subtitle);

            DrawVersionChip(new Rect(rect.x, rect.y + 9, rect.width - 12, 18));

            // Live status under the version chip.
            string state = Uploader.Running ? "Uploading..." : busy ? "Signing in..." :
                PublisherData.Accounts.Count(a => a.HasSession) + "/" + PublisherData.Accounts.Count + " accounts ready";
            var st = new GUIContent(state);
            var stSize = S.Chip.CalcSize(st);
            var old = GUI.contentColor;
            if (Uploader.Running) GUI.contentColor = S.Accent;
            GUI.Label(new Rect(rect.xMax - stSize.x - 12, rect.y + 31, stSize.x, 18), st, S.Chip);
            GUI.contentColor = old;
        }

        /// <summary>
        /// Version chip, right-aligned in <paramref name="area"/>. Shows "v2.1.0 → v2.2.0" in amber when an update
        /// is out. Click it to check for updates now.
        /// </summary>
        private static void DrawVersionChip(Rect area)
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
        private void DrawUpdateBanner()
        {
            if (!UpdateChecker.ShowBanner) return;

            Card("Update available: v" + UpdateChecker.LatestVersion);
            var bar = GUILayoutUtility.GetRect(1, 3, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(bar, S.Warning);
            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField("You have v" + UpdateChecker.CurrentVersion + ". Update to get the latest fixes and features.", EditorStyles.wordWrappedLabel);
            if (!string.IsNullOrEmpty(UpdateChecker.Notes)) EditorGUILayout.LabelField(UpdateChecker.Notes, mutedStyle);

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                var oldColor = GUI.backgroundColor;
                GUI.backgroundColor = S.Warning;
                if (GUILayout.Button("Download update", GUILayout.Height(24))) UpdateChecker.OpenDownload();
                GUI.backgroundColor = oldColor;

                if (GUILayout.Button("Later", GUILayout.Height(24), GUILayout.ExpandWidth(false))) UpdateChecker.RemindLater();
                if (GUILayout.Button("Skip this version", GUILayout.Height(24), GUILayout.ExpandWidth(false))) UpdateChecker.SkipThisVersion();
            }
            EndCard();
        }

        private void DrawAccounts()
        {
            Card("Accounts");
            if (!TokenProtector.Available)
                EditorGUILayout.HelpBox("Saving logins needs Windows (they are encrypted with your Windows account). On this OS sign-ins are not kept.", MessageType.Warning);

            var accounts = PublisherData.Accounts;
            if (accounts.Count == 0)
                GUILayout.Label("No accounts yet. Sign in below. Your password is never saved; only the VRChat login session is, encrypted for your Windows user.", mutedStyle);

            for (int i = 0; i < accounts.Count; i++)
            {
                var a = accounts[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    a.enabled = EditorGUILayout.Toggle(a.enabled, GUILayout.Width(16));
                    a.label = EditorGUILayout.TextField(a.label, GUILayout.Width(90));
                    if (EditorGUI.EndChangeCheck()) PublisherData.SaveAccounts();

                    GUILayout.Label(new GUIContent(a.displayName, a.userId), GUILayout.MinWidth(80));
                    StatusChip(a.HasSession ? "Ready" : "Sign in again", a.HasSession ? S.Success : S.Warning, 84);

                    using (new EditorGUI.DisabledScope(busy))
                    {
                        if (GUILayout.Button(new GUIContent("Check", "Confirm with VRChat that this saved login still works."), EditorStyles.miniButtonLeft, GUILayout.Width(48)))
                            CheckAccount(a);
                        if (GUILayout.Button(new GUIContent("Re-login", "Sign in to this account again."), EditorStyles.miniButtonMid, GUILayout.Width(58)))
                        {
                            addingAccount = true;
                            loginName = a.loginName;
                        }
                        if (GUILayout.Button(new GUIContent("Remove", "Forget this account and its saved login on this PC."), EditorStyles.miniButtonRight, GUILayout.Width(56)) &&
                            EditorUtility.DisplayDialog("Remove account", "Forget " + a.Title + " and its saved login on this PC?\nBlueprint IDs stay saved.", "Remove", "Cancel"))
                        {
                            accounts.RemoveAt(i);
                            PublisherData.SaveAccounts();
                            GUIUtility.ExitGUI();
                        }
                    }
                }
            }

            GUILayout.Space(6);
            if (!addingAccount)
            {
                using (new EditorGUILayout.HorizontalScope())
                using (new EditorGUI.DisabledScope(busy))
                {
                    if (GUILayout.Button("+ Sign in to an account", GUILayout.Height(24))) { addingAccount = true; accountMessage = ""; }
                    if (GUILayout.Button(new GUIContent("Add SDK's current login", "Save the account the VRChat SDK control panel is logged into right now."), GUILayout.Height(24)))
                        ImportSdkLogin();
                }
            }
            else
            {
                DrawSignIn();
            }

            if (!string.IsNullOrEmpty(accountMessage)) EditorGUILayout.HelpBox(accountMessage, accountMessageType);
            EndCard();
        }

        private void DrawSignIn()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (pendingCodeType == null)
                {
                    using (new EditorGUI.DisabledScope(busy))
                    {
                        loginName = EditorGUILayout.TextField("Username / email", loginName);
                        password = EditorGUILayout.PasswordField("Password", password);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button(busy ? "Signing in..." : "Sign in", GUILayout.Height(22)) && !string.IsNullOrWhiteSpace(loginName) && !string.IsNullOrEmpty(password))
                                SignIn();
                            if (GUILayout.Button("Cancel", GUILayout.Width(70), GUILayout.Height(22)))
                            {
                                addingAccount = false;
                                password = "";
                            }
                        }
                    }
                }
                else
                {
                    GUILayout.Label(VrcSession.IsEmailCode(pendingCodeType)
                        ? "Enter the code VRChat emailed to you:"
                        : "Enter the 6-digit code from your authenticator app:", EditorStyles.wordWrappedLabel);
                    using (new EditorGUI.DisabledScope(busy))
                    {
                        twoFactorCode = EditorGUILayout.TextField("Code", twoFactorCode);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button(busy ? "Checking..." : "Verify", GUILayout.Height(22)) && twoFactorCode.Trim().Length >= 6)
                                SubmitCode();
                            if (GUILayout.Button("Cancel", GUILayout.Width(70), GUILayout.Height(22)))
                                CancelSignIn();
                        }
                    }
                }
            }
        }

        private void DrawAvatar(out PipelineManager pipeline, out AvatarRecord record)
        {
            pipeline = null;
            record = null;
            Card("Avatar");

            avatar = (VRCAvatarDescriptor)EditorGUILayout.ObjectField("Selected Avatar", avatar, typeof(VRCAvatarDescriptor), true);
            if (avatar == null)
            {
                EditorGUILayout.HelpBox("Select your avatar (the object with the VRC Avatar Descriptor) in the scene.", MessageType.Info);
                EndCard();
                return;
            }
            if (!avatar.gameObject.scene.IsValid() || EditorUtility.IsPersistent(avatar))
            {
                EditorGUILayout.HelpBox("Use the avatar in an open scene, not a prefab asset.", MessageType.Error);
                EndCard();
                return;
            }
            pipeline = avatar.GetComponent<PipelineManager>();
            if (pipeline == null)
            {
                EditorGUILayout.HelpBox("The avatar has no Pipeline Manager. Open it once in the VRChat SDK panel so the SDK adds one.", MessageType.Error);
                EndCard();
                return;
            }

            record = PublisherData.FindOrCreate(AvatarKey(), pipeline.blueprintId, avatar.name);
            EditorGUI.BeginChangeCheck();

            // --- Content info, laid out like the SDK's "Prepare Your Content" -------------------------
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    record.name = EditorGUILayout.TextField(new GUIContent("Name *", "Avatar name on VRChat."), record.name);

                    int vis = record.releaseStatus == "public" ? 1 : 0;
                    vis = EditorGUILayout.Popup("Visibility", vis, new[] { "Private", "Public" });
                    record.releaseStatus = vis == 1 ? "public" : "private";

                    record.primaryStyle = StylePopup("Primary Style", record.primaryStyle);
                    using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(record.primaryStyle)))
                        record.secondaryStyle = StylePopup("Secondary Style", record.secondaryStyle);
                    if (!ContentInfo.StylesLoaded)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Label("Styles load from VRChat (needs an SDK login).", mutedStyle);
                            if (GUILayout.Button("Retry", EditorStyles.miniButton, GUILayout.Width(50)))
                                ContentInfo.LoadStyles(true).ContinueWith(_ => Repaint(), System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
                        }
                    }

                    DrawContentWarnings(record);
                    DrawTags(record);
                }

                GUILayout.Space(8);
                DrawThumbnail(record);
            }

            GUILayout.Label("Description", EditorStyles.miniBoldLabel);
            record.description = EditorGUILayout.TextArea(record.description, descriptionStyle, GUILayout.MinHeight(48));

            GUILayout.Space(4);
            record.applyInfoToExisting = EditorGUILayout.ToggleLeft(
                new GUIContent("Also apply this info to avatars already uploaded", "Off: avatars that already exist keep the name, description, styles and tags they have on VRChat. New uploads always use this info."),
                record.applyInfoToExisting);
            record.updateThumbnailOnExisting = EditorGUILayout.ToggleLeft("Also replace the thumbnail on avatars already uploaded", record.updateThumbnailOnExisting);

            // --- Blueprint IDs -------------------------------------------------------------------------
            GUILayout.Space(6);
            GUILayout.Label("Blueprint ID on each account (empty = create a new avatar there)", EditorStyles.miniBoldLabel);
            foreach (var a in PublisherData.Accounts)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(a.Title, GUILayout.Width(150));
                    var id = record.GetId(a.userId);
                    var edited = EditorGUILayout.DelayedTextField(id).Trim();
                    if (edited != id)
                    {
                        if (edited.Length == 0 || AvatarIdPattern.IsMatch(edited)) record.SetId(a.userId, edited);
                        else accountMessage = "That doesn't look like an avatar ID (avtr_xxxxxxxx-xxxx-...).";
                    }
                    using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(id)))
                    {
                        if (GUILayout.Button(new GUIContent("Open", "Open this avatar on vrchat.com"), EditorStyles.miniButton, GUILayout.Width(44)))
                            Application.OpenURL("https://vrchat.com/home/avatar/" + id);
                    }
                }
            }
            GUILayout.Label("Live Pipeline Manager ID: " + (string.IsNullOrEmpty(pipeline.blueprintId) ? "(empty)" : pipeline.blueprintId), mutedStyle);

            if (EditorGUI.EndChangeCheck()) PublisherData.SaveAvatars();
            EndCard();
        }

        private string StylePopup(string label, string current)
        {
            var names = new[] { "Not Specified" }.Concat(ContentInfo.Styles.Select(s => s.StyleName)).ToList();
            if (!string.IsNullOrEmpty(current) && !names.Contains(current)) names.Add(current); // keep a saved value while styles load
            int index = string.IsNullOrEmpty(current) ? 0 : names.IndexOf(current);
            index = EditorGUILayout.Popup(label, index, names.ToArray());
            return index <= 0 ? "" : names[index];
        }

        private void DrawContentWarnings(AvatarRecord record)
        {
            int mask = 0;
            for (int i = 0; i < ContentInfo.WarningTags.Length; i++)
                if (record.contentWarnings.Contains(ContentInfo.WarningTags[i])) mask |= 1 << i;

            var labels = ContentInfo.WarningTags.Select(ContentInfo.WarningLabel).ToArray();
            int picked = EditorGUILayout.MaskField(new GUIContent("Content Warnings", record.contentWarnings.Count + "/5 selected"), mask, labels);
            if (picked == mask) return;

            record.contentWarnings.Clear();
            for (int i = 0; i < ContentInfo.WarningTags.Length; i++)
                if ((picked & (1 << i)) != 0) record.contentWarnings.Add(ContentInfo.WarningTags[i]);
            GUI.changed = true;
        }

        private void DrawTags(AvatarRecord record)
        {
            var text = string.Join(", ", record.tags);
            var edited = EditorGUILayout.DelayedTextField(new GUIContent("Tags", "Your own tags, separated by commas."), text);
            if (edited == text) return;
            record.tags = edited.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            GUI.changed = true;
        }

        private void DrawThumbnail(AvatarRecord record)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(170)))
            {
                var rect = GUILayoutUtility.GetRect(168, 126, GUILayout.Width(168), GUILayout.Height(126));
                EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.13f));
                var preview = ThumbnailPreview(record.thumbnailPath);
                if (preview != null) GUI.DrawTexture(rect, preview, ScaleMode.ScaleAndCrop);
                else GUI.Label(rect, "No Image", noImageStyle);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Select Image", GUILayout.Height(30)))
                    {
                        var start = string.IsNullOrEmpty(record.thumbnailPath) ? Application.dataPath : Path.GetDirectoryName(record.thumbnailPath);
                        var path = EditorUtility.OpenFilePanelWithFilters("Avatar thumbnail", start, new[] { "Images", "png,jpg,jpeg" });
                        if (!string.IsNullOrEmpty(path)) { record.thumbnailPath = path; GUI.changed = true; }
                    }
                    if (GUILayout.Button(new GUIContent("Capture\nIn Scene", "Take a 4:3 picture from the Scene view."), GUILayout.Height(30)))
                    {
                        var file = "thumb_" + Hash128.Compute(record.avatarKey);
                        var path = ContentInfo.CaptureSceneView(file);
                        if (path == null) accountMessage = "Open a Scene view and frame your avatar first.";
                        else { record.thumbnailPath = path; previewPath = null; GUI.changed = true; }
                    }
                }
            }
        }

        private Texture2D ThumbnailPreview(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            var stamp = File.GetLastWriteTimeUtc(path);
            if (previewTexture != null && previewPath == path && previewStamp == stamp) return previewTexture;

            if (previewTexture != null) DestroyImmediate(previewTexture);
            previewTexture = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
            if (!previewTexture.LoadImage(File.ReadAllBytes(path))) { DestroyImmediate(previewTexture); previewTexture = null; }
            previewPath = path;
            previewStamp = stamp;
            return previewTexture;
        }

        private void DrawPublish(PipelineManager pipeline, AvatarRecord record)
        {
            Card("Build & Upload");
            var targets = PublisherData.Accounts.Where(a => a.enabled).ToList();

            if (record != null)
            {
                EditorGUI.BeginChangeCheck();
                GUILayout.Label("Platform(s)", EditorStyles.miniBoldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    record.windows = PlatformToggle(PublishPlatforms.Windows, record.windows);
                    record.android = PlatformToggle(PublishPlatforms.Android, record.android);
                    record.ios = PlatformToggle(PublishPlatforms.IOS, record.ios);
                }
                if (EditorGUI.EndChangeCheck()) PublisherData.SaveAvatars();

                var missing = record.Platforms().Where(p => !PublishPlatforms.Installed(p)).ToList();
                if (missing.Count > 0)
                    EditorGUILayout.HelpBox(string.Join(", ", missing.Select(PublishPlatforms.Label)) +
                                            " build support isn't installed in this Unity. Add it in Unity Hub > Installs > (this version) > Add modules.", MessageType.Warning);
                if (record.Platforms().Any(p => !PublishPlatforms.IsCurrent(p)))
                    GUILayout.Label("Unity switches platform between uploads (that reloads scripts and can take a few minutes the first time). " +
                                    "The upload continues by itself and switches back to " + PublishPlatforms.Label(PublishPlatforms.Current ?? PublishPlatforms.Windows) + " at the end.", mutedStyle);
            }

            var platforms = record?.Platforms() ?? new System.Collections.Generic.List<string>();
            string block = null;
            if (pipeline == null) block = "Pick an avatar first.";
            else if (targets.Count == 0) block = "Tick at least one account.";
            else if (platforms.Count == 0) block = "Tick at least one platform.";
            else if (platforms.Any(p => !PublishPlatforms.Installed(p))) block = "Untick platforms that aren't installed, or install them.";
            else if (string.IsNullOrWhiteSpace(record.name)) block = "Give the avatar a name.";
            else if (EditorApplication.isPlayingOrWillChangePlaymode) block = "Exit Play Mode first.";
            else if (busy) block = "Finish signing in first.";
            else if (targets.Any(a => !a.HasSession)) block = "Sign in again to: " + string.Join(", ", targets.Where(a => !a.HasSession).Select(a => a.Title));
            else if (MultiAccountUploader.ThumbnailPath(record) == null && targets.Any(a => string.IsNullOrEmpty(record.GetId(a.userId))))
                block = "Set a thumbnail: it's needed to create the avatar on accounts that don't have it yet.";
            else if (platforms.Any(p => !PublishPlatforms.IsCurrent(p)) && string.IsNullOrEmpty(avatar.gameObject.scene.path))
                block = "Save the scene first (needed to switch platforms).";

            GUILayout.Space(4);
            GUILayout.Label("Each upload switches to the account, checks it with VRChat, then builds and uploads through the official VRChat SDK " +
                            "(same validation and copyright agreement as the SDK panel). Your SDK login is put back afterwards. " +
                            "Only upload avatars you have the rights to on every account.", mutedStyle);
            GUILayout.Space(4);

            using (new EditorGUI.DisabledScope(block != null))
            {
                int uploads = targets.Count * Math.Max(1, platforms.Count);
                var label = "Build & Publish: " + targets.Count + (targets.Count == 1 ? " account" : " accounts") +
                            (platforms.Count > 1 ? " × " + platforms.Count + " platforms (" + uploads + " uploads)" : "");
                var oldBg = GUI.backgroundColor;
                if (block == null) GUI.backgroundColor = S.Accent;
                bool go = GUILayout.Button(S.Icon("PlayButton", "  " + label), bigButton);
                GUI.backgroundColor = oldBg;
                if (go) StartUpload(pipeline, record, targets, platforms);
            }
            if (block != null) EditorGUILayout.HelpBox(block, MessageType.Info);
            EndCard();
        }

        private static bool PlatformToggle(string platform, bool value)
        {
            bool installed = PublishPlatforms.Installed(platform);
            string icon = platform == PublishPlatforms.Android ? "BuildSettings.Android.Small"
                : platform == PublishPlatforms.IOS ? "BuildSettings.iPhone.Small" : "BuildSettings.Standalone.Small";
            var content = S.Icon(icon, " " + PublishPlatforms.Label(platform) + (installed ? "" : "  (not installed)"),
                installed ? null : "Install it in Unity Hub > Installs > Add modules.");
            var old = GUI.backgroundColor;
            if (value) GUI.backgroundColor = installed ? S.Accent : S.Warning;
            bool result = GUILayout.Toggle(value, content, EditorStyles.miniButton, GUILayout.Height(24), GUILayout.MinWidth(110));
            GUI.backgroundColor = old;
            return result;
        }

        private void DrawProgress()
        {
            if (Uploader.Steps.Count == 0 && Uploader.Log.Count == 0) return;
            Card("Progress");

            foreach (var s in Uploader.Steps)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    string icon;
                    switch (s.state)
                    {
                        case MultiAccountUploader.StepState.Done: icon = "TestPassed"; break;
                        case MultiAccountUploader.StepState.Failed: icon = "TestFailed"; break;
                        case MultiAccountUploader.StepState.Running: icon = "d_WaitSpin00"; break;
                        case MultiAccountUploader.StepState.Skipped: icon = "TestIgnored"; break;
                        default: icon = "TestNormal"; break;
                    }
                    var bar = GUILayoutUtility.GetRect(3, 20, GUILayout.Width(3));
                    EditorGUI.DrawRect(bar, StepColor(s.state));
                    GUILayout.Label(EditorGUIUtility.IconContent(icon), GUILayout.Width(18), GUILayout.Height(18));
                    GUILayout.Label(s.title, EditorStyles.boldLabel, GUILayout.Width(120));
                    GUILayout.Label(PublishPlatforms.Label(s.platform), EditorStyles.miniLabel, GUILayout.Width(95));
                    GUILayout.Label(s.status, EditorStyles.wordWrappedMiniLabel);
                }
                if (s.state == MultiAccountUploader.StepState.Running && s.progress > 0)
                {
                    var r = GUILayoutUtility.GetRect(1, 14, GUILayout.ExpandWidth(true));
                    EditorGUI.ProgressBar(r, s.progress, Mathf.RoundToInt(s.progress * 100) + "%");
                }
            }

            GUILayout.Space(4);
            if (Uploader.Running)
            {
                if (GUILayout.Button("Cancel", GUILayout.Height(22))) Uploader.Cancel();
            }
            else if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(60)))
            {
                Uploader.Clear();
                GUIUtility.ExitGUI();
            }

            showLog = EditorGUILayout.Foldout(showLog, "Log (" + Uploader.Log.Count + ")", true);
            if (showLog)
            {
                logScroll = EditorGUILayout.BeginScrollView(logScroll, GUILayout.Height(140));
                foreach (var line in Uploader.Log) GUILayout.Label(line, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndScrollView();
                if (GUILayout.Button("Copy log", EditorStyles.miniButton, GUILayout.Width(80)))
                    EditorGUIUtility.systemCopyBuffer = string.Join("\n", Uploader.Log);
            }
            EndCard();
        }

        // ------------------------------------------------------------------
        // Actions
        // ------------------------------------------------------------------

        private void StartUpload(PipelineManager pipeline, AvatarRecord record, System.Collections.Generic.List<SavedAccount> targets, System.Collections.Generic.List<string> platforms)
        {
            var list = string.Join("\n", targets.Select(a => "•  " + a.Title + "  →  " +
                (string.IsNullOrEmpty(record.GetId(a.userId)) ? "NEW avatar" : record.GetId(a.userId))));
            var platformText = string.Join(", ", platforms.Select(PublishPlatforms.Label));
            if (!EditorUtility.DisplayDialog("Build & Publish",
                    "Build and upload \"" + record.name + "\" for " + platformText + " to:\n\n" + list +
                    "\n\nThis builds once per account and platform and can take a while.", "Upload", "Cancel"))
                return;

            if (platforms.Any(p => !PublishPlatforms.IsCurrent(p)) && avatar.gameObject.scene.isDirty &&
                !UnityEditor.SceneManagement.EditorSceneManager.SaveScene(avatar.gameObject.scene))
                return;

            Uploader.Start(avatar.gameObject, pipeline, record, targets, platforms);
            Repaint();
        }

        private async void SignIn()
        {
            busy = true;
            accountMessage = "";
            sessionBeforeLogin = VrcSession.Capture();
            try
            {
                var result = await VrcSession.Login(loginName.Trim(), password, fresh: true);
                await HandleLoginResult(result);
            }
            catch (Exception e)
            {
                await EndSignIn("Sign-in failed: " + e.Message, MessageType.Error);
            }
            busy = false;
            Repaint();
        }

        private async void SubmitCode()
        {
            busy = true;
            try
            {
                if (!await VrcSession.VerifyCode(twoFactorCode, pendingCodeType, loginName.Trim(), password))
                {
                    accountMessage = "That code didn't work. Try again (codes change every 30 seconds).";
                    accountMessageType = MessageType.Warning;
                }
                else
                {
                    var result = await VrcSession.Login(loginName.Trim(), password, fresh: false);
                    await HandleLoginResult(result);
                }
            }
            catch (Exception e)
            {
                await EndSignIn("Sign-in failed: " + e.Message, MessageType.Error);
            }
            busy = false;
            Repaint();
        }

        private async System.Threading.Tasks.Task HandleLoginResult(VrcSession.LoginResult result)
        {
            switch (result.state)
            {
                case VrcSession.LoginState.NeedsCode:
                    pendingCodeType = result.codeType;
                    twoFactorCode = "";
                    accountMessage = "";
                    return;

                case VrcSession.LoginState.Failed:
                    await EndSignIn(result.error ?? "Sign-in failed.", MessageType.Error);
                    return;
            }

            // Confirm the new session with VRChat before saving it.
            var me = await VrcSession.Apply(result.session);
            if (me == null)
            {
                await EndSignIn("Signed in, but VRChat didn't accept the session. Try again.", MessageType.Error);
                return;
            }
            var account = SaveAccount(me, result.session, loginName.Trim());
            await EndSignIn("Saved " + account.Title + ". Your SDK login was put back.", MessageType.Info);
        }

        private async void CancelSignIn()
        {
            await EndSignIn("", MessageType.None);
            Repaint();
        }

        /// <summary>Clears the sign-in form and puts back whatever login the SDK had before.</summary>
        private async System.Threading.Tasks.Task EndSignIn(string message, MessageType type)
        {
            password = "";
            twoFactorCode = "";
            pendingCodeType = null;
            addingAccount = false;
            accountMessage = message;
            accountMessageType = type;
            var restore = sessionBeforeLogin;
            sessionBeforeLogin = null;
            if (restore != null) await VrcSession.Apply(restore);
        }

        private async void ImportSdkLogin()
        {
            busy = true;
            try
            {
                var snap = VrcSession.Capture();
                if (!snap.HasToken)
                {
                    accountMessage = "The SDK isn't logged in. Log in through VRChat SDK > Show Control Panel first, or use \"Sign in to an account\".";
                    accountMessageType = MessageType.Warning;
                }
                else
                {
                    var me = await VrcSession.Apply(snap);
                    if (me == null)
                    {
                        accountMessage = "The SDK's login has expired. Log in again in the SDK panel.";
                        accountMessageType = MessageType.Warning;
                    }
                    else
                    {
                        var a = SaveAccount(me, snap, snap.providerUserId);
                        accountMessage = "Saved " + a.Title + ".";
                        accountMessageType = MessageType.Info;
                    }
                }
            }
            catch (Exception e)
            {
                accountMessage = e.Message;
                accountMessageType = MessageType.Error;
            }
            busy = false;
            Repaint();
        }

        private async void CheckAccount(SavedAccount account)
        {
            busy = true;
            var before = VrcSession.Capture();
            try
            {
                var me = await VrcSession.Apply(account.ToSnapshot());
                account.lastCheckedUtc = DateTime.UtcNow.ToString("o");
                if (me == null)
                {
                    account.sessionExpired = true;
                    accountMessage = account.Title + ": login expired, sign in again.";
                    accountMessageType = MessageType.Warning;
                }
                else if (me.id != account.userId)
                {
                    account.sessionExpired = true;
                    accountMessage = account.Title + ": saved login belongs to " + me.displayName + ". Sign in again.";
                    accountMessageType = MessageType.Error;
                }
                else
                {
                    account.sessionExpired = false;
                    account.displayName = me.displayName;
                    accountMessage = account.Title + ": login works.";
                    accountMessageType = MessageType.Info;
                }
                PublisherData.SaveAccounts();
            }
            finally
            {
                await VrcSession.Apply(before);
                busy = false;
                Repaint();
            }
        }

        private static SavedAccount SaveAccount(VrcSession.Identity me, VrcSession.Snapshot session, string login)
        {
            var accounts = PublisherData.Accounts;
            var account = accounts.FirstOrDefault(a => a.userId == me.id);
            if (account == null)
            {
                account = new SavedAccount { userId = me.id, label = accounts.Count == 0 ? "Main" : "Alt " + accounts.Count };
                accounts.Add(account);
            }
            account.displayName = me.displayName;
            if (!string.IsNullOrWhiteSpace(login)) account.loginName = login;
            account.StoreSession(session);
            account.lastCheckedUtc = DateTime.UtcNow.ToString("o");
            PublisherData.SaveAccounts();

            // Pick up Blueprint IDs from the old two-slot version of this tool.
            foreach (var record in PublisherData.Avatars) PublisherData.MigrateLegacy(record);
            PublisherData.SaveAvatars();
            return account;
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private string AvatarKey()
        {
            var t = avatar.transform;
            var path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return avatar.gameObject.scene.path + "::" + path;
        }

        private void EnsureStyles()
        {
            S.Ensure(); // rebuilt automatically after a skin change
            titleStyle = S.Title;
            sectionStyle = S.CardTitle;
            mutedStyle = S.PathLabel;
            cardStyle = S.Card;
            bigButton = S.BigButton;
            if (descriptionStyle == null) descriptionStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
            if (noImageStyle == null)
            {
                noImageStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 14 };
                noImageStyle.normal.textColor = new Color(0.6f, 0.6f, 0.62f);
            }
        }

        private static readonly System.Collections.Generic.Dictionary<string, string> CardIcons =
            new System.Collections.Generic.Dictionary<string, string>
            {
                { "Accounts", "d_UnityEditor.HierarchyWindow" },
                { "Avatar", "Avatar Icon" },
                { "Build & Upload", "BuildSettings.Standalone.Small" },
                { "Progress", "d_UnityEditor.ConsoleWindow" }
            };

        /// <summary>Card with icon, title and divider (same as Motion Converter's cards).</summary>
        private void Card(string title)
        {
            EditorGUILayout.BeginVertical(cardStyle);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (CardIcons.TryGetValue(title, out var icon))
                    GUILayout.Label(EditorGUIUtility.IconContent(icon), GUILayout.Width(18), GUILayout.Height(18));
                GUILayout.Label(title, sectionStyle);
            }
            S.Line();
            EditorGUILayout.Space(6);
        }

        private static void StatusChip(string text, Color color, float width)
        {
            var old = GUI.contentColor;
            GUI.contentColor = color;
            GUILayout.Label(text, S.Chip, GUILayout.Width(width), GUILayout.Height(18));
            GUI.contentColor = old;
        }

        private static Color StepColor(MultiAccountUploader.StepState state)
        {
            switch (state)
            {
                case MultiAccountUploader.StepState.Done: return S.Success;
                case MultiAccountUploader.StepState.Failed: return S.Error;
                case MultiAccountUploader.StepState.Running: return S.Accent;
                case MultiAccountUploader.StepState.Skipped: return S.Warning;
                default: return S.CardBorder;
            }
        }

        private static void EndCard() => EditorGUILayout.EndVertical();
    }
}
#endif
