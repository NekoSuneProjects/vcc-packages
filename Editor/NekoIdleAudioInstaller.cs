#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using NekoSuneVR.VRChatIdleAudio.UI;
using VRC.SDK3.Avatars.Components;

namespace NekoSuneVR.VRChatIdleAudio
{
    /// <summary>
    /// The hub window. Picks which installer to open and reports what is already set up on the
    /// selected avatar.
    ///
    /// The actual work lives in the two focused installers, which are fully independent of each
    /// other:
    ///   - <see cref="NekoIntroInstaller"/>   spawn intro, particle presets, boot audio
    ///   - <see cref="NekoAfkIdleInstaller"/> AFK loading phase and looping idle audio
    /// </summary>
    public class NekoIdleAudioInstaller : EditorWindow
    {
        private const string IntroRootName = "NekoSune_IntroOnly";
        private const string IdleRootName = "NekoSune_IdleOnly";
        private const string LegacyRootName = "NekoSune_IntroIdleAudio";

        [SerializeField] private GameObject avatarRoot;
        private Vector2 scroll;

        [MenuItem("Tools/NekoSuneVR/Intro + Idle Audio", false, 0)]
        public static void Open()
        {
            var window = GetWindow<NekoIdleAudioInstaller>(true, "NekoSuneVR", true);
            window.minSize = new Vector2(500, 620);
            window.maxSize = new Vector2(900, 1200);
            window.TryAutoSelect();
            window.Show();
        }

        private void OnEnable()
        {
            UpdateChecker.Changed += Repaint;
            TryAutoSelect();
        }

        private void TryAutoSelect()
        {
            if (avatarRoot != null || Selection.activeGameObject == null) return;

            var descriptor = Selection.activeGameObject.GetComponentInParent<VRCAvatarDescriptor>();
            if (descriptor != null)
                avatarRoot = descriptor.gameObject;
        }

        private void OnSelectionChange()
        {
            Repaint();
        }

        // ------------------------------------------------------------------
        // GUI
        // ------------------------------------------------------------------

        private void OnDisable()
        {
            UpdateChecker.Changed -= Repaint;
        }

        private void OnGUI()
        {
            NekoUI.WindowBackground(this);

            scroll = EditorGUILayout.BeginScrollView(scroll);

            NekoPackageChrome.Header("NekoSuneVR", "Avatar intro and AFK idle audio for VRChat");
            NekoPackageChrome.UpdateBanner();

            DrawAvatarCard();
            DrawToolCards();
            DrawEnvironmentCard();

            GUILayout.Space(12f);

            EditorGUILayout.EndScrollView();
            NekoAboutFooter.DrawWithSeparator();
        }

        private void DrawAvatarCard()
        {
            NekoUI.BeginCard("Avatar");
            NekoUI.BeginCardBody();

            EditorGUI.BeginChangeCheck();
            avatarRoot = (GameObject)EditorGUILayout.ObjectField("Avatar Root", avatarRoot, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck())
                Repaint();

            if (avatarRoot == null)
            {
                NekoUI.FieldLabel("Pick your avatar, or just select it in the Hierarchy - it will be picked up automatically.");
            }
            else if (avatarRoot.GetComponent<VRCAvatarDescriptor>() == null)
            {
                NekoUI.Notice("This object has no VRC Avatar Descriptor.", NekoUI.Bad);
            }
            else if (avatarRoot.transform.Find(LegacyRootName) != null)
            {
                NekoUI.Notice(
                    "This avatar still has a v1 combined install (" + LegacyRootName + "). Installing either " +
                    "tool below will not remove it - delete that object first to avoid two FX layers fighting.",
                    NekoUI.Warn);
            }

            NekoUI.EndCardBody();
            NekoUI.EndCard();
        }

        private void DrawToolCards()
        {
            NekoUI.BeginCard("Installers");
            NekoUI.BeginCardBody();

            NekoUI.FieldLabel(
                "These are fully independent. Install one, the other, or both - each gets its own FX layer, " +
                "its own generated folder and its own radial menu toggle.");

            GUILayout.Space(6f);

            DrawToolTile(
                "Avatar Intro",
                "Spawn-in effect. Nine particle presets including hearts rising from your feet, " +
                "diamonds, star bursts and falling sakura, plus optional boot audio and ScreenFX.",
                new[] { "9 presets", "Boot chime", "ScreenFX" },
                NekoUI.AccentA,
                IsInstalled(IntroRootName),
                NekoIntroInstaller.Open);

            GUILayout.Space(8f);

            DrawToolTile(
                "AFK Idle Audio",
                "Looping music while you are AFK, with a tuned distance falloff - full and bassy up " +
                "close, quiet and soft across the room. Includes a short loading phase.",
                new[] { "Falloff curve", "Loading phase", "Low-pass" },
                NekoUI.AccentB,
                IsInstalled(IdleRootName),
                NekoAfkIdleInstaller.Open);

            NekoUI.EndCardBody();
            NekoUI.EndCard();
        }

        /// <summary>One launcher tile: accent stripe, title, blurb, feature chips and an open button.</summary>
        private void DrawToolTile(
            string title,
            string description,
            string[] chips,
            Color accent,
            bool installed,
            Action open)
        {
            const float Height = 118f;

            Rect tile = GUILayoutUtility.GetRect(0, int.MaxValue, Height, Height);
            bool hover = tile.Contains(Event.current.mousePosition);

            NekoUI.RoundedBox(tile, hover ? NekoUI.CardHover : new Color(1f, 1f, 1f, 0.035f), 8f, 1f,
                hover ? new Color(accent.r, accent.g, accent.b, 0.55f) : NekoUI.Line);

            // Accent stripe down the left edge.
            NekoUI.RoundedBox(new Rect(tile.x + 1f, tile.y + 8f, 3f, tile.height - 16f), accent, 1.5f);

            GUI.Label(new Rect(tile.x + 16f, tile.y + 8f, tile.width - 130f, 20f), title,
                new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 14,
                    normal = { textColor = Color.white }
                });

            if (installed)
            {
                var badge = new Rect(tile.x + 16f + GUI.skin.label.CalcSize(new GUIContent(title)).x + 14f,
                    tile.y + 11f, 62f, 15f);
                NekoUI.RoundedBox(badge, new Color(NekoUI.Good.r, NekoUI.Good.g, NekoUI.Good.b, 0.22f), 7f);
                GUI.Label(badge, "INSTALLED", new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 8,
                    normal = { textColor = NekoUI.Good }
                });
            }

            GUI.Label(new Rect(tile.x + 16f, tile.y + 28f, tile.width - 32f, 40f), description, NekoUI.Muted);

            // Feature chips
            float chipX = tile.x + 16f;
            foreach (string chip in chips)
            {
                var size = EditorStyles.miniLabel.CalcSize(new GUIContent(chip));
                var chipRect = new Rect(chipX, tile.yMax - 36f, size.x + 14f, 17f);

                if (chipRect.xMax > tile.xMax - 120f) break;

                NekoUI.RoundedBox(chipRect, new Color(accent.r, accent.g, accent.b, 0.16f), 8f);
                GUI.Label(chipRect, chip, new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = NekoUI.Lighten(accent, 0.25f) }
                });

                chipX = chipRect.xMax + 5f;
            }

            var button = new Rect(tile.xMax - 116f, tile.yMax - 40f, 100f, 28f);
            bool buttonHover = button.Contains(Event.current.mousePosition);

            NekoUI.GradientBox(button, accent, NekoUI.Lighten(accent, buttonHover ? 0.16f : 0.02f), 6f);
            GUI.Label(button, installed ? "Reconfigure" : "Open", new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                normal = { textColor = Color.white }
            });

            EditorGUIUtility.AddCursorRect(tile, MouseCursor.Link);
            if (GUI.Button(tile, GUIContent.none, GUIStyle.none))
                open();
        }

        private bool IsInstalled(string rootName)
        {
            return avatarRoot != null && avatarRoot.transform.Find(rootName) != null;
        }

        private void DrawEnvironmentCard()
        {
            NekoUI.BeginCard("Project");
            NekoUI.BeginCardBody();

            DrawStatusRow("VRCFury", NekoInstallerCore.HasVRCFury);
            DrawStatusRow("Modular Avatar", NekoInstallerCore.HasModularAvatar);
            DrawStatusRow("Doppel Dope Shader", NekoInstallerCore.FindDoppelShader() != null, optional: true);

            if (!NekoInstallerCore.HasVRCFury && !NekoInstallerCore.HasModularAvatar)
            {
                GUILayout.Space(4f);
                NekoUI.Notice(
                    "Neither VRCFury nor Modular Avatar was found. One of them is required - both merge the " +
                    "generated controller without touching your avatar's own FX layer.",
                    NekoUI.Bad);
            }

            NekoUI.EndCardBody();
            NekoUI.EndCard();
        }

        private void DrawStatusRow(string label, bool present, bool optional = false)
        {
            Rect row = GUILayoutUtility.GetRect(0, int.MaxValue, 20f, 20f);

            Color color = present ? NekoUI.Good : (optional ? NekoUI.TextMuted : NekoUI.Warn);
            NekoUI.RoundedBox(new Rect(row.x + 2f, row.y + 7f, 7f, 7f), color, 3.5f);

            GUI.Label(new Rect(row.x + 16f, row.y, row.width - 100f, row.height), label, NekoUI.Body);

            GUI.Label(new Rect(row.xMax - 96f, row.y, 90f, row.height),
                present ? "Detected" : (optional ? "Not installed" : "Missing"),
                new GUIStyle(NekoUI.Muted) { alignment = TextAnchor.MiddleRight });
        }
    }
}
#endif
