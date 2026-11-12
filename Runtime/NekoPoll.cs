using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace NekoSune.WorldUiAssistant
{
    /// <summary>
    /// Synced poll: one vote per player (changing your vote moves it), live result bars.
    /// Vote buttons call Vote0 … Vote5; the master can reset.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class NekoPoll : UdonSharpBehaviour
    {
        public const int MaxVoters = 80;
        public string[] options;
        [Tooltip("Filled Images (Image Type: Filled) showing each option's share.")]
        public Image[] bars;
        public TextMeshProUGUI[] counts;
        public TextMeshProUGUI totalText;
        [Tooltip("Hide the results until you have voted.")]
        public bool resultsAfterVoting;
        public GameObject resultsGroup;

        [UdonSynced] private int[] voterIds = new int[MaxVoters];
        [UdonSynced] private int[] voterChoice = new int[MaxVoters];
        [UdonSynced] private int voters;

        private void Start() => Draw();

        public void Vote0() => Vote(0);
        public void Vote1() => Vote(1);
        public void Vote2() => Vote(2);
        public void Vote3() => Vote(3);
        public void Vote4() => Vote(4);
        public void Vote5() => Vote(5);

        private void Vote(int option)
        {
            var me = Networking.LocalPlayer;
            if (me == null || options == null || option >= options.Length) return;
            Networking.SetOwner(me, gameObject);
            int slot = -1;
            for (int i = 0; i < voters; i++) if (voterIds[i] == me.playerId) slot = i;
            if (slot < 0)
            {
                if (voters >= MaxVoters) return;
                slot = voters++;
                voterIds[slot] = me.playerId;
            }
            voterChoice[slot] = option;
            RequestSerialization();
            Draw();
        }

        public void ResetPoll()
        {
            var me = Networking.LocalPlayer;
            if (me == null || !me.isMaster) return;
            Networking.SetOwner(me, gameObject);
            voters = 0;
            RequestSerialization();
            Draw();
        }

        public override void OnDeserialization() => Draw();

        private bool IVoted()
        {
            var me = Networking.LocalPlayer;
            if (me == null) return false;
            for (int i = 0; i < voters; i++) if (voterIds[i] == me.playerId) return true;
            return false;
        }

        private void Draw()
        {
            if (options == null) return;
            var tally = new int[options.Length];
            for (int i = 0; i < voters; i++) if (voterChoice[i] < tally.Length) tally[voterChoice[i]]++;
            for (int o = 0; o < options.Length; o++)
            {
                float share = voters > 0 ? (float)tally[o] / voters : 0f;
                if (bars != null && o < bars.Length && bars[o] != null) bars[o].fillAmount = share;
                if (counts != null && o < counts.Length && counts[o] != null) counts[o].text = tally[o] + " (" + Mathf.RoundToInt(share * 100) + "%)";
            }
            if (totalText != null) totalText.text = voters + (voters == 1 ? " vote" : " votes");
            if (resultsGroup != null) resultsGroup.SetActive(!resultsAfterVoting || IVoted());
        }
    }
}
