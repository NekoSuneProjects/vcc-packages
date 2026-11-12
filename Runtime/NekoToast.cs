using TMPro;
using UdonSharp;
using UnityEngine;

namespace NekoSune.WorldUiAssistant
{
    /// <summary>
    /// A small message that pops up for a few seconds ("Mirror on", "Welcome!"). Local: each player
    /// sees their own. Other scripts call Show("text"); buttons can use the preset messages.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class NekoToast : UdonSharpBehaviour
    {
        [Tooltip("The object that appears (usually a small panel). Hidden when no message is showing.")]
        public GameObject box;
        public TextMeshProUGUI label;
        [Tooltip("Seconds a message stays visible.")]
        public float seconds = 2.5f;
        [Tooltip("Shown when the world loads (leave empty for none).")]
        public string welcomeMessage;
        [Tooltip("Messages for ShowPreset0 … ShowPreset4 (handy for buttons).")]
        public string[] presets;

        private int showId;

        private void Start()
        {
            if (box != null) box.SetActive(false);
            if (!string.IsNullOrEmpty(welcomeMessage)) SendCustomEventDelayedSeconds(nameof(ShowWelcome), 2f);
        }

        public void ShowWelcome() => Show(welcomeMessage);

        /// <summary>Shows a message; a newer message replaces an older one.</summary>
        public void Show(string message)
        {
            if (label != null) label.text = message;
            if (box != null) box.SetActive(true);
            showId++;
            SendCustomEventDelayedSeconds(nameof(HideIfOld), seconds);
        }

        private int hideChecks;

        public void HideIfOld()
        {
            // Every Show() schedules one hide; only the hide for the newest message closes the box.
            hideChecks++;
            if (hideChecks < showId) return;
            if (box != null) box.SetActive(false);
            else if (label != null) label.text = "";
        }

        public void ShowPreset0() => ShowPreset(0);
        public void ShowPreset1() => ShowPreset(1);
        public void ShowPreset2() => ShowPreset(2);
        public void ShowPreset3() => ShowPreset(3);
        public void ShowPreset4() => ShowPreset(4);

        private void ShowPreset(int i)
        {
            if (presets != null && i < presets.Length) Show(presets[i]);
        }
    }
}
