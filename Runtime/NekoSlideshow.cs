using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace NekoSune.WorldUiAssistant
{
    /// <summary>
    /// Picture gallery / slideshow with captions, Next/Back and optional auto-advance. Synced keeps
    /// everyone on the same slide (presentations). For pictures loaded from the web, use the
    /// NekoSune World Gallery package.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class NekoSlideshow : UdonSharpBehaviour
    {
        public Sprite[] slides;
        public string[] captions;
        public Image picture;
        public TextMeshProUGUI captionText;
        public TextMeshProUGUI counterText;
        [Tooltip("Seconds per slide (0 = only with the buttons).")]
        public float autoAdvanceSeconds = 0f;
        public bool synced;

        [UdonSynced] private int syncedIndex;
        private int index;
        private float nextAt;

        private void Start()
        {
            Show(0, false);
        }

        public void Next() => Show(index + 1, true);
        public void Back() => Show(index - 1, true);

        private void Show(int i, bool share)
        {
            if (slides == null || slides.Length == 0) return;
            index = (i % slides.Length + slides.Length) % slides.Length;
            if (picture != null) picture.sprite = slides[index];
            if (captionText != null) captionText.text = captions != null && index < captions.Length ? captions[index] : "";
            if (counterText != null) counterText.text = (index + 1) + " / " + slides.Length;
            nextAt = Time.time + autoAdvanceSeconds;
            if (share && synced)
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
                syncedIndex = index;
                RequestSerialization();
            }
        }

        public override void OnDeserialization() => Show(syncedIndex, false);

        private void Update()
        {
            if (autoAdvanceSeconds <= 0f || Time.time < nextAt) return;
            // When synced, only the owner advances (everyone else follows).
            if (synced && !Networking.IsOwner(gameObject)) { nextAt = Time.time + autoAdvanceSeconds; return; }
            Show(index + 1, true);
        }
    }
}
