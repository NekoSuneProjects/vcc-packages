using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Persistence;
using VRC.SDKBase;

namespace NekoSune.WorldUiAssistant
{
    /// <summary>
    /// Achievements / progress / simple inventory saved per player between visits (PlayerData).
    /// Other scripts or buttons call Unlock0 … Unlock9. Each entry has a row whose icon brightens
    /// when unlocked.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class NekoAchievements : UdonSharpBehaviour
    {
        public string[] titles;
        [Tooltip("Graphics (icons/rows) that light up when the entry is unlocked.")]
        public Graphic[] icons;
        public TextMeshProUGUI progressText;
        public Image progressBar;
        public Color lockedColor = new Color(1, 1, 1, 0.25f);
        public Color unlockedColor = Color.white;
        [Tooltip("Shown briefly on unlock (optional).")]
        public NekoToast toast;
        public string saveKeyPrefix = "neko.achievement.";

        private bool[] unlocked;
        private bool restored;

        private void Start()
        {
            unlocked = new bool[titles == null ? 0 : titles.Length];
            Draw();
        }

        public override void OnPlayerRestored(VRCPlayerApi player)
        {
            if (!player.isLocal) return;
            for (int i = 0; i < unlocked.Length; i++)
                if (PlayerData.TryGetBool(player, saveKeyPrefix + i, out bool v)) unlocked[i] = v;
            restored = true;
            Draw();
        }

        public void Unlock0() => Unlock(0);
        public void Unlock1() => Unlock(1);
        public void Unlock2() => Unlock(2);
        public void Unlock3() => Unlock(3);
        public void Unlock4() => Unlock(4);
        public void Unlock5() => Unlock(5);
        public void Unlock6() => Unlock(6);
        public void Unlock7() => Unlock(7);
        public void Unlock8() => Unlock(8);
        public void Unlock9() => Unlock(9);

        public void Unlock(int i)
        {
            if (unlocked == null || i < 0 || i >= unlocked.Length || unlocked[i]) return;
            unlocked[i] = true;
            if (restored) PlayerData.SetBool(saveKeyPrefix + i, true);
            if (toast != null) toast.Show("Unlocked: " + titles[i]);
            Draw();
        }

        /// <summary>Clears your own progress (e.g. a "Start over" button).</summary>
        public void ResetMine()
        {
            for (int i = 0; i < unlocked.Length; i++)
            {
                unlocked[i] = false;
                if (restored) PlayerData.SetBool(saveKeyPrefix + i, false);
            }
            Draw();
        }

        private void Draw()
        {
            int done = 0;
            for (int i = 0; i < unlocked.Length; i++)
            {
                if (unlocked[i]) done++;
                if (icons != null && i < icons.Length && icons[i] != null) icons[i].color = unlocked[i] ? unlockedColor : lockedColor;
            }
            if (progressText != null) progressText.text = done + " / " + unlocked.Length;
            if (progressBar != null) progressBar.fillAmount = unlocked.Length > 0 ? (float)done / unlocked.Length : 0f;
        }
    }
}
