using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace NekoSune.WorldUiAssistant
{
    /// <summary>
    /// Background music player with a playlist: play/pause, next/back, shuffle, loop and volume.
    /// Synced: everyone hears the same track; local: each player picks their own.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class NekoMusicPlayer : UdonSharpBehaviour
    {
        public AudioSource source;
        public AudioClip[] tracks;
        public string[] titles;
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI timeText;
        public Slider volumeBar;
        public Toggle shuffleToggle;
        public bool playOnStart = true;
        public bool shuffle;
        public bool synced;

        [UdonSynced] private int syncedTrack;
        [UdonSynced] private bool syncedPlaying;
        [UdonSynced] private double syncedStart;

        private int track;
        private bool playing;

        private void Start()
        {
            if (volumeBar != null) VolumeChanged();
            if (playOnStart && (!synced || Networking.IsOwner(gameObject))) PlayTrack(shuffle ? Random.Range(0, Count()) : 0, true);
        }

        private int Count() => tracks == null ? 0 : tracks.Length;

        public void TogglePlay()
        {
            if (source == null) return;
            playing = !playing;
            if (playing) source.UnPause(); else source.Pause();
            if (playing && !source.isPlaying) PlayTrack(track, true);
            Share();
        }

        public void Next() => PlayTrack(shuffle ? RandomOther() : (track + 1) % Mathf.Max(1, Count()), true);
        public void Back() => PlayTrack((track - 1 + Mathf.Max(1, Count())) % Mathf.Max(1, Count()), true);

        public void ShuffleChanged()
        {
            shuffle = shuffleToggle != null ? shuffleToggle.isOn : !shuffle;
        }

        public void VolumeChanged()
        {
            if (source != null && volumeBar != null) source.volume = volumeBar.value;
        }

        private int RandomOther()
        {
            if (Count() < 2) return 0;
            int r = Random.Range(0, Count() - 1);
            return r >= track ? r + 1 : r;
        }

        private void PlayTrack(int i, bool share)
        {
            if (source == null || Count() == 0) return;
            track = Mathf.Clamp(i, 0, Count() - 1);
            source.clip = tracks[track];
            source.time = 0f;
            source.Play();
            playing = true;
            if (titleText != null) titleText.text = titles != null && track < titles.Length ? titles[track] : (tracks[track] != null ? tracks[track].name : "");
            if (share)
            {
                syncedStart = Networking.GetServerTimeInSeconds();
                Share();
            }
        }

        private void Share()
        {
            if (!synced) return;
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            syncedTrack = track;
            syncedPlaying = playing;
            RequestSerialization();
        }

        public override void OnDeserialization()
        {
            if (source == null || Count() == 0) return;
            if (syncedTrack != track || (syncedPlaying && !source.isPlaying)) PlayTrack(syncedTrack, false);
            float t = (float)(Networking.GetServerTimeInSeconds() - syncedStart);
            if (source.clip != null && t > 0 && t < source.clip.length) source.time = t;
            playing = syncedPlaying;
            if (!playing) source.Pause();
        }

        private void Update()
        {
            if (source == null) return;
            if (timeText != null && source.clip != null)
                timeText.text = Clock(source.time) + " / " + Clock(source.clip.length);
            // Next track when one ends (the owner decides when synced).
            if (playing && !source.isPlaying && source.clip != null && source.time == 0f && (!synced || Networking.IsOwner(gameObject))) Next();
        }

        private string Clock(float s) => Mathf.FloorToInt(s / 60) + ":" + Mathf.FloorToInt(s % 60).ToString("00");
    }
}
