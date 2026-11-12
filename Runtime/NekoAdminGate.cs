using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace NekoSune.WorldUiAssistant
{
    /// <summary>
    /// Shows admin/moderator controls only to allowed players: the instance master, the instance owner
    /// and/or a list of display names. Re-checks when players join or leave (the master can change).
    /// Note: display names can change and Udon isn't secret, so keep admin powers harmless.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class NekoAdminGate : UdonSharpBehaviour
    {
        [Tooltip("Shown only to allowed players.")]
        public GameObject[] adminOnly;
        [Tooltip("Shown to everyone else (e.g. \"Staff only\").")]
        public GameObject[] othersOnly;
        public bool allowMaster = true;
        public bool allowInstanceOwner = true;
        public string[] allowedNames;
        public TextMeshProUGUI statusText;

        private void Start() => Check();
        public override void OnPlayerJoined(VRCPlayerApi player) => Check();
        public override void OnPlayerLeft(VRCPlayerApi player) => SendCustomEventDelayedFrames(nameof(Check), 2);

        public void Check()
        {
            var me = Networking.LocalPlayer;
            if (me == null) return;
            bool allowed = (allowMaster && me.isMaster) || (allowInstanceOwner && me.isInstanceOwner);
            if (!allowed && allowedNames != null)
                for (int i = 0; i < allowedNames.Length; i++)
                    if (allowedNames[i] == me.displayName) { allowed = true; break; }
            if (adminOnly != null) for (int i = 0; i < adminOnly.Length; i++) if (adminOnly[i] != null) adminOnly[i].SetActive(allowed);
            if (othersOnly != null) for (int i = 0; i < othersOnly.Length; i++) if (othersOnly[i] != null) othersOnly[i].SetActive(!allowed);
            if (statusText != null) statusText.text = allowed ? "Admin controls" : "Staff only";
        }
    }
}
