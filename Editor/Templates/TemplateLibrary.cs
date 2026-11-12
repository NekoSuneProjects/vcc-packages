using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace NekoSune.WorldUiAssistant
{
    /// <summary>A ready-made panel or system the user drops in and customises with the normal Build tools.</summary>
    internal class UiTemplate
    {
        public string id;
        public string title;
        public string category;         // "Boards", "Menus", "Systems", "Games & social", "Pieces"
        public string description;
        public string icon = "Prefab Icon";
        public string lessonId;
        public Func<UiThemeId, Canvas> build;
    }

    internal static partial class TemplateLibrary
    {
        public const string HelperRootName = "World UI Assistant – Objects";
        public const string GeneratedFolder = "Assets/NekoSune/UIAssistant/Generated";

        private static List<UiTemplate> all;
        public static List<UiTemplate> All
        {
            get
            {
                if (all == null)
                {
                    all = new List<UiTemplate>();
                    AddBoards(all);
                    AddSystems(all);
                }
                return all;
            }
        }

        public static UiTemplate Get(string id) => All.FirstOrDefault(t => t.id == id);

        /// <summary>Builds a template as one Undo step and frames it.</summary>
        public static Canvas Create(UiTemplate t, UiThemeId theme)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            var canvas = t.build(theme);
            Undo.CollapseUndoOperations(group);
            Undo.SetCurrentGroupName("Create " + t.title);
            if (canvas)
            {
                Selection.activeGameObject = canvas.gameObject;
                UiFactory.Frame((RectTransform)canvas.transform);
            }
            return canvas;
        }

        // ------------------------------------------------------------------ scene helpers

        /// <summary>Tidy parent for objects templates make outside the UI (teleport points, mirrors…).</summary>
        public static Transform HelperRoot()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var existing = scene.GetRootGameObjects().FirstOrDefault(g => g.name == HelperRootName);
            if (existing) return existing.transform;
            var go = new GameObject(HelperRootName);
            Undo.RegisterCreatedObjectUndo(go, "Create helper objects");
            return go.transform;
        }

        public static GameObject HelperObject(string name, Vector3 position)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(HelperRoot(), false);
            go.transform.position = position;
            return go;
        }

        public static Vector3 ScenePivot => SceneView.lastActiveSceneView ? SceneView.lastActiveSceneView.pivot : Vector3.zero;

        public static T FindInScene<T>() where T : Component =>
            Resources.FindObjectsOfTypeAll<T>().FirstOrDefault(c => c && !EditorUtility.IsPersistent(c) && c.gameObject.scene.IsValid());

        public static Component FindInSceneByTypeName(string typeName) =>
            Resources.FindObjectsOfTypeAll<Component>().FirstOrDefault(c => c && c.GetType().Name == typeName && !EditorUtility.IsPersistent(c) && c.gameObject.scene.IsValid());

        public static string SafeFileName(string s) => new string(s.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());

        // ------------------------------------------------------------------ Phase 4: boards and menus

        /// <summary>Editable list of links for the Links / QR template (set in the Templates tab).</summary>
        public static List<(string name, string url)> Links = new List<(string, string)>
        {
            ("Discord", "https://discord.gg/your-invite"),
            ("Patreon", "https://patreon.com/your-name"),
            ("Website", "https://example.com"),
        };

        private static void AddBoards(List<UiTemplate> list)
        {
            list.Add(new UiTemplate
            {
                id = "welcome", title = "Welcome board", category = "Boards", lessonId = "t-welcome", icon = "d_Favorite",
                description = "Title, a welcome text and buttons that open Rules and Credits pages.",
                build = theme =>
                {
                    var b = UiBuilder.NewPanel("Welcome Board", new Vector2(900, 1000), theme);
                    Transform home = null, rules = null, credits = null;
                    home = b.Page("Home", true, () =>
                    {
                        b.Title("Welcome!");
                        b.Caption("A world by <your name>");
                        b.Divider();
                        b.Text("Make yourself at home. Mirrors and settings are on the menu by the spawn. Be kind and have fun!");
                        b.Spacer(20);
                    });
                    rules = b.Page("Rules", false, () => { b.Heading("Rules"); RulesText(b); });
                    credits = b.Page("Credits", false, () => { b.Heading("Credits"); CreditsText(b); });
                    var old = b.parent;
                    b.parent = home;
                    b.Row(() =>
                    {
                        var r = b.Button("Rules");
                        b.Action(r, ActionChoice.OpenPage, a => { a.page = rules.gameObject; UiActions.RefreshPages(a); });
                        var c = b.Button("Credits");
                        b.Action(c, ActionChoice.OpenPage, a => { a.page = credits.gameObject; UiActions.RefreshPages(a); });
                    });
                    foreach (var page in new[] { rules, credits })
                    {
                        b.parent = page;
                        var back = b.Button("Back");
                        b.Action(back, ActionChoice.OpenPage, a => { a.page = home.gameObject; UiActions.RefreshPages(a); });
                    }
                    b.parent = old;
                    return b.panel;
                },
            });

            list.Add(new UiTemplate
            {
                id = "rules", title = "Rules board", category = "Boards", lessonId = "t-rules", icon = "d_UnityEditor.ConsoleWindow",
                description = "A clear numbered list of world rules.",
                build = theme =>
                {
                    var b = UiBuilder.NewPanel("Rules Board", new Vector2(800, 900), theme);
                    b.Title("World Rules");
                    b.Divider();
                    RulesText(b);
                    b.Caption("Breaking the rules may get you removed from the instance.");
                    return b.panel;
                },
            });

            list.Add(new UiTemplate
            {
                id = "settings", title = "Settings menu", category = "Menus", lessonId = "t-settings", icon = "SettingsIcon",
                description = "Mirror HQ / LQ / Off, music volume, post-processing, pens, chairs and colliders – all local, with a status message.",
                build = BuildSettings,
            });

            list.Add(new UiTemplate
            {
                id = "teleport", title = "Teleport menu", category = "Menus", lessonId = "t-teleport", icon = "d_MoveTool",
                description = "Buttons that teleport the player to points in your world. Points are created for you to move.",
                build = theme =>
                {
                    var b = UiBuilder.NewPanel("Teleport Menu", new Vector2(700, 800), theme);
                    b.Title("Teleport");
                    b.Caption("Only you are moved.");
                    var pivot = ScenePivot;
                    string[] names = { "Spawn", "Lounge", "Game room", "Rooftop" };
                    for (int i = 0; i < names.Length; i++)
                    {
                        var point = HelperObject("Teleport Point – " + names[i], pivot + new Vector3(i * 3f, 0f, 4f));
                        var btn = b.Button(names[i]);
                        b.Action(btn, ActionChoice.Teleport, a => a.teleportTarget = point.transform);
                    }
                    return b.panel;
                },
            });

            list.Add(new UiTemplate
            {
                id = "credits", title = "Credits board", category = "Boards", lessonId = "t-credits", icon = "d_Favorite",
                description = "Who made the world, assets used and thanks.",
                build = theme =>
                {
                    var b = UiBuilder.NewPanel("Credits Board", new Vector2(800, 900), theme);
                    b.Title("Credits");
                    b.Divider();
                    CreditsText(b);
                    return b.panel;
                },
            });

            list.Add(new UiTemplate
            {
                id = "schedule", title = "Event schedule", category = "Boards", lessonId = "t-schedule", icon = "d_UnityEditor.AnimationWindow",
                description = "Times and events in two neat columns.",
                build = theme =>
                {
                    var b = UiBuilder.NewPanel("Event Schedule", new Vector2(900, 900), theme);
                    b.Title("This week");
                    b.Caption("All times in UTC");
                    b.Divider();
                    string[,] rows = { { "Mon 20:00", "Chill & chat" }, { "Wed 19:00", "Dance night" }, { "Fri 21:00", "Movie night" }, { "Sat 18:00", "World hopping" }, { "Sun 17:00", "Photo meetup" } };
                    for (int i = 0; i < rows.GetLength(0); i++)
                    {
                        int r = i;
                        b.Row(() =>
                        {
                            var t = b.Text(rows[r, 0], TextStyle.Body, "Time");
                            t.fontStyle = FontStyles.Bold;
                            t.color = b.theme.accent;
                            b.Text(rows[r, 1], TextStyle.Body, "Event");
                        }, "Event " + (i + 1));
                    }
                    return b.panel;
                },
            });

            list.Add(new UiTemplate
            {
                id = "links", title = "Links / QR board", category = "Boards", lessonId = "t-links", icon = "d_Linked",
                description = "Your links as QR codes players can scan with their phone, plus copyable text. Edit the links in the Templates tab first.",
                build = theme =>
                {
                    var b = UiBuilder.NewPanel("Links Board", new Vector2(900, 1100), theme);
                    b.Title("Find me online");
                    b.Caption("Scan with your phone camera, or copy the link.");
                    foreach (var (name, url) in Links)
                    {
                        if (string.IsNullOrWhiteSpace(url)) continue;
                        var sprite = QrCode.SaveSprite(url, GeneratedFolder + "/QR/" + SafeFileName(name) + ".png");
                        b.Row(() =>
                        {
                            var img = b.Image(sprite, 200, "QR " + name);
                            var le = img.GetComponent<LayoutElement>();
                            le.preferredWidth = le.minWidth = 200;
                            le.flexibleWidth = 0;
                            b.Column(() =>
                            {
                                b.Heading(name);
                                var field = b.Input(url, "Link " + name);
                                field.text = url;
                                field.readOnly = true;
                            });
                        }, name);
                    }
                    return b.panel;
                },
            });
        }

        private static void RulesText(UiBuilder b)
        {
            string[] rules =
            {
                "1. Be kind. No harassment, hate or bullying.",
                "2. No crashers, loud mics or earrape.",
                "3. Respect personal space.",
                "4. Keep it suitable for everyone here.",
                "5. Listen to the world staff.",
            };
            foreach (var r in rules) b.Text(r);
        }

        private static void CreditsText(UiBuilder b)
        {
            b.Text("<b>World by</b>  <your name>");
            b.Text("<b>Shaders</b>  <shader authors>");
            b.Text("<b>Music</b>  <artists>");
            b.Text("<b>Thanks to</b>  everyone who visits ♥");
        }

        // ------------------------------------------------------------------ settings

        private const string MirrorPrefab = "Packages/com.vrchat.worlds/Samples/UdonExampleScene/Prefabs/VRCMirror.prefab";

        private static Canvas BuildSettings(UiThemeId theme)
        {
            var b = UiBuilder.NewPanel("Settings Menu", new Vector2(800, 1100), theme);
            b.Title("Settings");
            b.Caption("Only changes things for you.");

            // Message line at the bottom (a NekoToast on its own text).
            TMP_Text messageText = null;

            b.Heading("Mirror");
            var (hq, lq) = Mirrors();
            var mirrorButtons = new List<(string label, GameObject show)> { ("HQ", hq), ("LQ", lq), ("Off", null) };
            var buttons = new List<Button>();
            b.Row(() => { foreach (var m in mirrorButtons) buttons.Add(b.Button(m.label, "Mirror " + m.label)); }, "Mirror Buttons");
            for (int i = 0; i < buttons.Count; i++)
            {
                var show = mirrorButtons[i].show;
                string label = mirrorButtons[i].label;
                b.Action(buttons[i], ActionChoice.ShowOne, a =>
                {
                    a.targets = new[] { hq, lq }.Where(x => x).ToArray();
                    a.page = show;
                    a.toastText = "Mirror: " + label;
                });
            }

            b.Heading("Music volume");
            var slider = b.Slider(0f, 1f, 0.6f, "Music Volume");
            var sliderAction = UiActions.SetSliderChoice(slider, SliderChoice.SoundVolume);
            var music = FindInScene<AudioSource>();
            if (music) UiActions.EditSlider(sliderAction, "Template", x => x.sounds = new[] { music });

            b.Heading("Effects");
            var pp = FindInSceneByTypeName("PostProcessVolume");
            var ppToggle = b.Toggle("Post-processing", true);
            b.Action(ppToggle, ActionChoice.ShowHide, a => { a.targets = pp ? new[] { pp.gameObject } : new GameObject[0]; a.toastText = "Post-processing changed"; });
            foreach (var name in new[] { "Pens", "Chairs", "Colliders" })
            {
                var t = b.Toggle(name, true);
                b.Action(t, ActionChoice.ShowHide, a => a.targets = new GameObject[0]);
            }

            b.Spacer(10);
            messageText = b.Caption("");
            messageText.name = "Message";
            messageText.color = b.theme.accent;
            var toast = UiBuilder.AddUdon<NekoToast>(messageText.gameObject);
            UiBuilder.Set(toast, x => { x.box = null; x.label = (TextMeshProUGUI)messageText; x.seconds = 2.5f; });

            foreach (var a in b.panel.GetComponentsInChildren<NekoUiAction>(true))
                if (!string.IsNullOrEmpty(a.toastText)) UiActions.Edit(a, "Template", x => x.toast = toast);
            return b.panel;
        }

        /// <summary>HQ and LQ mirrors: existing ones if the scene has them, otherwise from the SDK prefab (both start off).</summary>
        private static (GameObject hq, GameObject lq) Mirrors()
        {
            var existing = UiActions.FindMirrors();
            if (existing.Count >= 2) return (existing[0], existing[1]);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MirrorPrefab);
            if (!prefab) return (existing.FirstOrDefault(), null);
            GameObject Make(string name, bool lowQuality)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Undo.RegisterCreatedObjectUndo(go, "Create mirror");
                go.name = name;
                go.transform.SetParent(HelperRoot(), false);
                go.transform.position = ScenePivot + new Vector3(0, 1.5f, 6f);
                if (lowQuality)
                {
                    // LQ: reflect only players (layers PlayerLocal + MirrorReflection), not the world.
                    var mirror = go.GetComponentsInChildren<Component>(true).FirstOrDefault(c => c && c.GetType().Name == "VRCMirrorReflection");
                    if (mirror)
                    {
                        var so = new SerializedObject(mirror);
                        var layers = so.FindProperty("m_ReflectLayers");
                        if (layers != null)
                        {
                            int mask = 0;
                            foreach (var layer in new[] { "PlayerLocal", "MirrorReflection", "Player" })
                            {
                                int l = LayerMask.NameToLayer(layer);
                                if (l >= 0) mask |= 1 << l;
                            }
                            layers.intValue = mask;
                            so.ApplyModifiedPropertiesWithoutUndo();
                        }
                    }
                }
                go.SetActive(false);
                return go;
            }
            var hq = existing.Count == 1 ? existing[0] : Make("Mirror HQ", false);
            var lq = Make("Mirror LQ", true);
            return (hq, lq);
        }
    }
}
