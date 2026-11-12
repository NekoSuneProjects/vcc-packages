using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDK3.Components.Video;
using VRC.SDK3.Video.Components.Base;
using VRC.SDKBase;

namespace NekoSune.WorldUiAssistant
{
    /// <summary>
    /// Controls for a stock VRChat video player (AVPro or Unity Video): play/pause, stop, seek, time,
    /// volume, URL field, loop, resync and a playlist. Synced: the owner's URL, play state and time are
    /// shared, so everyone watches together (late joiners too).
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class NekoVideoPanel : UdonSharpBehaviour
    {
        public BaseVRCVideoPlayer player;
        [Tooltip("The video's speakers; the volume slider sets them.")]
        public AudioSource[] speakers;
        public VRCUrlInputField urlField;
        public Slider seekBar;
        public Slider volumeBar;
        public TextMeshProUGUI timeText;
        public TextMeshProUGUI statusText;
        public TextMeshProUGUI titleText;
        public Toggle loopToggle;

        [Tooltip("Playlist: videos must be typed in here in the editor (VRCUrl).")]
        public VRCUrl[] playlist;
        public string[] titles;
        [Tooltip("Optional thumbnail per playlist entry.")]
        public Sprite[] thumbnails;
        [Tooltip("Playlist buttons (Play0 … Play7); unused ones are hidden.")]
        public GameObject[] playlistButtons;
        public TextMeshProUGUI[] playlistLabels;
        public Image[] playlistThumbs;

        [Tooltip("Everyone watches the same video at the same time.")]
        public bool synced = true;
        [Tooltip("Only the instance master (or whoever owns the player) can change the video.")]
        public bool masterOnly;

        [UdonSynced] private VRCUrl syncedUrl = VRCUrl.Empty;
        [UdonSynced] private bool syncedPlaying;
        [UdonSynced] private bool syncedLoop;
        [UdonSynced] private int syncedIndex = -1;
        [UdonSynced] private double syncedStart;   // server time the video was at 0s
        [UdonSynced] private float syncedPausedAt;

        private VRCUrl loadedUrl = VRCUrl.Empty;
        private bool updatingUi;
        private float nextDriftCheck;

        private void Start()
        {
            if (volumeBar != null) VolumeChanged();
            SetStatus("Ready");
            if (playlistButtons == null) return;
            for (int i = 0; i < playlistButtons.Length; i++)
            {
                bool used = i < Count() && playlist[i] != null && !string.IsNullOrEmpty(playlist[i].Get());
                if (playlistButtons[i] != null) playlistButtons[i].SetActive(used);
                if (!used) continue;
                if (playlistLabels != null && i < playlistLabels.Length && playlistLabels[i] != null)
                    playlistLabels[i].text = titles != null && i < titles.Length && !string.IsNullOrEmpty(titles[i]) ? titles[i] : "Video " + (i + 1);
                bool thumb = thumbnails != null && i < thumbnails.Length && thumbnails[i] != null;
                if (playlistThumbs != null && i < playlistThumbs.Length && playlistThumbs[i] != null)
                {
                    playlistThumbs[i].gameObject.SetActive(thumb);
                    if (thumb) playlistThumbs[i].sprite = thumbnails[i];
                }
            }
        }

        // ------------------------------------------------------------------ buttons

        public void PlayFromField()
        {
            if (!CanControl() || urlField == null) return;
            var url = urlField.GetUrl();
            if (url == null || string.IsNullOrEmpty(url.Get())) return;
            StartVideo(url, -1);
            urlField.SetUrl(VRCUrl.Empty);
        }

        public void TogglePause()
        {
            if (!CanControl() || player == null) return;
            if (player.IsPlaying)
            {
                player.Pause();
                syncedPlaying = false;
                syncedPausedAt = player.GetTime();
            }
            else
            {
                player.Play();
                syncedPlaying = true;
                syncedStart = Networking.GetServerTimeInSeconds() - player.GetTime();
            }
            Share();
        }

        public void StopVideo()
        {
            if (!CanControl() || player == null) return;
            player.Stop();
            syncedPlaying = false;
            syncedUrl = VRCUrl.Empty;
            loadedUrl = VRCUrl.Empty;
            Share();
            SetStatus("Stopped");
        }

        public void ToggleLoop()
        {
            if (!CanControl() || player == null) return;
            syncedLoop = loopToggle != null ? loopToggle.isOn : !syncedLoop;
            player.Loop = syncedLoop;
            Share();
        }

        /// <summary>Re-loads the current video and jumps to the shared time (fixes desyncs).</summary>
        public void Resync()
        {
            if (syncedUrl == null || string.IsNullOrEmpty(syncedUrl.Get())) return;
            loadedUrl = VRCUrl.Empty;
            ApplySynced();
        }

        public void SeekChanged()
        {
            if (updatingUi || seekBar == null || player == null || !CanControl()) return;
            float t = seekBar.value * player.GetDuration();
            player.SetTime(t);
            syncedStart = Networking.GetServerTimeInSeconds() - t;
            syncedPausedAt = t;
            Share();
        }

        public void VolumeChanged()
        {
            if (volumeBar == null || speakers == null) return;
            for (int i = 0; i < speakers.Length; i++) if (speakers[i] != null) speakers[i].volume = volumeBar.value;
        }

        public void Next() => PlayIndex((syncedIndex + 1) % Mathf.Max(1, Count()));
        public void Previous() => PlayIndex((syncedIndex - 1 + Mathf.Max(1, Count())) % Mathf.Max(1, Count()));
        public void Play0() => PlayIndex(0);
        public void Play1() => PlayIndex(1);
        public void Play2() => PlayIndex(2);
        public void Play3() => PlayIndex(3);
        public void Play4() => PlayIndex(4);
        public void Play5() => PlayIndex(5);
        public void Play6() => PlayIndex(6);
        public void Play7() => PlayIndex(7);

        private int Count() => playlist == null ? 0 : playlist.Length;

        public void PlayIndex(int i)
        {
            if (!CanControl() || i < 0 || i >= Count() || playlist[i] == null) return;
            StartVideo(playlist[i], i);
        }

        /// <summary>For other scripts (e.g. the store's video preview): play a URL from the editor.</summary>
        public void PlayUrl(VRCUrl url) => StartVideo(url, -1);

        // ------------------------------------------------------------------ playback + sync

        private bool CanControl()
        {
            if (!masterOnly) return true;
            var me = Networking.LocalPlayer;
            return me != null && (me.isMaster || Networking.IsOwner(gameObject));
        }

        private void StartVideo(VRCUrl url, int index)
        {
            if (player == null) return;
            syncedUrl = url;
            syncedIndex = index;
            syncedPlaying = true;
            syncedStart = Networking.GetServerTimeInSeconds();
            syncedPausedAt = 0f;
            loadedUrl = url;
            player.PlayURL(url);
            SetStatus("Loading…");
            SetTitle(index);
            Share();
        }

        private void Share()
        {
            if (!synced) return;
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            RequestSerialization();
        }

        public override void OnDeserialization()
        {
            if (synced) ApplySynced();
        }

        private void ApplySynced()
        {
            if (player == null) return;
            player.Loop = syncedLoop;
            if (loopToggle != null && loopToggle.isOn != syncedLoop) { updatingUi = true; loopToggle.isOn = syncedLoop; updatingUi = false; }
            if (syncedUrl == null || string.IsNullOrEmpty(syncedUrl.Get())) { player.Stop(); loadedUrl = VRCUrl.Empty; return; }
            if (loadedUrl == null || loadedUrl.Get() != syncedUrl.Get())
            {
                loadedUrl = syncedUrl;
                player.PlayURL(syncedUrl);   // OnVideoReady jumps to the right time
                SetStatus("Loading…");
                SetTitle(syncedIndex);
                return;
            }
            if (syncedPlaying && !player.IsPlaying) player.Play();
            if (!syncedPlaying && player.IsPlaying) player.Pause();
            JumpToSharedTime();
        }

        private void JumpToSharedTime()
        {
            float target = syncedPlaying ? (float)(Networking.GetServerTimeInSeconds() - syncedStart) : syncedPausedAt;
            float duration = player.GetDuration();
            if (duration > 0 && !float.IsInfinity(duration)) target = syncedLoop ? target % duration : Mathf.Min(target, duration);
            if (Mathf.Abs(player.GetTime() - target) > 1f) player.SetTime(target);
        }

        public override void OnVideoReady()
        {
            SetStatus("Playing");
            if (synced && !Networking.IsOwner(gameObject))
            {
                JumpToSharedTime();
                if (!syncedPlaying) player.Pause();
            }
        }

        public override void OnVideoEnd() => SetStatus("Finished");

        public override void OnVideoError(VideoError videoError) => SetStatus("Error: " + videoError.ToString());

        private void Update()
        {
            if (player == null) return;
            float time = player.GetTime(), duration = player.GetDuration();
            bool live = float.IsInfinity(duration) || duration <= 0f;
            if (timeText != null) timeText.text = live ? Clock(time) + "  LIVE" : Clock(time) + " / " + Clock(duration);
            if (seekBar != null && !live)
            {
                updatingUi = true;
                seekBar.value = time / duration;
                updatingUi = false;
            }
            // Non-owners quietly fix drift every few seconds.
            if (synced && syncedPlaying && !Networking.IsOwner(gameObject) && Time.time > nextDriftCheck)
            {
                nextDriftCheck = Time.time + 5f;
                if (player.IsPlaying) JumpToSharedTime();
            }
        }

        private string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return s >= 3600 ? (s / 3600) + ":" + ((s / 60) % 60).ToString("00") + ":" + (s % 60).ToString("00") : (s / 60) + ":" + (s % 60).ToString("00");
        }

        private void SetStatus(string s)
        {
            if (statusText != null) statusText.text = s;
        }

        private void SetTitle(int index)
        {
            if (titleText == null) return;
            titleText.text = index >= 0 && titles != null && index < titles.Length ? titles[index] : "";
        }
    }
}
