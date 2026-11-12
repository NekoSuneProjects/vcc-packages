using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Data;
using VRC.SDK3.Image;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace NekoSune.WorldUiAssistant
{
    /// <summary>
    /// Cards filled from the web: a JSON file (title/text per card) and pictures, so you can update a
    /// news board, event list or showcase without re-uploading the world.
    /// VRChat rules: URLs must be typed in the editor (VRCUrl), the JSON and images must be on an
    /// allowed host (e.g. GitHub Pages) – or players need "Allow Untrusted URLs" on – and downloads are
    /// rate-limited (one string every 5 s, one image every 5 s), so they are queued.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class NekoDataCards : UdonSharpBehaviour
    {
        [Tooltip("JSON array: [{\"title\": \"…\", \"text\": \"…\"}, …] – one object per card. Leave empty to only load pictures.")]
        public VRCUrl dataUrl;
        [Tooltip("One picture URL per card (PNG/JPG, max 2048 px).")]
        public VRCUrl[] imageUrls;

        public GameObject[] cards;
        public TextMeshProUGUI[] titles;
        public TextMeshProUGUI[] texts;
        public RawImage[] pictures;
        public TextMeshProUGUI statusText;
        [Tooltip("Reload every N minutes (0 = only once).")]
        public float refreshMinutes = 0f;
        [Tooltip("Hide cards that the JSON doesn't fill.")]
        public bool hideEmptyCards = true;

        private VRCImageDownloader images;
        private int nextImage;

        private void Start()
        {
            images = new VRCImageDownloader();
            Reload();
        }

        public void Reload()
        {
            if (dataUrl != null && !string.IsNullOrEmpty(dataUrl.Get()))
            {
                SetStatus("Loading…");
                VRCStringDownloader.LoadUrl(dataUrl, (IUdonEventReceiver)this);
            }
            nextImage = 0;
            LoadNextImage();
            if (refreshMinutes > 0f) SendCustomEventDelayedSeconds(nameof(Reload), refreshMinutes * 60f);
        }

        public void LoadNextImage()
        {
            if (imageUrls == null || pictures == null) return;
            while (nextImage < imageUrls.Length && (imageUrls[nextImage] == null || string.IsNullOrEmpty(imageUrls[nextImage].Get()) || nextImage >= pictures.Length || pictures[nextImage] == null)) nextImage++;
            if (nextImage >= imageUrls.Length) return;
            var info = new TextureInfo();
            info.GenerateMipMaps = true;
            images.DownloadImage(imageUrls[nextImage], null, (IUdonEventReceiver)this, info);
        }

        public override void OnImageLoadSuccess(IVRCImageDownload result)
        {
            if (nextImage < pictures.Length && pictures[nextImage] != null)
            {
                pictures[nextImage].texture = result.Result;
                pictures[nextImage].color = Color.white;
            }
            nextImage++;
            SendCustomEventDelayedSeconds(nameof(LoadNextImage), 5.5f);   // VRChat allows one image download every 5 s
        }

        public override void OnImageLoadError(IVRCImageDownload result)
        {
            Debug.LogWarning("[NekoDataCards] Picture " + nextImage + " failed: " + result.Error);
            nextImage++;
            SendCustomEventDelayedSeconds(nameof(LoadNextImage), 5.5f);
        }

        public override void OnStringLoadSuccess(IVRCStringDownload result)
        {
            if (!VRCJson.TryDeserializeFromJson(result.Result, out DataToken json) || json.TokenType != TokenType.DataList)
            {
                SetStatus("The data isn't a JSON list.");
                return;
            }
            var list = json.DataList;
            int count = cards == null ? 0 : cards.Length;
            for (int i = 0; i < count; i++)
            {
                bool has = i < list.Count && list.TryGetValue(i, TokenType.DataDictionary, out DataToken item);
                if (has)
                {
                    list.TryGetValue(i, TokenType.DataDictionary, out DataToken entry);
                    var d = entry.DataDictionary;
                    if (titles != null && i < titles.Length && titles[i] != null && d.TryGetValue("title", TokenType.String, out DataToken t)) titles[i].text = t.String;
                    if (texts != null && i < texts.Length && texts[i] != null && d.TryGetValue("text", TokenType.String, out DataToken x)) texts[i].text = x.String;
                }
                if (cards[i] != null && hideEmptyCards) cards[i].SetActive(has);
            }
            SetStatus("");
        }

        public override void OnStringLoadError(IVRCStringDownload result)
        {
            SetStatus("Couldn't load: " + result.Error);
        }

        private void SetStatus(string s)
        {
            if (statusText != null) statusText.text = s;
        }

        private void OnDestroy()
        {
            if (images != null) images.Dispose();
        }
    }
}
