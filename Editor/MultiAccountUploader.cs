#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using VRC.Core;
using VRC.SDK3A.Editor;
using VRC.SDKBase.Editor;
using VRC.SDKBase.Editor.Api;

namespace NekoSune.Tools
{
    /// <summary>
    /// Uploads one avatar to several accounts and platforms through the official SDK builder API
    /// (IVRCSdkAvatarBuilderApi.BuildAndUpload), so the SDK's build, validation, copyright agreement,
    /// permission and ownership checks run for every upload.
    ///
    /// Order: platform by platform (current platform first), and every account on each platform, so Unity
    /// switches platform as few times as possible. Switching platform reloads all scripts, so the run is kept
    /// in SessionState (memory only, survives reloads) and continues by itself after the reload.
    ///
    /// Per upload: switch session → confirm with VRChat it is the right account → put that account's
    /// Blueprint ID on the PipelineManager → BuildAndUpload → remember the ID VRChat assigned.
    /// At the end the original SDK login, Blueprint ID and platform are put back.
    /// </summary>
    [InitializeOnLoad]
    internal static class MultiAccountUploader
    {
        public enum StepState { Waiting, Running, Done, Failed, Skipped }

        [Serializable]
        public sealed class Step
        {
            public string userId = "";
            public string title = "";
            public string platform = "";
            public StepState state = StepState.Waiting;
            public string status = "Waiting";
            public float progress;
        }

        [Serializable]
        private sealed class RunState
        {
            public bool active;
            public bool cancelRequested;
            public bool cleanedUp;
            public string avatarGlobalId = "";
            public string avatarName = "";
            public string recordKey = "";
            public string originalBlueprintId = "";
            public string originalPlatform = "";
            public string waitingForPlatform = "";
            public int index;
            public List<Step> steps = new List<Step>();
            public List<string> log = new List<string>();

            // SDK login to put back at the end. Memory only (SessionState), never written to disk.
            public string origHuman = "", origProvider = "", origProviderUserId = "", origAuth = "", origTwoFactor = "";
        }

        private const string StateKey = "NekoSuneMultiPublisher.Run";
        private static RunState state;
        private static bool working;
        private static CancellationTokenSource cancel;
        private static Step current;
        private static double resumeAfter;

        public static event Action Changed;

        public static bool Running => state != null && state.active;
        public static IReadOnlyList<Step> Steps => state?.steps ?? new List<Step>();
        public static IReadOnlyList<string> Log => state?.log ?? new List<string>();

        // The SDK reports upload progress from a background thread. Anything that touches Unity (Repaint, GUI)
        // must happen on the main thread, so off-thread changes only set a flag that the main thread picks up.
        private static int mainThreadId;
        private static volatile bool changedPending;

        private static void NotifyChanged()
        {
            if (Thread.CurrentThread.ManagedThreadId == mainThreadId) Changed?.Invoke();
            else changedPending = true;
        }

        private static void FlushPendingChanges()
        {
            if (!changedPending) return;
            changedPending = false;
            Changed?.Invoke();
        }

        static MultiAccountUploader()
        {
            mainThreadId = Thread.CurrentThread.ManagedThreadId;
            EditorApplication.update += FlushPendingChanges;

            var json = SessionState.GetString(StateKey, "");
            if (string.IsNullOrEmpty(json)) return;
            try { state = JsonUtility.FromJson<RunState>(json); } catch { state = null; }

            if (state != null && state.active)
            {
                // Scripts were just reloaded (normally by our platform switch): carry on.
                var running = state.steps.ElementAtOrDefault(state.index);
                if (running != null && running.state == StepState.Running)
                {
                    // A reload happened in the middle of an upload (not by us). Don't guess, mark it.
                    running.state = StepState.Failed;
                    running.status = "Interrupted by a script reload. Run it again for this account.";
                    state.index++;
                }
                ScheduleResume(1.0);
            }
        }

        // ------------------------------------------------------------------
        // Public
        // ------------------------------------------------------------------

        public static void Start(GameObject avatar, PipelineManager pipeline, AvatarRecord record, List<SavedAccount> accounts, List<string> platforms)
        {
            if (Running) return;

            // Current platform first, then the others: fewest switches.
            var ordered = platforms.OrderBy(p => PublishPlatforms.IsCurrent(p) ? 0 : 1).ThenBy(p => Array.IndexOf(PublishPlatforms.All, p)).ToList();
            var original = VrcSession.Capture();

            state = new RunState
            {
                active = true,
                avatarGlobalId = GlobalObjectId.GetGlobalObjectIdSlow(avatar).ToString(),
                avatarName = avatar.name,
                recordKey = record.avatarKey,
                originalBlueprintId = pipeline.blueprintId ?? "",
                originalPlatform = PublishPlatforms.Current ?? "",
                origHuman = original.humanName,
                origProvider = original.provider,
                origProviderUserId = original.providerUserId,
                origAuth = original.auth,
                origTwoFactor = original.twoFactor
            };
            foreach (var p in ordered)
            foreach (var a in accounts)
                state.steps.Add(new Step { userId = a.userId, title = a.Title, platform = p });

            Write("Starting: \"" + avatar.name + "\" to " + accounts.Count + " account(s) on " +
                  string.Join(", ", ordered.Select(PublishPlatforms.Label)) + ".");
            Save();
            Continue();
        }

        public static void Cancel()
        {
            if (state == null || !state.active) return;
            state.cancelRequested = true;
            cancel?.Cancel();
            Write("Cancel requested: stopping at the next safe point, remaining uploads are skipped.");
            Save();
        }

        /// <summary>Clears a finished run's progress list.</summary>
        public static void Clear()
        {
            if (Running) return;
            state = null;
            SessionState.EraseString(StateKey);
            NotifyChanged();
        }

        // ------------------------------------------------------------------
        // Run loop
        // ------------------------------------------------------------------

        private static void ScheduleResume(double delaySeconds)
        {
            resumeAfter = EditorApplication.timeSinceStartup + delaySeconds;
            EditorApplication.update -= WaitThenResume;
            EditorApplication.update += WaitThenResume;
        }

        private static void WaitThenResume()
        {
            if (EditorApplication.timeSinceStartup < resumeAfter) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (state == null || !state.active) { EditorApplication.update -= WaitThenResume; return; }
            if (!string.IsNullOrEmpty(state.waitingForPlatform) && !PublishPlatforms.IsCurrent(state.waitingForPlatform)) return;

            EditorApplication.update -= WaitThenResume;
            Continue();
        }

        private static async void Continue()
        {
            if (working || state == null || !state.active) return;
            working = true;
            cancel = new CancellationTokenSource();
            state.waitingForPlatform = "";
            PublisherData.EnsureLoaded();

            try
            {
                if (!state.cleanedUp)
                {
                    bool switching = await RunSteps();
                    if (switching) return; // Unity is reloading for a platform switch; we continue afterwards.
                    await CleanUp();
                }

                // Last: back to the platform you started on.
                if (!string.IsNullOrEmpty(state.originalPlatform) && !PublishPlatforms.IsCurrent(state.originalPlatform))
                {
                    if (BeginPlatformSwitch(state.originalPlatform, "Switching back to " + PublishPlatforms.Label(state.originalPlatform) + "..."))
                        return;
                }

                int ok = state.steps.Count(s => s.state == StepState.Done);
                Write("Finished: " + ok + "/" + state.steps.Count + " uploaded.");
                state.active = false;
            }
            catch (Exception e)
            {
                Write("Stopped: " + e.Message);
                Debug.LogException(e);
                foreach (var s in state.steps.Where(s => s.state == StepState.Waiting || s.state == StepState.Running))
                {
                    s.state = StepState.Failed;
                    s.status = e.Message;
                }
                if (!state.cleanedUp) await CleanUp();
                state.active = false;
            }
            finally
            {
                working = false;
                cancel?.Dispose();
                cancel = null;
                current = null;
                Save();
            }
        }

        /// <summary>Runs uploads from state.index on. Returns true when it started a platform switch.</summary>
        private static async Task<bool> RunSteps()
        {
            var avatar = FindAvatar();
            var pipeline = avatar != null ? avatar.GetComponent<PipelineManager>() : null;
            var record = PublisherData.Avatars.FirstOrDefault(a => a.avatarKey == state.recordKey);
            if (avatar == null || pipeline == null || record == null)
            {
                foreach (var s in state.steps.Skip(state.index)) Fail(s, "Avatar not found in the open scene. Keep the same scene open while uploading.");
                state.index = state.steps.Count;
                return false;
            }

            IVRCSdkAvatarBuilderApi builder = null;
            try
            {
                while (state.index < state.steps.Count)
                {
                    var step = state.steps[state.index];

                    if (state.cancelRequested)
                    {
                        foreach (var s in state.steps.Skip(state.index).Where(s => s.state == StepState.Waiting))
                        {
                            s.state = StepState.Skipped;
                            s.status = "Skipped (cancelled)";
                        }
                        state.index = state.steps.Count;
                        break;
                    }

                    if (!PublishPlatforms.IsCurrent(step.platform))
                    {
                        if (!PublishPlatforms.Installed(step.platform))
                        {
                            foreach (var s in state.steps.Skip(state.index).Where(s => s.platform == step.platform))
                                Fail(s, PublishPlatforms.Label(step.platform) + " build support isn't installed. Add it in Unity Hub > Installs > Add modules.");
                            state.index++;
                            continue;
                        }
                        Save();
                        if (BeginPlatformSwitch(step.platform, "Switching to " + PublishPlatforms.Label(step.platform) + " (Unity reloads, then continues)..."))
                            return true;

                        foreach (var s in state.steps.Skip(state.index).Where(s => s.platform == step.platform))
                            Fail(s, "Unity could not switch to " + PublishPlatforms.Label(step.platform) + ".");
                        state.index++;
                        continue;
                    }

                    if (builder == null)
                    {
                        builder = await GetBuilder(cancel.Token);
                        if (builder == null) throw new Exception("Could not open the VRChat SDK builder. Open VRChat SDK > Show Control Panel once, then try again.");
                        builder.OnSdkBuildProgress += OnBuildProgress;
                        builder.OnSdkUploadProgress += OnUploadProgress;
                    }

                    current = step;
                    await UploadOne(step, avatar, pipeline, record, builder);
                    current = null;
                    state.index++;
                    Save();
                }
            }
            finally
            {
                if (builder != null)
                {
                    builder.OnSdkBuildProgress -= OnBuildProgress;
                    builder.OnSdkUploadProgress -= OnUploadProgress;
                }
            }
            return false;
        }

        private static bool BeginPlatformSwitch(string platform, string message)
        {
            Write(message);
            state.waitingForPlatform = platform;
            Save();
            if (!PublishPlatforms.SwitchTo(platform))
            {
                state.waitingForPlatform = "";
                return false;
            }
            // Normally the reload restarts us from the static constructor. This is the fallback in case no reload happens.
            ScheduleResume(30);
            return true;
        }

        private static async Task CleanUp()
        {
            var pipeline = FindAvatar()?.GetComponent<PipelineManager>();
            if (pipeline != null)
            {
                Undo.RecordObject(pipeline, "Restore Blueprint ID");
                pipeline.blueprintId = state.originalBlueprintId;
                EditorUtility.SetDirty(pipeline);
            }

            Write("Restoring your original SDK login...");
            var back = await VrcSession.Apply(new VrcSession.Snapshot
            {
                humanName = state.origHuman,
                provider = string.IsNullOrEmpty(state.origProvider) ? "vrchat" : state.origProvider,
                providerUserId = state.origProviderUserId,
                auth = state.origAuth,
                twoFactor = state.origTwoFactor
            });
            Write(back != null ? "SDK is logged in as " + back.displayName + " again." : "SDK is logged out (as before).");

            state.origAuth = state.origTwoFactor = ""; // no longer needed in memory
            state.cleanedUp = true;
            Save();
        }

        private static async Task UploadOne(Step step, GameObject avatar, PipelineManager pipeline, AvatarRecord record, IVRCSdkAvatarBuilderApi builder)
        {
            var account = PublisherData.Accounts.FirstOrDefault(a => a.userId == step.userId);
            var who = step.title + " [" + PublishPlatforms.Label(step.platform) + "]";
            step.state = StepState.Running;
            step.progress = 0;

            if (account == null) { Fail(step, "Account was removed."); return; }
            if (!account.HasSession) { Fail(step, "Not signed in. Sign in to this account again."); return; }

            Status(step, "Switching account...");
            var me = await VrcSession.Apply(account.ToSnapshot());
            account.lastCheckedUtc = DateTime.UtcNow.ToString("o");
            if (me == null)
            {
                account.sessionExpired = true;
                PublisherData.SaveAccounts();
                Fail(step, "Login expired. Sign in to this account again.");
                return;
            }
            if (me.id != account.userId) { Fail(step, "Saved login belongs to " + me.displayName + ", not " + account.displayName + ". Skipped for safety."); return; }
            if (!VrcSession.SdkUserIs(account.userId)) { Fail(step, "The SDK did not switch to this account (it shows " + (VrcSession.SdkUserName ?? "nobody") + "). Skipped for safety."); return; }

            await ContentInfo.LoadStyles();
            var thumbnail = ThumbnailPath(record);
            var id = record.GetId(account.userId);
            bool isNew = string.IsNullOrEmpty(id);
            VRCAvatar info;
            string thumb = null;

            if (isNew)
            {
                if (thumbnail == null) { Fail(step, "First upload to this account needs a thumbnail image."); return; }
                info = new VRCAvatar
                {
                    Name = string.IsNullOrWhiteSpace(record.name) ? avatar.name : record.name.Trim(),
                    Description = record.description ?? "",
                    Tags = record.ApiTags(),
                    ReleaseStatus = record.releaseStatus == "public" ? "public" : "private",
                    Styles = Styles(record.primaryStyle, record.secondaryStyle)
                };
                thumb = thumbnail;
                Write(who + ": creating new avatar \"" + info.Name + "\".");
            }
            else
            {
                try { info = await VRCApi.GetAvatar(id, true, cancel.Token); }
                catch (Exception e) { Fail(step, "Couldn't load " + id + " on this account (" + e.Message + "). Clear that ID to upload as a new avatar."); return; }
                if (info.AuthorId != account.userId) { Fail(step, id + " is not owned by this account. Fix or clear the ID for this account."); return; }

                if (record.applyInfoToExisting)
                {
                    if (!string.IsNullOrWhiteSpace(record.name)) info.Name = record.name.Trim();
                    info.Description = record.description ?? "";
                    info.ReleaseStatus = record.releaseStatus == "public" ? "public" : "private";
                    info.Styles = Styles(record.primaryStyle, record.secondaryStyle);
                    // Keep VRChat's own tags (fallback, admin, ...); replace warnings and your tags.
                    var keep = (info.Tags ?? new List<string>()).Where(t => !t.StartsWith("author_tag_") && !ContentInfo.WarningTags.Contains(t));
                    info.Tags = keep.Concat(record.ApiTags()).Distinct().ToList();
                }
                else
                {
                    // The API returns style names but expects IDs back.
                    info.Styles = Styles(info.Styles.Primary, info.Styles.Secondary);
                }
                if (record.updateThumbnailOnExisting && thumbnail != null) thumb = thumbnail;
                Write(who + ": updating " + id + ".");
            }

            Undo.RecordObject(pipeline, "Set Blueprint ID for " + account.displayName);
            pipeline.blueprintId = id;
            EditorUtility.SetDirty(pipeline);

            Status(step, "Building...");
            try
            {
                await builder.BuildAndUpload(avatar, info, thumb, cancel.Token);
            }
            catch (OperationCanceledException)
            {
                step.state = StepState.Skipped;
                Status(step, "Cancelled");
                return;
            }
            catch (BundleExistsException)
            {
                // Same build already on VRChat for this platform: nothing to do.
            }
            catch (Exception e)
            {
                Fail(step, Describe(e));
                return;
            }

            var newId = pipeline.blueprintId;
            if (string.IsNullOrEmpty(newId)) { Fail(step, "The SDK finished but no Blueprint ID was set. Check the SDK panel."); return; }
            record.SetId(account.userId, newId);
            PublisherData.SaveAvatars();

            // A new avatar is created without styles; set them now.
            if (isNew && (info.Styles.Primary != null || info.Styles.Secondary != null))
            {
                try
                {
                    var created = await VRCApi.GetAvatar(newId, true, cancel.Token);
                    created.Styles = info.Styles;
                    created.Tags = info.Tags;
                    await VRCApi.UpdateAvatarInfo(newId, created, cancel.Token);
                }
                catch (Exception e) { Write(who + ": uploaded, but setting the style failed (" + e.Message + ")."); }
            }

            step.state = StepState.Done;
            step.progress = 1;
            Status(step, "Uploaded  (" + newId + ")");
            Write(who + ": done.");
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static VRCAvatar.AvatarStyles Styles(string primary, string secondary)
        {
            var p = ContentInfo.StyleId(primary);
            var s = ContentInfo.StyleId(secondary);
            if (p == null && s != null) { p = s; s = null; }
            return new VRCAvatar.AvatarStyles { Primary = p, Secondary = s };
        }

        public static string ThumbnailPath(AvatarRecord record) =>
            record != null && !string.IsNullOrEmpty(record.thumbnailPath) && System.IO.File.Exists(record.thumbnailPath) ? record.thumbnailPath : null;

        private static GameObject FindAvatar()
        {
            if (state == null || !GlobalObjectId.TryParse(state.avatarGlobalId, out var gid)) return null;
            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid) as GameObject;
        }

        private static async Task<IVRCSdkAvatarBuilderApi> GetBuilder(CancellationToken token)
        {
            if (VRCSdkControlPanel.window == null) EditorApplication.ExecuteMenuItem("VRChat SDK/Show Control Panel");
            for (int i = 0; i < 60; i++)
            {
                if (VRCSdkControlPanel.window != null && VRCSdkControlPanel.TryGetBuilder<IVRCSdkAvatarBuilderApi>(out var builder))
                    return builder;
                await Task.Delay(500, token);
            }
            return null;
        }

        private static string Describe(Exception e)
        {
            switch (e)
            {
                case ValidationException v:
                    return "SDK validation failed: " + (v.Errors != null && v.Errors.Count > 0 ? string.Join("; ", v.Errors) : v.Message) + ". Fix it in the SDK panel.";
                case OwnershipException o: return "Ownership: " + o.Message;
                case BuildBlockedException b: return "Build blocked: " + b.Message;
                case UploadException u: return "Upload failed: " + u.Message;
                case BuilderException b: return "Build failed: " + b.Message;
                default: return e.Message;
            }
        }

        // These are called by the SDK in the middle of a build/upload, sometimes from a background thread.
        // They only store text/numbers and never throw: an exception here would make the SDK abort the upload.
        private static void OnBuildProgress(object sender, string message)
        {
            try
            {
                var step = current;
                if (step == null) return;
                step.status = "Building: " + message;
                NotifyChanged();
            }
            catch (Exception) { /* progress display only */ }
        }

        private static void OnUploadProgress(object sender, (string status, float percentage) p)
        {
            try
            {
                var step = current;
                if (step == null) return;
                step.status = "Uploading: " + p.status;
                step.progress = p.percentage < 0 ? 0 : p.percentage > 1 ? 1 : p.percentage;
                NotifyChanged();
            }
            catch (Exception) { /* progress display only */ }
        }

        private static void Status(Step step, string text)
        {
            step.status = text;
            NotifyChanged();
        }

        private static void Fail(Step step, string reason)
        {
            step.state = StepState.Failed;
            step.status = reason;
            Write(step.title + " [" + PublishPlatforms.Label(step.platform) + "]: " + reason);
        }

        private static void Write(string line)
        {
            if (state == null) return;
            state.log.Add(DateTime.Now.ToString("HH:mm:ss") + "  " + line);
            if (state.log.Count > 300) state.log.RemoveAt(0);
            Debug.Log("[Multi-Publisher] " + line);
            NotifyChanged();
        }

        private static void Save()
        {
            if (state != null) SessionState.SetString(StateKey, JsonUtility.ToJson(state));
            NotifyChanged();
        }
    }
}
#endif
