using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace NekoSune.WorldUiAssistant
{
    /// <summary>
    /// Number keypad for code doors and password panels. The right code shows/hides objects (e.g. hides
    /// a door). Local by default; synced opens it for everyone. Note: Udon code isn't secret – anyone
    /// who rips the world can read it, so don't use it to protect anything important.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class NekoKeypad : UdonSharpBehaviour
    {
        public string code = "1234";
        public int maxLength = 8;
        public TextMeshProUGUI display;
        [Tooltip("Show stars instead of digits.")]
        public bool hideDigits = true;
        [Tooltip("Shown when the code is right.")]
        public GameObject[] showOnSuccess;
        [Tooltip("Hidden when the code is right (e.g. the door).")]
        public GameObject[] hideOnSuccess;
        public bool synced;
        public AudioSource successSound;
        public AudioSource failSound;

        [UdonSynced] private bool syncedOpen;
        private string typed = "";

        private void Start() => Show();

        public void Press0() => Press("0");
        public void Press1() => Press("1");
        public void Press2() => Press("2");
        public void Press3() => Press("3");
        public void Press4() => Press("4");
        public void Press5() => Press("5");
        public void Press6() => Press("6");
        public void Press7() => Press("7");
        public void Press8() => Press("8");
        public void Press9() => Press("9");

        private void Press(string digit)
        {
            if (typed.Length >= maxLength) return;
            typed += digit;
            Show();
        }

        public void Clear()
        {
            typed = "";
            Show();
        }

        public void Enter()
        {
            if (typed == code)
            {
                if (synced)
                {
                    Networking.SetOwner(Networking.LocalPlayer, gameObject);
                    syncedOpen = true;
                    RequestSerialization();
                }
                Open(true);
                if (successSound != null) successSound.Play();
                if (display != null) display.text = "OK";
            }
            else
            {
                if (failSound != null) failSound.Play();
                if (display != null) display.text = "Wrong";
            }
            typed = "";
            SendCustomEventDelayedSeconds(nameof(Show), 1.2f);
        }

        /// <summary>Closes again (e.g. a "Lock" button).</summary>
        public void Lock()
        {
            if (synced)
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
                syncedOpen = false;
                RequestSerialization();
            }
            Open(false);
        }

        public override void OnDeserialization() => Open(syncedOpen);

        private void Open(bool open)
        {
            if (showOnSuccess != null) for (int i = 0; i < showOnSuccess.Length; i++) if (showOnSuccess[i] != null) showOnSuccess[i].SetActive(open);
            if (hideOnSuccess != null) for (int i = 0; i < hideOnSuccess.Length; i++) if (hideOnSuccess[i] != null) hideOnSuccess[i].SetActive(!open);
        }

        public void Show()
        {
            if (display == null) return;
            if (typed.Length == 0) { display.text = "Enter code"; return; }
            display.text = hideDigits ? new string('*', typed.Length) : typed;
        }
    }
}
