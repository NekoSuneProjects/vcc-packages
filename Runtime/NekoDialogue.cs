using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace NekoSune.WorldUiAssistant
{
    /// <summary>
    /// Dialogue box for NPCs and story signs: speaker + line, typed out letter by letter. Next/Back/Restart.
    /// Local by default; synced keeps everyone on the same line (good for guided tours).
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class NekoDialogue : UdonSharpBehaviour
    {
        public string[] speakers;
        [TextArea] public string[] lines;
        public TextMeshProUGUI speakerText;
        public TextMeshProUGUI lineText;
        public TextMeshProUGUI progressText;
        [Tooltip("Letters per second (0 = show at once).")]
        public float typeSpeed = 40f;
        public bool synced;
        [Tooltip("Shown after the last line (e.g. a Restart button).")]
        public GameObject endGroup;

        [UdonSynced] private int syncedLine;
        private int line;
        private float typedChars;

        private void Start() => Go(0, false);

        public void Next() => Go(line + 1, true);
        public void Back() => Go(line - 1, true);
        public void Restart() => Go(0, true);

        /// <summary>Clicking while typing shows the whole line first.</summary>
        public void Skip()
        {
            if (lineText != null && typedChars < Current().Length) typedChars = Current().Length;
            else Next();
        }

        private void Go(int index, bool share)
        {
            if (lines == null || lines.Length == 0) return;
            line = Mathf.Clamp(index, 0, lines.Length - 1);
            typedChars = typeSpeed > 0 ? 0 : Current().Length;
            if (speakerText != null) speakerText.text = speakers != null && line < speakers.Length ? speakers[line] : "";
            if (progressText != null) progressText.text = (line + 1) + " / " + lines.Length;
            if (endGroup != null) endGroup.SetActive(line == lines.Length - 1);
            if (share && synced)
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
                syncedLine = line;
                RequestSerialization();
            }
        }

        public override void OnDeserialization() => Go(syncedLine, false);

        private string Current() => lines[line] == null ? "" : lines[line];

        private void Update()
        {
            if (lineText == null || lines == null || lines.Length == 0) return;
            string full = Current();
            if (typedChars < full.Length) typedChars += Time.deltaTime * typeSpeed;
            int n = Mathf.Min(full.Length, Mathf.FloorToInt(typedChars));
            lineText.text = full.Substring(0, n);
        }
    }
}
