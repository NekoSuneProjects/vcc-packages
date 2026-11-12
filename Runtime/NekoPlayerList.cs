using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace NekoSune.WorldUiAssistant
{
    /// <summary>
    /// Player list with join times (as seen by you) and a world/instance info card: player count, how
    /// long you've been here, the instance master and owner. Local, updates on join/leave.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class NekoPlayerList : UdonSharpBehaviour
    {
        public const int Max = 90;
        public TextMeshProUGUI listText;
        public TextMeshProUGUI infoText;
        public string worldName = "My World";

        private int[] ids = new int[Max];
        private string[] joined = new string[Max];
        private int known;
        private DateTime arrived;
        private float nextInfo;

        private void Start()
        {
            arrived = DateTime.Now;
            Draw();
        }

        public override void OnPlayerJoined(VRCPlayerApi player)
        {
            if (player == null || known >= Max) return;
            ids[known] = player.playerId;
            // Players already here when we arrive are reported at our join too.
            joined[known] = (DateTime.Now - arrived).TotalSeconds < 5 && !player.isLocal ? "before you" : DateTime.Now.ToString("HH:mm");
            known++;
            Draw();
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            for (int i = 0; i < known; i++)
            {
                if (ids[i] != player.playerId) continue;
                ids[i] = ids[known - 1];
                joined[i] = joined[known - 1];
                known--;
                break;
            }
            SendCustomEventDelayedFrames(nameof(Draw), 1);
        }

        public void Draw()
        {
            if (listText != null)
            {
                string s = "";
                for (int i = 0; i < known; i++)
                {
                    var p = VRCPlayerApi.GetPlayerById(ids[i]);
                    if (p == null) continue;
                    s += p.displayName + (p.isMaster ? "  <size=70%>(master)</size>" : "") + "  <color=#9aa>" + joined[i] + "</color>\n";
                }
                listText.text = s;
            }
            DrawInfo();
        }

        private void DrawInfo()
        {
            if (infoText == null) return;
            string master = "", owner = "";
            var players = new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()];
            VRCPlayerApi.GetPlayers(players);
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null) continue;
                if (players[i].isMaster) master = players[i].displayName;
                if (players[i].isInstanceOwner) owner = players[i].displayName;
            }
            var here = DateTime.Now - arrived;
            infoText.text = "<b>" + worldName + "</b>\n" +
                            players.Length + (players.Length == 1 ? " player" : " players") + "\n" +
                            "You've been here " + (int)here.TotalMinutes + " min\n" +
                            "Master: " + master + (owner != "" ? "\nInstance owner: " + owner : "");
        }

        private void Update()
        {
            if (Time.time < nextInfo) return;
            nextInfo = Time.time + 30f;
            DrawInfo();
        }
    }
}
