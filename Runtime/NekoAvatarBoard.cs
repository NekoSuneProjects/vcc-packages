using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

namespace NekoSune.WorldUiAssistant
{
    /// <summary>
    /// Avatar board: buttons switch the player into avatars from hidden VRChat avatar pedestals, with a
    /// search field that filters the buttons. Each avatar must be public (or yours) to be used.
    /// For a large searchable catalogue, use the NekoSune World Avatar Search package.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class NekoAvatarBoard : UdonSharpBehaviour
    {
        public VRCAvatarPedestal[] pedestals;
        public string[] avatarNames;
        [Tooltip("The button for each avatar (hidden when it doesn't match the search).")]
        public GameObject[] buttons;
        public TMP_InputField searchField;
        public TextMeshProUGUI statusText;

        public void Use0() => Use(0);
        public void Use1() => Use(1);
        public void Use2() => Use(2);
        public void Use3() => Use(3);
        public void Use4() => Use(4);
        public void Use5() => Use(5);
        public void Use6() => Use(6);
        public void Use7() => Use(7);
        public void Use8() => Use(8);
        public void Use9() => Use(9);
        public void Use10() => Use(10);
        public void Use11() => Use(11);

        private void Use(int i)
        {
            if (pedestals == null || i >= pedestals.Length || pedestals[i] == null) return;
            pedestals[i].SetAvatarUse(Networking.LocalPlayer);
            if (statusText != null && avatarNames != null && i < avatarNames.Length) statusText.text = "Switching to " + avatarNames[i] + "…";
        }

        public void SearchChanged()
        {
            if (buttons == null || avatarNames == null) return;
            string s = searchField != null ? searchField.text.ToLower().Trim() : "";
            for (int i = 0; i < buttons.Length && i < avatarNames.Length; i++)
                if (buttons[i] != null) buttons[i].SetActive(s == "" || avatarNames[i].ToLower().Contains(s));
        }
    }
}
