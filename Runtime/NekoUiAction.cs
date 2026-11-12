using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace NekoSune.WorldUiAssistant
{
    /// <summary>
    /// Makes a UI button or toggle do one thing. The World UI Assistant adds this next to the
    /// Button/Toggle and points the click at it: Button.onClick → UdonBehaviour.SendCustomEvent("Run").
    ///
    /// VRChat can't call your own C# methods straight from a UI event, but it can call
    /// SendCustomEvent on an UdonBehaviour, which then runs the method with that name.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class NekoUiAction : UdonSharpBehaviour
    {
        // What happens. The Assistant sets this; the numbers match its action list.
        public const int ShowObjects = 0;
        public const int HideObjects = 1;
        public const int ToggleObjects = 2;
        public const int PlaySound = 3;
        public const int StopSound = 4;
        public const int TeleportPlayer = 5;
        public const int OpenPage = 6;
        public const int CustomEvent = 7;
        public const int ShowOneOfGroup = 8;

        [Tooltip("Which action runs (see the constants above).")]
        public int action;

        [Tooltip("On: everyone in the instance sees the change. Off: only the player who clicked.")]
        public bool synced;

        [Tooltip("Objects to show, hide or switch.")]
        public GameObject[] targets;

        [Tooltip("Sound to play or stop.")]
        public AudioSource sound;

        [Tooltip("Where the player is teleported to.")]
        public Transform teleportTarget;

        [Tooltip("The page to open, and all pages in the same menu (the others are hidden).")]
        public GameObject page;
        public GameObject[] pages;

        [Tooltip("Another Udon script and the name of the event to send to it.")]
        public UdonBehaviour eventTarget;
        public string eventName;

        [Tooltip("Set when a UI Toggle drives this: its on/off state is used instead of switching.")]
        public Toggle uiToggle;

        [Tooltip("Optional message popup to show after the action, e.g. \"Mirror on\".")]
        public NekoToast toast;
        public string toastText;

        // The shared state when synced. Late joiners receive it too.
        [UdonSynced] private bool syncedOn;
        [UdonSynced] private int syncedIndex = -1;
        private bool applying;

        /// <summary>Called by the button or toggle.</summary>
        public void Run()
        {
            if (applying) return; // we changed the toggle ourselves after a sync

            if (action == ShowObjects || action == HideObjects || action == ToggleObjects)
            {
                bool on = NewObjectState();
                if (synced) ShareState(on);
                SetObjects(on);
            }
            else if (action == PlaySound || action == StopSound)
            {
                bool play = uiToggle != null ? uiToggle.isOn : action == PlaySound;
                if (synced) ShareState(play);
                SetSound(play);
            }
            else if (action == TeleportPlayer)
            {
                // Teleporting always moves only the player who clicked.
                if (teleportTarget != null) Networking.LocalPlayer.TeleportTo(teleportTarget.position, teleportTarget.rotation);
            }
            else if (action == OpenPage)
            {
                // Pages are local: every player browses the menu on their own.
                if (pages == null) return;
                for (int i = 0; i < pages.Length; i++)
                    if (pages[i] != null) pages[i].SetActive(pages[i] == page);
            }
            else if (action == CustomEvent)
            {
                if (eventTarget == null || string.IsNullOrEmpty(eventName)) return;
                if (synced) eventTarget.SendCustomNetworkEvent(NetworkEventTarget.All, eventName);
                else eventTarget.SendCustomEvent(eventName);
            }
            else if (action == ShowOneOfGroup)
            {
                // Shows "page" and hides the rest of "targets" (e.g. mirror HQ / LQ / off). No page = all hidden.
                int index = IndexOf(page);
                if (synced)
                {
                    Networking.SetOwner(Networking.LocalPlayer, gameObject);
                    syncedIndex = index;
                    RequestSerialization();
                }
                ShowOnly(index);
            }

            if (toast != null && !string.IsNullOrEmpty(toastText)) toast.Show(toastText);
        }

        private int IndexOf(GameObject go)
        {
            if (targets == null || go == null) return -1;
            for (int i = 0; i < targets.Length; i++) if (targets[i] == go) return i;
            return -1;
        }

        private void ShowOnly(int index)
        {
            if (targets == null) return;
            for (int i = 0; i < targets.Length; i++)
                if (targets[i] != null) targets[i].SetActive(i == index);
        }

        private bool NewObjectState()
        {
            if (uiToggle != null) return action == HideObjects ? !uiToggle.isOn : uiToggle.isOn;
            if (action == ShowObjects) return true;
            if (action == HideObjects) return false;
            // Switch: base it on the first object.
            return targets == null || targets.Length == 0 || targets[0] == null || !targets[0].activeSelf;
        }

        private void ShareState(bool on)
        {
            // Only the owner of an object can change its synced values.
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            syncedOn = on;
            RequestSerialization();
        }

        /// <summary>Runs on everyone else when the owner shares a new state.</summary>
        public override void OnDeserialization()
        {
            if (action == ShowOneOfGroup) { ShowOnly(syncedIndex); return; }
            if (action == PlaySound || action == StopSound) SetSound(syncedOn);
            else SetObjects(syncedOn);

            if (uiToggle != null)
            {
                bool shown = action == HideObjects ? !syncedOn : syncedOn;
                if (uiToggle.isOn != shown)
                {
                    applying = true;
                    uiToggle.isOn = shown;
                    applying = false;
                }
            }
        }

        private void SetObjects(bool on)
        {
            if (targets == null) return;
            for (int i = 0; i < targets.Length; i++)
                if (targets[i] != null) targets[i].SetActive(on);
        }

        private void SetSound(bool play)
        {
            if (sound == null) return;
            if (play) sound.Play();
            else sound.Stop();
        }
    }
}
