using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UdonSharp;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDK3.Video.Components.Base;
using VRC.SDKBase;
using VRC.Udon;
using B = NekoSune.WorldUiAssistant.UiBuilder;

namespace NekoSune.WorldUiAssistant
{
    /// <summary>Phase 4b: ready-made systems. Each builds normal UI plus one small Udon script.</summary>
    internal static partial class TemplateLibrary
    {
        private const string Samples = "Packages/com.vrchat.worlds/Samples/UdonExampleScene/Prefabs/";

        private static void AddSystems(List<UiTemplate> list)
        {
            void Add(string id, string title, string category, string icon, string description, Func<UiThemeId, Canvas> build) =>
                list.Add(new UiTemplate { id = id, title = title, category = category, icon = icon, description = description, lessonId = "s-" + id, build = build });

            // Media
            Add("video", "Video player controls", "Media", "VideoClip Icon", "Play/pause, stop, seek, time, volume, URL field, loop, resync and a playlist for a stock VRChat video player (one is added if your scene has none). Synced so everyone watches together.", BuildVideo);
            Add("music", "Music player", "Media", "AudioClip Icon", "Playlist with play/pause, next/back, shuffle and volume. Local or synced.", BuildMusic);
            Add("slideshow", "Gallery / slideshow", "Media", "Image Icon", "Pictures with captions, Next/Back and auto-advance. Synced for presentations.", BuildSlideshow);

            // Menus & controls
            Add("tabs", "Tabbed menu", "Menus", "d_UnityEditor.SceneHierarchyWindow", "Tab buttons across the top that switch pages – the classic menu.", BuildTabs);
            Add("portable", "Portable menu", "Menus", "d_UnityEditor.GameView", "A menu that opens near the player: in front of the face, following the head or on a hand. Desktop key or VR gesture.", BuildPortable);
            Add("hotkeys", "Hotkeys", "Menus", "d_UnityEditor.ConsoleWindow", "Desktop keys for your actions (e.g. M = mirror), an on-screen list (F1) and a VR double-jump shortcut.", BuildHotkeys);
            Add("admin", "Admin panel", "Menus", "d_Lock", "Controls only the instance master, instance owner or listed names can see.", BuildAdmin);

            // Shop
            Add("store", "Store / market", "Shop", "d_Prefab Icon", "Product cards with search, categories and sorting, a detail page with picture and video preview, VRChat Creator Economy listings (Owned + unlocks) and external shops with QR codes. Optional remote JSON data and a supporter wall.", BuildStore);

            // Games & social
            Add("keypad", "Keypad / code door", "Games", "d_Lock", "Number pad with a code that opens a door. Local or synced.", BuildKeypad);
            Add("scoreboard", "Scoreboard / leaderboard", "Games", "d_Favorite", "Synced scores for everyone, sorted, plus each player's saved personal best.", BuildScoreboard);
            Add("hud", "Game HUD", "Games", "d_UnityEditor.AnimationWindow", "Score, lives and a timer for mini-games, with game over.", BuildHud);
            Add("achievements", "Achievements / progress", "Games", "d_Favorite", "Unlockable achievements saved per player between visits, with a progress bar.", BuildAchievements);
            Add("poll", "Poll / voting", "Social", "d_VerticalLayoutGroup Icon", "Synced votes – one per player – with live result bars.", BuildPoll);
            Add("dialogue", "Dialogue box", "Social", "TextAsset Icon", "NPC / story text typed out line by line with a speaker name.", BuildDialogue);
            Add("players", "Player list & instance info", "Social", "d_UnityEditor.SceneHierarchyWindow", "Who's here, when they joined, the instance master/owner and your time in the instance.", BuildPlayers);
            Add("avatars", "Avatar board", "Social", "AvatarSelector", "Buttons that switch you into avatars (via hidden avatar pedestals), with search.", BuildAvatars);

            // Pieces
            Add("map", "Map – you are here", "Pieces", "d_ViewToolOrbit", "A marker that follows you on a map picture, with teleport buttons.", BuildMap);
            Add("photo", "Photo spot helper", "Pieces", "d_SceneViewCamera", "3-2-1 countdown with a ring light to pose for the VRChat camera.", BuildPhoto);
            Add("clock", "Clock & countdown", "Pieces", "d_UnityEditor.AnimationWindow", "Local time and a countdown to your next event (UTC).", BuildClock);
            Add("toast", "Message popup", "Pieces", "console.infoicon", "Short messages like \"Mirror on\" or \"Welcome!\". Actions can show messages here.", BuildToast);
            Add("welcome-once", "Welcome with \"Don't show again\"", "Pieces", "d_Favorite", "A rules/welcome board each player can hide for good (saved between visits).", BuildWelcomeOnce);
        }

        // ------------------------------------------------------------------ shared helpers

        private static T Udon<T>(Component on) where T : UdonSharpBehaviour => B.AddUdon<T>(on.gameObject);

        private static T Controller<T>(string name, UiBuilder b) where T : UdonSharpBehaviour
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(HelperRoot(), false);
            go.transform.position = b.panel.transform.position;
            return B.AddUdon<T>(go);
        }

        private static Button Btn(UiBuilder b, string label, UdonSharpBehaviour target, string evt)
        {
            var button = b.Button(label);
            B.OnClick(button, target, evt);
            return button;
        }

        private static TextMeshProUGUI Tmp(TMP_Text t) => (TextMeshProUGUI)t;

        /// <summary>The LayoutElement of a widget, added if it has none (texts don't by default).</summary>
        private static LayoutElement LE(Component c)
        {
            var le = c.GetComponent<LayoutElement>();
            return le ? le : Undo.AddComponent<LayoutElement>(c.gameObject);
        }

        private static Image FilledBar(UiBuilder b, string name, Color color, float height = 24f)
        {
            var img = b.Image(null, height, name);
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillAmount = 0f;
            img.color = color;
            return img;
        }

        private static GameObject InstantiateSample(string prefab, string name)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Samples + prefab);
            if (!asset) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = name;
            go.transform.SetParent(HelperRoot(), false);
            return go;
        }

        /// <summary>A URL field players type in. VRChat only lets Udon use typed URLs from a VRCUrlInputField.</summary>
        private static VRCUrlInputField UrlField(UiBuilder b, string placeholder)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("URL Field", typeof(RectTransform), typeof(Image));
            var image = go.GetComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd");
            image.type = Image.Type.Sliced;
            Text MakeText(string n, string value, FontStyle style, Color color)
            {
                var t = new GameObject(n, typeof(RectTransform), typeof(Text));
                t.transform.SetParent(go.transform, false);
                var rt = (RectTransform)t.transform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(14, 6); rt.offsetMax = new Vector2(-14, -6);
                var text = t.GetComponent<Text>();
                text.font = font; text.fontSize = b.theme.fontSize; text.text = value; text.fontStyle = style; text.color = color;
                text.alignment = TextAnchor.MiddleLeft; text.supportRichText = false;
                return text;
            }
            var placeholderText = MakeText("Placeholder", placeholder, FontStyle.Italic, new Color(0.2f, 0.2f, 0.2f, 0.6f));
            var valueText = MakeText("Text", "", FontStyle.Normal, Color.black);
            var field = go.AddComponent<VRCUrlInputField>();
            field.textComponent = valueText;
            field.placeholder = placeholderText;
            field.navigation = new Navigation { mode = Navigation.Mode.None };
            go.AddComponent<LayoutElement>().minHeight = 56;
            go.transform.SetParent(b.parent, false);
            UiFactory.SetLayer(go.transform, b.panel.gameObject.layer);
            Undo.RegisterCreatedObjectUndo(go, "Add URL field");
            return field;
        }

        private static void Wire(TMP_InputField f, UdonSharpBehaviour target, string evt)
        {
            var udon = UdonSharpEditor.UdonSharpEditorUtility.GetBackingUdonBehaviour(target);
            UnityEventTools.AddStringPersistentListener(f.onValueChanged, udon.SendCustomEvent, evt);
        }

        // ------------------------------------------------------------------ Media

        private static Canvas BuildVideo(UiThemeId theme)
        {
            var b = UiBuilder.NewPanel("Video Controls", new Vector2(1000, 900), theme);
            var player = FindInScene<BaseVRCVideoPlayer>();
            if (!player)
            {
                // The SDK's sample player, without its own sync script and UI (this panel replaces them).
                var go = InstantiateSample("VideoPlayers/UdonSyncPlayer (Unity).prefab", "Video Player");
                if (go)
                {
                    go.transform.position = b.panel.transform.position + b.panel.transform.right * 2f + Vector3.up * 0.5f;
                    foreach (var u in go.GetComponents<UdonBehaviour>()) Undo.DestroyObjectImmediate(u);
                    var oldUi = go.transform.Find("Canvas");
                    if (oldUi) Undo.DestroyObjectImmediate(oldUi.gameObject);
                    player = go.GetComponent<BaseVRCVideoPlayer>();
                }
            }

            var v = Udon<NekoVideoPanel>(b.panel);
            b.Title("Now playing");
            var title = b.Caption("");
            var status = b.Caption("Ready");
            var seek = b.Slider(0, 1, 0, "Seek");
            B.OnChange(seek, v, nameof(NekoVideoPanel.SeekChanged));
            var time = b.Text("0:00 / 0:00", TextStyle.Body, "Time");
            time.alignment = TextAlignmentOptions.Center;
            b.Row(() =>
            {
                Btn(b, "Play / Pause", v, nameof(NekoVideoPanel.TogglePause));
                Btn(b, "Stop", v, nameof(NekoVideoPanel.StopVideo));
                Btn(b, "Resync", v, nameof(NekoVideoPanel.Resync));
            }, "Controls");
            var loop = b.Toggle("Loop", false);
            B.OnChange(loop, v, nameof(NekoVideoPanel.ToggleLoop));
            b.Heading("Volume");
            var volume = b.Slider(0, 1, 0.5f, "Volume");
            B.OnChange(volume, v, nameof(NekoVideoPanel.VolumeChanged));
            b.Heading("Paste a link");
            VRCUrlInputField url = null;
            b.Row(() =>
            {
                url = UrlField(b, "https://…");
                LE(url).flexibleWidth = 3;
                Btn(b, "Play link", v, nameof(NekoVideoPanel.PlayFromField));
            }, "URL Row");
            b.Heading("Playlist");
            b.Row(() =>
            {
                Btn(b, "◀ Previous", v, nameof(NekoVideoPanel.Previous));
                Btn(b, "Next ▶", v, nameof(NekoVideoPanel.Next));
            }, "Playlist Buttons");

            B.Set(v, x =>
            {
                x.player = player;
                x.speakers = player ? player.GetComponentsInChildren<AudioSource>(true) : new AudioSource[0];
                x.urlField = url;
                x.seekBar = seek;
                x.volumeBar = volume;
                x.timeText = Tmp(time);
                x.statusText = Tmp(status);
                x.titleText = Tmp(title);
                x.loopToggle = loop;
                x.playlist = new VRCUrl[0];
                x.titles = new string[0];
            });
            return b.panel;
        }

        private static Canvas BuildMusic(UiThemeId theme)
        {
            var b = UiBuilder.NewPanel("Music Player", new Vector2(800, 700), theme);
            var m = Udon<NekoMusicPlayer>(b.panel);
            var sourceGo = HelperObject("Music", b.panel.transform.position);
            var source = Undo.AddComponent<AudioSource>(sourceGo);
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            b.Title("Music");
            var title = b.Heading("—");
            var time = b.Caption("0:00 / 0:00");
            b.Row(() =>
            {
                Btn(b, "◀◀", m, nameof(NekoMusicPlayer.Back));
                Btn(b, "Play / Pause", m, nameof(NekoMusicPlayer.TogglePlay));
                Btn(b, "▶▶", m, nameof(NekoMusicPlayer.Next));
            }, "Controls");
            var shuffle = b.Toggle("Shuffle", false);
            B.OnChange(shuffle, m, nameof(NekoMusicPlayer.ShuffleChanged));
            b.Heading("Volume");
            var volume = b.Slider(0, 1, 0.5f, "Volume");
            B.OnChange(volume, m, nameof(NekoMusicPlayer.VolumeChanged));
            B.Set(m, x =>
            {
                x.source = source; x.tracks = new AudioClip[0]; x.titles = new string[0];
                x.titleText = Tmp(title); x.timeText = Tmp(time); x.volumeBar = volume; x.shuffleToggle = shuffle;
            });
            return b.panel;
        }

        private static Canvas BuildSlideshow(UiThemeId theme)
        {
            var b = UiBuilder.NewPanel("Slideshow", new Vector2(1200, 1000), theme);
            var s = Udon<NekoSlideshow>(b.panel);
            var picture = b.Image(null, 620, "Picture");
            picture.preserveAspect = true;
            var caption = b.Text("Add your pictures to the Slides list", TextStyle.Body, "Caption");
            caption.alignment = TextAlignmentOptions.Center;
            var counter = b.Caption("1 / 1");
            counter.alignment = TextAlignmentOptions.Center;
            b.Row(() =>
            {
                Btn(b, "◀ Back", s, nameof(NekoSlideshow.Back));
                Btn(b, "Next ▶", s, nameof(NekoSlideshow.Next));
            }, "Buttons");
            B.Set(s, x => { x.slides = new Sprite[0]; x.captions = new string[0]; x.picture = picture; x.captionText = Tmp(caption); x.counterText = Tmp(counter); });
            return b.panel;
        }

        // ------------------------------------------------------------------ Menus & controls

        private static Canvas BuildTabs(UiThemeId theme)
        {
            var b = UiBuilder.NewPanel("Tabbed Menu", new Vector2(1000, 900), theme);
            string[] names = { "Info", "Rules", "Links" };
            var tabs = new List<Button>();
            b.Row(() => { foreach (var n in names) tabs.Add(b.Button(n, "Tab " + n)); }, "Tabs");
            b.Divider();
            var pages = new List<Transform>();
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                pages.Add(b.Page(names[i], i == 0, () =>
                {
                    b.Heading(names[index]);
                    b.Text("Content for the " + names[index] + " tab. Select this page in the outline and add anything.");
                }));
            }
            for (int i = 0; i < tabs.Count; i++)
            {
                var page = pages[i].gameObject;
                b.Action(tabs[i], ActionChoice.OpenPage, a => { a.page = page; UiActions.RefreshPages(a); });
            }
            return b.panel;
        }

        private static Canvas BuildPortable(UiThemeId theme)
        {
            var b = UiBuilder.NewPanel("Portable Menu", new Vector2(500, 600), theme);
            b.Title("Menu");
            b.Caption("Opens with M on desktop, or double-tap the left trigger in VR.");
            b.Button("Your button");
            b.Button("Another button");
            var controller = Controller<NekoPortableMenu>("Portable Menu Controller", b);
            Btn(b, "Close", controller, nameof(NekoPortableMenu.Close));
            B.Set(controller, x => { x.menu = b.panel.transform; x.followMode = NekoPortableMenu.StayWhereOpened; });
            return b.panel;
        }

        private static Canvas BuildHotkeys(UiThemeId theme)
        {
            var b = UiBuilder.NewPanel("Hotkeys", new Vector2(500, 420), theme);
            b.Heading("Hotkeys");
            var list = b.Text("", TextStyle.Body, "Hotkey List");
            var hotkeys = Udon<NekoHotkeys>(b.panel);

            // Use the first few actions already in the scene, e.g. a mirror button.
            var actions = Resources.FindObjectsOfTypeAll<NekoUiAction>().Where(a => a && !EditorUtility.IsPersistent(a) && a.gameObject.scene.IsValid() && !a.transform.IsChildOf(b.panel.transform)).Take(4).ToList();
            var keys = new[] { KeyCode.M, KeyCode.K, KeyCode.L, KeyCode.J };
            // The list follows your head when opened with F1 (a portable menu).
            var portable = Controller<NekoPortableMenu>("Hotkey List Controller", b);
            B.Set(portable, x => { x.menu = b.panel.transform; x.followMode = NekoPortableMenu.FollowHead; x.distance = 0.9f; x.desktopKey = KeyCode.F1; x.vrGesture = NekoPortableMenu.GestureNone; });
            B.Set(hotkeys, x =>
            {
                x.keys = actions.Select((a, i) => (int)keys[i]).ToArray();
                x.targets = actions.Select(a => UdonSharpEditor.UdonSharpEditorUtility.GetBackingUdonBehaviour(a)).ToArray();
                x.events = actions.Select(a => UiActions.RunEvent).ToArray();
                x.labels = actions.Select(a => a.name).ToArray();
                x.hintText = Tmp(list);
                x.hintPanel = null;
            });
            list.text = actions.Count == 0 ? "Add actions to buttons first, then set keys in the System card." : string.Join("\n", actions.Select((a, i) => keys[i] + "  " + a.name));
            return b.panel;
        }

        private static Canvas BuildAdmin(UiThemeId theme)
        {
            var b = UiBuilder.NewPanel("Admin Panel", new Vector2(700, 700), theme);
            var gate = Udon<NekoAdminGate>(b.panel);
            b.Title("Admin");
            var status = b.Caption("Staff only");
            Transform adminGroup = null, othersGroup = null;
            adminGroup = b.Column(() =>
            {
                b.Text("Only the instance master, instance owner and names you list see these.");
                b.Button("Admin button 1");
                b.Button("Admin button 2");
            }, "Admin Only");
            othersGroup = b.Column(() => b.Text("These controls are for world staff."), "Everyone Else");
            B.Set(gate, x => { x.adminOnly = new[] { adminGroup.gameObject }; x.othersOnly = new[] { othersGroup.gameObject }; x.allowedNames = new string[0]; x.statusText = Tmp(status); });
            return b.panel;
        }

        // ------------------------------------------------------------------ Shop

        private static readonly (string name, string price, float value, string cat, string desc, string url)[] SampleProducts =
        {
            ("Fox avatar", "$15", 15, "Avatars", "A fluffy fox base with 40 blendshapes and PhysBones.", "https://booth.pm/en"),
            ("Neon world kit", "$8", 8, "Worlds", "Props and shaders for a neon city world.", "https://gumroad.com"),
            ("Supporter pass", "In VRChat", 5, "Support", "Support the world – unlocks a golden badge here.", ""),
            ("Cat ears", "$4", 4, "Avatars", "Animated cat ears with toggles.", "https://jinxxy.com"),
            ("Shader pack", "$10", 10, "Worlds", "Water, glass and hologram shaders.", "https://gumroad.com"),
            ("Sticker set", "Free", 0, "Support", "Free stickers – grab them on Discord.", "https://discord.gg/your-invite"),
        };

        private static Canvas BuildStore(UiThemeId theme)
        {
            var b = UiBuilder.NewPanel("Store", new Vector2(1300, 1400), theme);
            var store = Udon<NekoStore>(b.panel);
            int n = SampleProducts.Length;
            var cards = new GameObject[n];
            var cardNames = new TextMeshProUGUI[n];
            var cardPrices = new TextMeshProUGUI[n];
            var newBadges = new GameObject[n];
            var ownedBadges = new GameObject[n];
            var qr = new Sprite[n];
            for (int i = 0; i < n; i++)
                if (!string.IsNullOrEmpty(SampleProducts[i].url))
                    qr[i] = QrCode.SaveSprite(SampleProducts[i].url, GeneratedFolder + "/QR/Store_" + SafeFileName(SampleProducts[i].name) + ".png");

            TMP_InputField search = null;
            TMP_Text results = null, supporters = null;
            Transform grid = null;
            var catalog = b.Page("Catalog", true, () =>
            {
                b.Title("Shop");
                b.Row(() =>
                {
                    search = b.Input("Search…", "Search");
                    LE(search).flexibleWidth = 3;
                    results = b.Caption("");
                }, "Search Row");
                b.Row(() =>
                {
                    Btn(b, "All", store, nameof(NekoStore.ShowAll));
                    Btn(b, "Avatars", store, nameof(NekoStore.ShowCategory0));
                    Btn(b, "Worlds", store, nameof(NekoStore.ShowCategory1));
                    Btn(b, "Support", store, nameof(NekoStore.ShowCategory2));
                }, "Categories");
                b.Row(() =>
                {
                    b.Caption("Sort:");
                    Btn(b, "Price", store, nameof(NekoStore.SortByPrice));
                    Btn(b, "Newest", store, nameof(NekoStore.SortByNewest));
                    Btn(b, "Name", store, nameof(NekoStore.SortByName));
                }, "Sort");
                grid = b.Grid(3, new Vector2(390, 330), () =>
                {
                    for (int i = 0; i < n; i++)
                    {
                        int index = i;
                        cards[i] = b.Column(() =>
                        {
                            var pic = b.Image(null, 150, "Picture");
                            pic.color = b.theme.control;
                            cardNames[index] = Tmp(b.Text(SampleProducts[index].name, TextStyle.Body, "Name"));
                            cardNames[index].fontStyle = FontStyles.Bold;
                            cardPrices[index] = Tmp(b.Text(SampleProducts[index].price, TextStyle.Caption, "Price"));
                            var badgeRow = b.Row(() =>
                            {
                                var nb = b.Caption(index < 2 ? "NEW" : "");
                                nb.color = b.theme.accent;
                                newBadges[index] = nb.gameObject;
                                var ob = b.Caption("OWNED ✓");
                                ob.color = b.theme.accent;
                                ownedBadges[index] = ob.gameObject;
                                ob.gameObject.SetActive(false);
                            }, "Badges");
                            Btn(b, "View", store, "Open" + index);
                        }, "Card " + SampleProducts[index].name, b.theme.control * new Color(1, 1, 1, 0.6f), 10f).gameObject;
                    }
                }, "Products");
                b.Divider();
                b.Heading("Thank you, supporters ♥");
                supporters = b.Text("", TextStyle.Body, "Supporters");
            });

            TMP_Text detailName = null, detailPrice = null, detailDesc = null;
            Image detailPic = null, detailQr = null;
            GameObject buy = null, owned = null, linkBox = null, videoBtn = null;
            TMP_InputField link = null;
            var detail = b.Page("Detail", false, () =>
            {
                Btn(b, "◀ Back to shop", store, nameof(NekoStore.Back));
                detailPic = b.Image(null, 360, "Picture");
                detailPic.color = b.theme.control;
                detailName = b.Heading("Product");
                detailPrice = b.Text("", TextStyle.Body, "Price");
                detailDesc = b.Text("", TextStyle.Body, "Description");
                buy = Btn(b, "Buy in VRChat", store, nameof(NekoStore.Buy)).gameObject;
                var ownedText = b.Text("You own this ✓", TextStyle.Body, "Owned");
                ownedText.color = b.theme.accent;
                owned = ownedText.gameObject;
                videoBtn = Btn(b, "▶ Watch preview", store, nameof(NekoStore.PlayPreview)).gameObject;
                linkBox = b.Row(() =>
                {
                    detailQr = b.Image(null, 220, "QR");
                    var le = detailQr.GetComponent<LayoutElement>();
                    le.preferredWidth = le.minWidth = 220;
                    detailQr.color = Color.white;
                    b.Column(() =>
                    {
                        b.Caption("Scan with your phone, or copy:");
                        link = b.Input("", "Link");
                        link.readOnly = true;
                    }, "Link Text");
                }, "External Link").gameObject;
                Btn(b, "Open the world's store", store, nameof(NekoStore.OpenStorePage));
            });
            Wire(search, store, nameof(NekoStore.SearchChanged));

            B.Set(store, x =>
            {
                x.names = SampleProducts.Select(p => p.name).ToArray();
                x.descriptions = SampleProducts.Select(p => p.desc).ToArray();
                x.prices = SampleProducts.Select(p => p.price).ToArray();
                x.priceValues = SampleProducts.Select(p => p.value).ToArray();
                x.categories = SampleProducts.Select(p => p.cat).ToArray();
                x.addedOrder = Enumerable.Range(0, n).Select(i => n - i).ToArray();
                x.products = new VRC.Economy.UdonProduct[n];
                x.externalUrls = SampleProducts.Select(p => p.url).ToArray();
                x.qrCodes = qr;
                x.pictures = new Sprite[n];
                x.videoPreviews = new VRCUrl[n];
                x.cards = cards; x.cardNames = cardNames; x.cardPrices = cardPrices; x.cardNewBadges = newBadges; x.cardOwnedBadges = ownedBadges;
                x.grid = grid;
                x.catalogPage = catalog.gameObject; x.detailPage = detail.gameObject;
                x.detailName = Tmp(detailName); x.detailPrice = Tmp(detailPrice); x.detailDescription = Tmp(detailDesc);
                x.detailPicture = detailPic; x.detailBuyButton = buy; x.detailOwned = owned; x.detailLinkBox = linkBox; x.detailLinkField = link; x.detailQr = detailQr;
                x.detailVideoButton = videoBtn;
                x.videoPanel = FindInScene<NekoVideoPanel>();
                x.searchField = search;
                x.categoryNames = new[] { "Avatars", "Worlds", "Support" };
                x.resultText = Tmp(results);
                x.unlockObjects = new GameObject[0]; x.unlockForProduct = new int[0];
                x.supporterText = Tmp(supporters);
                x.thankYouNames = new[] { "Everyone who visits" };
            });
            return b.panel;
        }

        // ------------------------------------------------------------------ Games & social

        private static Canvas BuildKeypad(UiThemeId theme)
        {
            var b = UiBuilder.NewPanel("Keypad", new Vector2(520, 800), theme);
            var k = Udon<NekoKeypad>(b.panel);
            var display = b.Heading("Enter code");
            display.alignment = TextAlignmentOptions.Center;
            b.Grid(3, new Vector2(150, 100), () =>
            {
                foreach (var d in new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9" }) Btn(b, d, k, "Press" + d);
                Btn(b, "C", k, nameof(NekoKeypad.Clear));
                Btn(b, "0", k, "Press0");
                Btn(b, "OK", k, nameof(NekoKeypad.Enter));
            }, "Keys");
            var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(door, "Create door");
            door.name = "Door (opens with the code)";
            door.transform.SetParent(HelperRoot(), false);
            door.transform.position = b.panel.transform.position + b.panel.transform.right * 1.2f;
            door.transform.localScale = new Vector3(1f, 2.2f, 0.1f);
            b.Caption("Code: 1234 – change it in the System card.");
            B.Set(k, x => { x.display = Tmp(display); x.hideOnSuccess = new[] { door }; x.showOnSuccess = new GameObject[0]; });
            return b.panel;
        }

        private static Canvas BuildScoreboard(UiThemeId theme)
        {
            var b = UiBuilder.NewPanel("Scoreboard", new Vector2(700, 900), theme);
            var s = Udon<NekoScoreboard>(b.panel);
            b.Title("Leaderboard");
            var board = b.Text("No scores yet", TextStyle.Body, "Board");
            var best = b.Caption("Your best: 0");
            b.Row(() =>
            {
                Btn(b, "+1", s, nameof(NekoScoreboard.AddPoint));
                Btn(b, "−1", s, nameof(NekoScoreboard.RemovePoint));
                Btn(b, "Reset", s, nameof(NekoScoreboard.ResetBoard));
            }, "Buttons");
            B.Set(s, x => { x.board = Tmp(board); x.personalBest = Tmp(best); });
            return b.panel;
        }

        private static Canvas BuildHud(UiThemeId theme)
        {
            var b = UiBuilder.NewPanel("Game HUD", new Vector2(800, 500), theme);
            var h = Udon<NekoGameHud>(b.panel);
            TMP_Text score = null, lives = null, timer = null, overText = null;
            b.Row(() =>
            {
                score = b.Heading("Score 0");
                lives = b.Heading("♥♥♥");
                timer = b.Heading("60");
            }, "Stats");
            Btn(b, "Start", h, nameof(NekoGameHud.StartGame));
            var over = b.Column(() =>
            {
                overText = b.Heading("Game over");
                Btn(b, "Play again", h, nameof(NekoGameHud.StartGame));
            }, "Game Over");
            b.Row(() =>
            {
                Btn(b, "Test: +score", h, nameof(NekoGameHud.AddScore));
                Btn(b, "Test: −life", h, nameof(NekoGameHud.LoseLife));
            }, "Test Buttons");
            B.Set(h, x => { x.scoreText = Tmp(score); x.livesText = Tmp(lives); x.timerText = Tmp(timer); x.gameOverGroup = over.gameObject; x.gameOverText = Tmp(overText); });
            return b.panel;
        }

        private static Canvas BuildAchievements(UiThemeId theme)
        {
            var b = UiBuilder.NewPanel("Achievements", new Vector2(800, 1000), theme);
            var a = Udon<NekoAchievements>(b.panel);
            b.Title("Achievements");
            var progress = b.Caption("0 / 5");
            var bar = FilledBar(b, "Progress Bar", b.theme.accent);
            string[] titles = { "Explorer – found the rooftop", "Social – said hi", "Collector – found 3 secrets", "Winner – won a game", "Regular – visited 5 times" };
            var icons = new List<Graphic>();
            foreach (var t in titles)
                b.Row(() =>
                {
                    var icon = b.Text("★", TextStyle.Heading, "Icon");
                    LE(icon).preferredWidth = 50;
                    icons.Add(icon);
                    b.Text(t, TextStyle.Body, "Title");
                }, t.Split(' ')[0]);
            Btn(b, "Test: unlock the first", a, nameof(NekoAchievements.Unlock0));
            B.Set(a, x => { x.titles = titles; x.icons = icons.ToArray(); x.progressText = Tmp(progress); x.progressBar = bar; x.toast = FindInScene<NekoToast>(); });
            return b.panel;
        }

        private static Canvas BuildPoll(UiThemeId theme)
        {
            var b = UiBuilder.NewPanel("Poll", new Vector2(900, 800), theme);
            var p = Udon<NekoPoll>(b.panel);
            b.Title("Which event next?");
            string[] options = { "Movie night", "Dance party", "Game night" };
            var bars = new Image[options.Length];
            var counts = new TextMeshProUGUI[options.Length];
            Transform results = null;
            for (int i = 0; i < options.Length; i++) Btn(b, options[i], p, "Vote" + i);
            results = b.Column(() =>
            {
                for (int i = 0; i < options.Length; i++)
                {
                    int index = i;
                    b.Row(() =>
                    {
                        var label = b.Text(options[index], TextStyle.Body, "Option");
                        LE(label).preferredWidth = 240;
                        bars[index] = FilledBar(b, "Bar", b.theme.accent, 30);
                        LE(bars[index]).flexibleWidth = 1;
                        counts[index] = Tmp(b.Text("0", TextStyle.Caption, "Count"));
                    }, "Result " + options[index]);
                }
            }, "Results");
            var total = b.Caption("0 votes");
            Btn(b, "Reset (master)", p, nameof(NekoPoll.ResetPoll));
            B.Set(p, x => { x.options = options; x.bars = bars; x.counts = counts; x.totalText = Tmp(total); x.resultsGroup = results.gameObject; });
            return b.panel;
        }

        private static Canvas BuildDialogue(UiThemeId theme)
        {
            var b = UiBuilder.NewPanel("Dialogue", new Vector2(1000, 500), theme);
            var d = Udon<NekoDialogue>(b.panel);
            var speaker = b.Heading("Guide");
            var line = b.Text("", TextStyle.Body, "Line");
            LE(line).minHeight = 140;
            var progress = b.Caption("1 / 3");
            b.Row(() =>
            {
                Btn(b, "◀ Back", d, nameof(NekoDialogue.Back));
                Btn(b, "Next ▶", d, nameof(NekoDialogue.Skip));
            }, "Buttons");
            var end = b.Column(() => Btn(b, "Start again", d, nameof(NekoDialogue.Restart)), "End");
            B.Set(d, x =>
            {
                x.speakers = new[] { "Guide", "Guide", "Guide" };
                x.lines = new[] { "Hi there! Welcome to my world.", "The mirrors and settings are on the menu behind you.", "Have fun, and be kind!" };
                x.speakerText = Tmp(speaker); x.lineText = Tmp(line); x.progressText = Tmp(progress); x.endGroup = end.gameObject;
            });
            return b.panel;
        }

        private static Canvas BuildPlayers(UiThemeId theme)
        {
            var b = UiBuilder.NewPanel("Players", new Vector2(900, 900), theme);
            var p = Udon<NekoPlayerList>(b.panel);
            b.Title("Who's here");
            var info = b.Text("", TextStyle.Body, "Instance Info");
            b.Divider();
            TMP_Text list = null;
            b.Scroll(520, () => list = b.Text("", TextStyle.Body, "List"));
            B.Set(p, x => { x.listText = Tmp(list); x.infoText = Tmp(info); x.worldName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name; });
            return b.panel;
        }

        private static Canvas BuildAvatars(UiThemeId theme)
        {
            var b = UiBuilder.NewPanel("Avatars", new Vector2(1000, 900), theme);
            var board = Udon<NekoAvatarBoard>(b.panel);
            b.Title("Avatars");
            var search = b.Input("Search…", "Search");
            Wire(search, board, nameof(NekoAvatarBoard.SearchChanged));
            var status = b.Caption("Paste avatar IDs (avtr_…) into the pedestals under World UI Assistant – Objects.");
            string[] names = { "Avatar 1", "Avatar 2", "Avatar 3", "Avatar 4" };
            var buttons = new GameObject[names.Length];
            var pedestals = new VRCAvatarPedestal[names.Length];
            b.Grid(2, new Vector2(440, 100), () =>
            {
                for (int i = 0; i < names.Length; i++) buttons[i] = Btn(b, names[i], board, "Use" + i).gameObject;
            }, "Avatar Buttons");
            for (int i = 0; i < names.Length; i++)
            {
                var go = InstantiateSample("AvatarPedestal.prefab", "Avatar Pedestal – " + names[i]);
                if (!go) { go = HelperObject("Avatar Pedestal – " + names[i], Vector3.zero); Undo.AddComponent<VRCAvatarPedestal>(go); }
                go.transform.position = b.panel.transform.position - b.panel.transform.forward * -1.5f + Vector3.down * 3f + b.panel.transform.right * i;
                foreach (var r in go.GetComponentsInChildren<Renderer>(true)) { Undo.RecordObject(r, "Hide pedestal"); r.enabled = false; }
                pedestals[i] = go.GetComponentInChildren<VRCAvatarPedestal>(true);
            }
            B.Set(board, x => { x.pedestals = pedestals; x.avatarNames = names; x.buttons = buttons; x.searchField = search; x.statusText = Tmp(status); });
            return b.panel;
        }

        // ------------------------------------------------------------------ Pieces

        private static Canvas BuildMap(UiThemeId theme)
        {
            var b = UiBuilder.NewPanel("Map", new Vector2(800, 1000), theme);
            var map = Udon<NekoMap>(b.panel);
            b.Title("Map");
            var area = b.Image(null, 600, "Map Picture");
            area.color = b.theme.control;
            var marker = new GameObject("You Are Here", typeof(RectTransform), typeof(Image));
            Undo.RegisterCreatedObjectUndo(marker, "Add marker");
            marker.transform.SetParent(area.transform, false);
            marker.layer = b.panel.gameObject.layer;
            var mrt = (RectTransform)marker.transform;
            mrt.sizeDelta = new Vector2(36, 36);
            var mimg = marker.GetComponent<Image>();
            mimg.color = b.theme.accent;
            mimg.raycastTarget = false;
            b.Caption("The marker shows where you are. Set the world area in the System card.");
            b.Row(() =>
            {
                foreach (var name in new[] { "Spawn", "Lake" })
                {
                    var point = HelperObject("Teleport Point – " + name, ScenePivot);
                    var btn = b.Button(name);
                    b.Action(btn, ActionChoice.Teleport, a => a.teleportTarget = point.transform);
                }
            }, "Teleports");
            B.Set(map, x => { x.mapArea = (RectTransform)area.transform; x.marker = mrt; });
            return b.panel;
        }

        private static Canvas BuildPhoto(UiThemeId theme)
        {
            var b = UiBuilder.NewPanel("Photo Spot", new Vector2(600, 600), theme);
            var t = Udon<NekoPhotoTimer>(b.panel);
            b.Title("Photo spot");
            var count = b.Text("", TextStyle.Title, "Countdown");
            Btn(b, "Start 3-2-1", t, nameof(NekoPhotoTimer.StartCountdown));
            var lightGo = HelperObject("Photo Ring Light", b.panel.transform.position + b.panel.transform.forward * -1f + Vector3.up * 0.3f);
            var light = Undo.AddComponent<Light>(lightGo);
            light.type = LightType.Point; light.range = 4; light.intensity = 1.5f;
            lightGo.SetActive(false);
            var toggle = b.Toggle("Ring light", false);
            b.Action(toggle, ActionChoice.ShowHide, a => a.targets = new[] { lightGo });
            b.Caption("Open the VRChat camera, press Start and pose.");
            B.Set(t, x => { x.countdownText = Tmp(count); x.showWhilePosing = new[] { lightGo }; x.hideWhilePosing = new GameObject[0]; });
            return b.panel;
        }

        private static Canvas BuildClock(UiThemeId theme)
        {
            var b = UiBuilder.NewPanel("Clock", new Vector2(700, 400), theme);
            var c = Udon<NekoClock>(b.panel);
            var clock = b.Title("12:00");
            var countdown = b.Text("", TextStyle.Heading, "Countdown");
            countdown.alignment = TextAlignmentOptions.Center;
            var next = DateTime.UtcNow.AddDays(7);
            B.Set(c, x => { x.clockText = Tmp(clock); x.countdownText = Tmp(countdown); x.eventName = "Next event"; x.year = next.Year; x.month = next.Month; x.day = next.Day; x.hour = 20; x.minute = 0; });
            return b.panel;
        }

        private static Canvas BuildToast(UiThemeId theme)
        {
            var b = UiBuilder.NewPanel("Message Popup", new Vector2(600, 160), theme);
            b.panel.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            var toast = Udon<NekoToast>(b.panel);
            TMP_Text label = null;
            var box = b.Column(() =>
            {
                label = b.Text("Welcome!", TextStyle.Heading, "Message");
                label.alignment = TextAlignmentOptions.Center;
            }, "Box", b.theme.background, 18f);
            B.Set(toast, x => { x.box = box.gameObject; x.label = Tmp(label); x.welcomeMessage = "Welcome!"; x.presets = new[] { "Mirror on", "Mirror off" }; });
            return b.panel;
        }

        private static Canvas BuildWelcomeOnce(UiThemeId theme)
        {
            var b = UiBuilder.NewPanel("Welcome (Don't Show Again)", new Vector2(800, 900), theme);
            var w = Udon<NekoWelcomeOnce>(b.panel);
            b.Title("Welcome!");
            b.Text("Please read the rules:");
            RulesText(b);
            b.Row(() =>
            {
                Btn(b, "Close", w, nameof(NekoWelcomeOnce.Close));
                Btn(b, "Don't show again", w, nameof(NekoWelcomeOnce.DontShowAgain));
            }, "Buttons");
            B.Set(w, x => x.board = b.panel.gameObject);
            return b.panel;
        }

        // ------------------------------------------------------------------ community video players

        /// <summary>Popular community video players found in the project (by type name, no hard reference).</summary>
        public static List<(string name, Component component)> CommunityPlayers()
        {
            var known = new[]
            {
                ("ProTV", "ArchiTech.ProTV"), ("USharpVideo", "UdonSharp.Video.USharpVideoPlayer"), ("VideoTXL", "Texel"),
                ("YamaPlayer", "Yamadev.YamaStream"), ("VizVid", "JLChnToZ.VRC.VVMW"),
            };
            var found = new List<(string, Component)>();
            foreach (var c in Resources.FindObjectsOfTypeAll<UdonSharpBehaviour>())
            {
                if (!c || EditorUtility.IsPersistent(c) || !c.gameObject.scene.IsValid()) continue;
                string full = c.GetType().FullName ?? "";
                foreach (var (name, ns) in known)
                    if (full.StartsWith(ns) && (full.Contains("TVManager") || full.Contains("SyncPlayer") || full.Contains("USharpVideoPlayer") || full.Contains("Core") || full.Contains("Player")))
                    {
                        if (!found.Any(f => f.Item1 == name)) found.Add((name, c));
                    }
            }
            return found;
        }
    }
}
