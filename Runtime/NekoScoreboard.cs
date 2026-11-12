using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Persistence;
using VRC.SDKBase;

namespace NekoSune.WorldUiAssistant
{
    /// <summary>
    /// A synced scoreboard for everyone in the instance, sorted highest first, plus each player's
    /// personal best saved between visits (PlayerData persistence). Buttons call AddPoint / RemovePoint;
    /// game scripts set "points" then send "AddPoints".
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class NekoScoreboard : UdonSharpBehaviour
    {
        public const int MaxPlayers = 32;
        public TextMeshProUGUI board;
        public TextMeshProUGUI personalBest;
        public int showTop = 10;
        [Tooltip("Only the instance master can reset the board.")]
        public bool masterResetOnly = true;
        [Tooltip("Points added by AddPoints (other scripts can set this first).")]
        public int points = 1;
        public string saveKey = "neko.scoreboard.best";

        [UdonSynced] private string[] names = new string[MaxPlayers];
        [UdonSynced] private int[] scores = new int[MaxPlayers];

        private int best;

        private void Start() => Draw();

        public void AddPoint() => Change(1);
        public void RemovePoint() => Change(-1);
        public void AddPoints() => Change(points);

        private void Change(int amount)
        {
            var me = Networking.LocalPlayer;
            if (me == null) return;
            Networking.SetOwner(me, gameObject);
            int slot = SlotFor(me.displayName);
            if (slot < 0) return;
            names[slot] = me.displayName;
            scores[slot] += amount;
            RequestSerialization();
            if (scores[slot] > best)
            {
                best = scores[slot];
                PlayerData.SetInt(saveKey, best);
            }
            Draw();
        }

        public void ResetBoard()
        {
            var me = Networking.LocalPlayer;
            if (me == null || (masterResetOnly && !me.isMaster)) return;
            Networking.SetOwner(me, gameObject);
            for (int i = 0; i < MaxPlayers; i++) { names[i] = null; scores[i] = 0; }
            RequestSerialization();
            Draw();
        }

        private int SlotFor(string name)
        {
            int free = -1;
            for (int i = 0; i < MaxPlayers; i++)
            {
                if (names[i] == name) return i;
                if (free < 0 && string.IsNullOrEmpty(names[i])) free = i;
            }
            return free;
        }

        public override void OnDeserialization() => Draw();

        public override void OnPlayerRestored(VRCPlayerApi player)
        {
            if (!player.isLocal) return;
            if (PlayerData.TryGetInt(player, saveKey, out int saved)) best = saved;
            Draw();
        }

        private void Draw()
        {
            if (personalBest != null) personalBest.text = "Your best: " + best;
            if (board == null) return;
            // Pick the highest remaining score each time (small lists, so this is fine).
            var used = new bool[MaxPlayers];
            string s = "";
            for (int rank = 1; rank <= showTop; rank++)
            {
                int top = -1;
                for (int i = 0; i < MaxPlayers; i++)
                    if (!used[i] && !string.IsNullOrEmpty(names[i]) && (top < 0 || scores[i] > scores[top])) top = i;
                if (top < 0) break;
                used[top] = true;
                s += rank + ". " + names[top] + "  <b>" + scores[top] + "</b>\n";
            }
            board.text = s == "" ? "No scores yet" : s;
        }
    }
}
