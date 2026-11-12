using UdonSharp;
using UnityEngine;
using VRC.SDK3.Persistence;
using VRC.SDKBase;

namespace NekoSune.WorldUiAssistant
{
    /// <summary>
    /// Welcome/rules board with "Don't show again": the choice is saved for each player between visits
    /// (PlayerData persistence). Local.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class NekoWelcomeOnce : UdonSharpBehaviour
    {
        [Tooltip("The board to hide (usually the panel itself).")]
        public GameObject board;
        [Tooltip("Change this to show the board again to everyone (e.g. when the rules change).")]
        public string saveKey = "neko.welcome.hidden.v1";

        public override void OnPlayerRestored(VRCPlayerApi player)
        {
            if (!player.isLocal || board == null) return;
            if (PlayerData.TryGetBool(player, saveKey, out bool hidden) && hidden) board.SetActive(false);
        }

        /// <summary>Just close it for now.</summary>
        public void Close()
        {
            if (board != null) board.SetActive(false);
        }

        public void DontShowAgain()
        {
            PlayerData.SetBool(saveKey, true);
            Close();
        }

        /// <summary>Show it again later (e.g. from a settings menu).</summary>
        public void ShowAgain()
        {
            PlayerData.SetBool(saveKey, false);
            if (board != null) board.SetActive(true);
        }
    }
}
