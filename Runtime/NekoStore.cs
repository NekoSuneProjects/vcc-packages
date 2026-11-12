using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.Economy;
using VRC.SDK3.Data;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace NekoSune.WorldUiAssistant
{
    /// <summary>
    /// A shop / market board. Products are cards in a grid (built by the UI Assistant). Supports
    /// search, categories, sorting, a detail page with pictures and an optional video preview,
    /// VRChat Creator Economy products (open the real listing, show "Owned", unlock content for
    /// buyers) and external shops (link text + QR code, because Udon can't open a browser).
    /// Optionally refreshes names/prices/descriptions from a remote JSON file.
    /// Local: each player browses on their own.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class NekoStore : UdonSharpBehaviour
    {
        [Header("Catalog (one entry per product, same order as the cards)")]
        public string[] names;
        public string[] descriptions;
        public string[] prices;
        public float[] priceValues;
        public string[] categories;
        public int[] addedOrder;              // higher = newer
        public UdonProduct[] products;        // VRChat Creator Economy product, or empty for external shops
        public string[] externalUrls;
        public Sprite[] qrCodes;
        public Sprite[] pictures;
        public VRCUrl[] videoPreviews;

        [Header("Cards")]
        public GameObject[] cards;
        public TextMeshProUGUI[] cardNames;
        public TextMeshProUGUI[] cardPrices;
        public GameObject[] cardNewBadges;
        public GameObject[] cardOwnedBadges;
        public Transform grid;

        [Header("Pages")]
        public GameObject catalogPage;
        public GameObject detailPage;
        public TextMeshProUGUI detailName;
        public TextMeshProUGUI detailPrice;
        public TextMeshProUGUI detailDescription;
        public Image detailPicture;
        public GameObject detailBuyButton;
        public GameObject detailOwned;
        public GameObject detailLinkBox;
        public TMP_InputField detailLinkField;
        public Image detailQr;
        public GameObject detailVideoButton;
        public NekoVideoPanel videoPanel;

        [Header("Search and filters")]
        public TMP_InputField searchField;
        public string[] categoryNames;
        public TextMeshProUGUI resultText;

        [Header("Unlock content for buyers (Creator Economy)")]
        public GameObject[] unlockObjects;
        public int[] unlockForProduct;        // product index each object needs

        [Header("Supporter wall")]
        public TextMeshProUGUI supporterText;
        public string[] thankYouNames;

        [Header("Remote data (optional)")]
        [Tooltip("JSON array: [{\"name\":…, \"price\":…, \"description\":…, \"category\":…}] – one per card.")]
        public VRCUrl dataUrl;

        private int current = -1;
        private string category = "";
        private bool[] owned;
        private string supporters = "";

        private void Start()
        {
            owned = new bool[Count()];
            if (detailPage != null) detailPage.SetActive(false);
            if (catalogPage != null) catalogPage.SetActive(true);
            UpdateUnlocks();
            UpdateSupporterWall();
            if (dataUrl != null && !string.IsNullOrEmpty(dataUrl.Get())) VRCStringDownloader.LoadUrl(dataUrl, (IUdonEventReceiver)this);
            Filter();
        }

        private int Count() => names == null ? 0 : names.Length;

        // ------------------------------------------------------------------ cards → detail

        public void Open0() => ShowDetail(0);
        public void Open1() => ShowDetail(1);
        public void Open2() => ShowDetail(2);
        public void Open3() => ShowDetail(3);
        public void Open4() => ShowDetail(4);
        public void Open5() => ShowDetail(5);
        public void Open6() => ShowDetail(6);
        public void Open7() => ShowDetail(7);
        public void Open8() => ShowDetail(8);
        public void Open9() => ShowDetail(9);
        public void Open10() => ShowDetail(10);
        public void Open11() => ShowDetail(11);

        public void ShowDetail(int i)
        {
            if (i < 0 || i >= Count()) return;
            current = i;
            if (detailName != null) detailName.text = names[i];
            if (detailPrice != null) detailPrice.text = Get(prices, i);
            if (detailDescription != null) detailDescription.text = Get(descriptions, i);
            if (detailPicture != null && pictures != null && i < pictures.Length && pictures[i] != null) detailPicture.sprite = pictures[i];

            bool economy = products != null && i < products.Length && products[i] != null;
            bool isOwned = owned != null && i < owned.Length && owned[i];
            if (detailBuyButton != null) detailBuyButton.SetActive(economy && !isOwned);
            if (detailOwned != null) detailOwned.SetActive(isOwned);

            string url = Get(externalUrls, i);
            if (detailLinkBox != null) detailLinkBox.SetActive(!economy && url != "");
            if (detailLinkField != null) detailLinkField.text = url;
            if (detailQr != null && qrCodes != null && i < qrCodes.Length) detailQr.sprite = qrCodes[i];

            bool hasVideo = videoPreviews != null && i < videoPreviews.Length && videoPreviews[i] != null && !string.IsNullOrEmpty(videoPreviews[i].Get());
            if (detailVideoButton != null) detailVideoButton.SetActive(hasVideo && videoPanel != null);

            if (catalogPage != null) catalogPage.SetActive(false);
            if (detailPage != null) detailPage.SetActive(true);
        }

        public void Back()
        {
            if (detailPage != null) detailPage.SetActive(false);
            if (catalogPage != null) catalogPage.SetActive(true);
        }

        /// <summary>Opens the product's real listing in VRChat (Creator Economy).</summary>
        public void Buy()
        {
            if (current < 0 || products == null || current >= products.Length || products[current] == null) return;
            Store.OpenListing(products[current].ID);
        }

        public void OpenStorePage() => Store.OpenWorldStorePage();

        public void PlayPreview()
        {
            if (current < 0 || videoPanel == null || videoPreviews == null || current >= videoPreviews.Length) return;
            videoPanel.PlayUrl(videoPreviews[current]);
        }

        // ------------------------------------------------------------------ search, categories, sorting

        public void SearchChanged() => Filter();

        public void ShowAll() { category = ""; Filter(); }
        public void ShowCategory0() => SetCategory(0);
        public void ShowCategory1() => SetCategory(1);
        public void ShowCategory2() => SetCategory(2);
        public void ShowCategory3() => SetCategory(3);
        public void ShowCategory4() => SetCategory(4);

        private void SetCategory(int i)
        {
            category = categoryNames != null && i < categoryNames.Length ? categoryNames[i] : "";
            Filter();
        }

        private void Filter()
        {
            string search = searchField != null ? searchField.text.ToLower().Trim() : "";
            int shown = 0;
            for (int i = 0; i < Count() && cards != null && i < cards.Length; i++)
            {
                bool ok = (category == "" || Get(categories, i) == category)
                          && (search == "" || names[i].ToLower().Contains(search) || Get(descriptions, i).ToLower().Contains(search));
                if (cards[i] != null) cards[i].SetActive(ok);
                if (ok) shown++;
            }
            if (resultText != null) resultText.text = shown + (shown == 1 ? " item" : " items");
        }

        public void SortByPrice() => Sort(0);
        public void SortByNewest() => Sort(1);
        public void SortByName() => Sort(2);

        /// <summary>Re-orders the cards in the grid (simple selection sort – fine for a few dozen items).</summary>
        private void Sort(int mode)
        {
            if (grid == null || cards == null) return;
            int n = Mathf.Min(Count(), cards.Length);
            var order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;
            for (int a = 0; a < n; a++)
            {
                int best = a;
                for (int b = a + 1; b < n; b++)
                    if (Before(order[b], order[best], mode)) best = b;
                int tmp = order[a]; order[a] = order[best]; order[best] = tmp;
            }
            for (int i = 0; i < n; i++) if (cards[order[i]] != null) cards[order[i]].transform.SetSiblingIndex(i);
        }

        private bool Before(int x, int y, int mode)
        {
            if (mode == 0) return GetF(priceValues, x) < GetF(priceValues, y);
            if (mode == 1) return GetI(addedOrder, x) > GetI(addedOrder, y);
            return string.Compare(names[x], names[y]) < 0;
        }

        // ------------------------------------------------------------------ ownership (Creator Economy)

        public override void OnPurchasesLoaded(IProduct[] loaded, VRCPlayerApi player)
        {
            if (player == null) return;
            if (loaded != null && loaded.Length > 0 && !player.isLocal) AddSupporter(player.displayName);
            if (!player.isLocal || loaded == null) return;
            for (int i = 0; i < loaded.Length; i++) SetOwned(loaded[i], true);
            if (loaded.Length > 0) AddSupporter(player.displayName);
        }

        public override void OnPurchaseConfirmed(IProduct product, VRCPlayerApi player, bool purchasedNow)
        {
            if (player == null) return;
            AddSupporter(player.displayName);
            if (player.isLocal) SetOwned(product, true);
        }

        public override void OnPurchaseExpired(IProduct product, VRCPlayerApi player)
        {
            if (player != null && player.isLocal) SetOwned(product, false);
        }

        private void SetOwned(IProduct product, bool value)
        {
            if (product == null || products == null) return;
            for (int i = 0; i < products.Length; i++)
            {
                if (products[i] == null || products[i].ID != product.ID) continue;
                if (owned != null && i < owned.Length) owned[i] = value;
                if (cardOwnedBadges != null && i < cardOwnedBadges.Length && cardOwnedBadges[i] != null) cardOwnedBadges[i].SetActive(value);
            }
            UpdateUnlocks();
            if (current >= 0 && detailPage != null && detailPage.activeSelf) ShowDetail(current);
        }

        private void UpdateUnlocks()
        {
            if (unlockObjects == null) return;
            for (int i = 0; i < unlockObjects.Length; i++)
            {
                int p = GetI(unlockForProduct, i);
                bool ok = owned != null && p >= 0 && p < owned.Length && owned[p];
                if (unlockObjects[i] != null) unlockObjects[i].SetActive(ok);
            }
        }

        private void AddSupporter(string name)
        {
            if (string.IsNullOrEmpty(name) || supporters.Contains("\n" + name + "\n")) return;
            supporters += "\n" + name + "\n";
            UpdateSupporterWall();
        }

        private void UpdateSupporterWall()
        {
            if (supporterText == null) return;
            string s = "";
            if (thankYouNames != null) for (int i = 0; i < thankYouNames.Length; i++) s += thankYouNames[i] + "\n";
            string[] here = supporters.Split('\n');
            for (int i = 0; i < here.Length; i++) if (here[i] != "") s += "♥ " + here[i] + "\n";
            supporterText.text = s;
        }

        // ------------------------------------------------------------------ remote data

        public override void OnStringLoadSuccess(IVRCStringDownload result)
        {
            if (!VRCJson.TryDeserializeFromJson(result.Result, out DataToken json) || json.TokenType != TokenType.DataList) return;
            var list = json.DataList;
            for (int i = 0; i < list.Count && i < Count(); i++)
            {
                if (!list.TryGetValue(i, TokenType.DataDictionary, out DataToken item)) continue;
                var d = item.DataDictionary;
                if (d.TryGetValue("name", TokenType.String, out DataToken n)) { names[i] = n.String; if (cardNames != null && i < cardNames.Length && cardNames[i] != null) cardNames[i].text = n.String; }
                if (d.TryGetValue("price", TokenType.String, out DataToken p)) { if (prices != null && i < prices.Length) prices[i] = p.String; if (cardPrices != null && i < cardPrices.Length && cardPrices[i] != null) cardPrices[i].text = p.String; }
                if (d.TryGetValue("description", TokenType.String, out DataToken ds) && descriptions != null && i < descriptions.Length) descriptions[i] = ds.String;
                if (d.TryGetValue("category", TokenType.String, out DataToken c) && categories != null && i < categories.Length) categories[i] = c.String;
            }
            Filter();
        }

        public override void OnStringLoadError(IVRCStringDownload result)
        {
            Debug.LogWarning("[NekoStore] Couldn't load store data: " + result.Error);
        }

        // ------------------------------------------------------------------ helpers

        private string Get(string[] a, int i) => a != null && i < a.Length && a[i] != null ? a[i] : "";
        private float GetF(float[] a, int i) => a != null && i < a.Length ? a[i] : 0f;
        private int GetI(int[] a, int i) => a != null && i < a.Length ? a[i] : -1;
    }
}
