using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;

namespace NekoSune.WorldUiAssistant
{
    /// <summary>
    /// Desktop hotkeys for UI actions (e.g. M = mirror), an on-screen list of them, and an optional VR
    /// shortcut. Uses Input.GetKeyDown, which works for desktop players only; VR players use VRChat's
    /// input events (InputJump/InputUse), which can't be rebound freely.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class NekoHotkeys : UdonSharpBehaviour
    {
        [Tooltip("Key codes (Unity KeyCode numbers – the UI Assistant fills these in). KeyCode arrays aren't available in Udon, so they are stored as numbers.")]
        public int[] keys;
        [Tooltip("What each key runs: an UdonBehaviour (e.g. a button's NekoUiAction) and the event name (usually Run).")]
        public UdonBehaviour[] targets;
        public string[] events;
        [Tooltip("Shown in the hotkey list, e.g. Mirror.")]
        public string[] labels;

        [Tooltip("Optional on-screen list of the hotkeys (local).")]
        public GameObject hintPanel;
        public TextMeshProUGUI hintText;
        public KeyCode hintKey = KeyCode.F1;
        public bool showHintsAtStart;

        [Tooltip("VR: double-tap jump runs this hotkey (-1 = off). Jump must not be needed for anything else.")]
        public int vrDoubleJumpRuns = -1;

        private float lastJump = -10f;

        private void Start()
        {
            if (hintText != null)
            {
                string s = "";
                for (int i = 0; i < keys.Length; i++)
                    s += "<b>" + ((KeyCode)keys[i]).ToString() + "</b>  " + (labels != null && i < labels.Length ? labels[i] : "") + "\n";
                s += "<b>" + hintKey.ToString() + "</b>  Show / hide this list";
                hintText.text = s;
            }
            if (hintPanel != null) hintPanel.SetActive(showHintsAtStart);
        }

        private void Update()
        {
            var me = Networking.LocalPlayer;
            if (me == null || me.IsUserInVR() || keys == null) return;
            for (int i = 0; i < keys.Length; i++)
                if (Input.GetKeyDown((KeyCode)keys[i])) RunHotkey(i);
            if (hintPanel != null && Input.GetKeyDown(hintKey)) hintPanel.SetActive(!hintPanel.activeSelf);
        }

        public void RunHotkey(int i)
        {
            if (targets == null || i < 0 || i >= targets.Length || targets[i] == null) return;
            string evt = events != null && i < events.Length && !string.IsNullOrEmpty(events[i]) ? events[i] : "Run";
            targets[i].SendCustomEvent(evt);
        }

        public override void InputJump(bool value, UdonInputEventArgs args)
        {
            if (vrDoubleJumpRuns < 0 || !value) return;
            var me = Networking.LocalPlayer;
            if (me == null || !me.IsUserInVR()) return;
            if (Time.time - lastJump < 0.4f) { RunHotkey(vrDoubleJumpRuns); lastJump = -10f; }
            else lastJump = Time.time;
        }
    }
}
