using TMPro;
using UdonSharp;
using UnityEngine;

namespace NekoSune.WorldUiAssistant
{
    /// <summary>
    /// Photo-spot helper: a 3-2-1 countdown (so you can get into pose before taking the picture with
    /// the VRChat camera), with optional objects to show or hide while posing (lights, backdrops) and a
    /// beep. Local.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class NekoPhotoTimer : UdonSharpBehaviour
    {
        public int seconds = 3;
        public TextMeshProUGUI countdownText;
        public AudioSource beep;
        [Tooltip("Shown while the countdown runs (e.g. a ring light).")]
        public GameObject[] showWhilePosing;
        [Tooltip("Hidden while the countdown runs (e.g. this panel, so it's not in the photo).")]
        public GameObject[] hideWhilePosing;
        public float holdSeconds = 4f;

        private int left;

        public void StartCountdown()
        {
            left = seconds;
            SetPosing(true);
            Tick();
        }

        public void Tick()
        {
            if (countdownText != null) countdownText.text = left > 0 ? left.ToString() : "Smile!";
            if (beep != null) beep.Play();
            if (left > 0)
            {
                left--;
                SendCustomEventDelayedSeconds(nameof(Tick), 1f);
            }
            else SendCustomEventDelayedSeconds(nameof(Done), holdSeconds);
        }

        public void Done()
        {
            SetPosing(false);
            if (countdownText != null) countdownText.text = "";
        }

        private void SetPosing(bool posing)
        {
            if (showWhilePosing != null) for (int i = 0; i < showWhilePosing.Length; i++) if (showWhilePosing[i] != null) showWhilePosing[i].SetActive(posing);
            if (hideWhilePosing != null) for (int i = 0; i < hideWhilePosing.Length; i++) if (hideWhilePosing[i] != null) hideWhilePosing[i].SetActive(!posing);
        }
    }
}
