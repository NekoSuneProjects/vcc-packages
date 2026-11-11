#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using NekoSuneVR.VRChatIdleAudio.UI;
using VRC.SDK3.Avatars.Components;

namespace NekoSuneVR.VRChatIdleAudio
{
    /// <summary>
    /// Installs the avatar spawn intro: a particle preset, optional boot audio and optional
    /// Doppel ScreenFX, merged as its own FX layer with its own radial menu toggle.
    ///
    /// Completely independent of the AFK Idle installer - separate root object, separate
    /// generated folder, separate controller - so either can be installed alone or both together.
    /// </summary>
    public class NekoIntroInstaller : EditorWindow
    {
        private const string RootName = "NekoSune_IntroOnly";
        private const string FolderName = "Intro";
        private const string AudioObjectName = "IntroAudioSource";
        private const string ParticleObjectName = "IntroParticles";
        private const string TimerObjectName = "IntroTimer";
        private const string EnableParameter = "NekoIntroEnable";

        [SerializeField] private GameObject avatarRoot;
        [SerializeField] private InstallBackend backend = InstallBackend.VRCFury;

        [SerializeField] private IntroPreset preset = IntroPreset.HeartsRising;
        [SerializeField, Range(0.25f, 300f)] private float introDuration = 3f;
        [SerializeField, Range(0f, 1f)] private float intensity = 0.5f;
        [SerializeField, Range(0.4f, 2.5f)] private float particleScale = 1f;
        [SerializeField] private bool worldSpaceParticles;
        [SerializeField] private bool createParticles = true;

        [SerializeField] private AudioClip introAudio;
        [SerializeField] private bool generateBootChime = true;
        [SerializeField, Range(0f, 1f)] private float introVolume = 0.65f;
        [SerializeField] private bool matchLengthToAudio = true;
        [SerializeField, Range(0f, 10f)] private float startDelay = 0f;

        [SerializeField] private GameObject extraIntroObject;

        [SerializeField] private bool useDoppel;
        [SerializeField] private NekoInstallerCore.DoppelRendererMode doppelRenderer =
            NekoInstallerCore.DoppelRendererMode.ParticleSystem;

        [SerializeField] private bool createMenuToggle = true;
        [SerializeField] private string menuFolderName = "Neko Audio";
        [SerializeField] private bool toggleDefaultOn = true;
        [SerializeField] private bool toggleSaved = true;

        private Vector2 scroll;
        private bool installQueued;

        [MenuItem("Tools/NekoSuneVR/Avatar Intro Installer", false, 21)]
        public static void Open()
        {
            var window = GetWindow<NekoIntroInstaller>(true, "Avatar Intro", true);
            window.minSize = new Vector2(520, 720);
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

            NekoPackageChrome.Header("Avatar Intro", "Spawn-in particles, boot audio and ScreenFX");
            NekoPackageChrome.UpdateBanner();

            DrawAvatarCard();
            DrawPresetCard();
            DrawTimingCard();
            DrawAudioCard();
            DrawExtrasCard();
            DrawMenuCard();
            DrawValidation();

            GUILayout.Space(6f);
            bool canInstall = CanInstall();
            if (NekoUI.PrimaryButton(installQueued ? "Installing..." : "Install Avatar Intro", 42f, canInstall && !installQueued))
                QueueInstall();

            GUILayout.Space(10f);

            EditorGUILayout.EndScrollView();
            NekoAboutFooter.DrawWithSeparator();
        }

        private void DrawAvatarCard()
        {
            NekoUI.BeginCard("Avatar");
            NekoUI.BeginCardBody();

            avatarRoot = (GameObject)EditorGUILayout.ObjectField("Avatar Root", avatarRoot, typeof(GameObject), true);
            backend = (InstallBackend)EditorGUILayout.EnumPopup("Install Backend", backend);

            NekoUI.EndCardBody();
            NekoUI.EndCard();
        }

        private void DrawPresetCard()
        {
            NekoUI.BeginCard("Particle Preset");
            NekoUI.BeginCardBody();

            DrawPresetGrid();

            GUILayout.Space(6f);

            var info = NekoParticlePresets.Get(preset);
            NekoUI.Notice(info.Description, NekoUI.AccentA);

            GUILayout.Space(4f);
            createParticles = NekoUI.Toggle("Create Particles", createParticles,
                "Turn this off for an audio-only or ScreenFX-only intro.");

            using (new EditorGUI.DisabledScope(!createParticles))
            {
                intensity = EditorGUILayout.Slider(
                    new GUIContent("Intensity", "Emission rate and particle budget."), intensity, 0f, 1f);

                particleScale = EditorGUILayout.Slider(
                    new GUIContent("Scale", "Multiply particle size and spawn radius. Raise this for tall or large avatars."),
                    particleScale, 0.4f, 2.5f);

                worldSpaceParticles = NekoUI.Toggle("World Space Trail", worldSpaceParticles,
                    "On: particles stay where they were emitted, so they trail behind if you move. " +
                    "Off: they follow the avatar. Off is the safer default.");
            }

            NekoUI.EndCardBody();
            NekoUI.EndCard();
        }

        /// <summary>
        /// Preset picker as a grid of preview tiles. Each tile draws the generated sprite over a
        /// gradient of that preset's own colours, so the choice reads visually rather than as a
        /// dropdown of names.
        /// </summary>
        private void DrawPresetGrid()
        {
            const int Columns = 3;
            const float TileHeight = 78f;

            var presets = NekoParticlePresets.All;
            int rows = Mathf.CeilToInt(presets.Length / (float)Columns);

            for (int row = 0; row < rows; row++)
            {
                Rect line = GUILayoutUtility.GetRect(0, int.MaxValue, TileHeight, TileHeight);
                float tileWidth = (line.width - (Columns - 1) * 6f) / Columns;

                for (int column = 0; column < Columns; column++)
                {
                    int index = row * Columns + column;
                    if (index >= presets.Length) break;

                    var info = presets[index];
                    var tile = new Rect(line.x + column * (tileWidth + 6f), line.y, tileWidth, TileHeight - 6f);

                    bool selected = preset == info.Id;
                    bool hover = tile.Contains(Event.current.mousePosition);

                    NekoUI.RoundedBox(tile,
                        selected ? new Color(info.ColorA.r, info.ColorA.g, info.ColorA.b, 0.20f)
                                 : (hover ? NekoUI.CardHover : new Color(1f, 1f, 1f, 0.035f)),
                        7f,
                        selected ? 2f : 1f,
                        selected ? info.ColorA : NekoUI.Line);

                    // Sprite preview, tinted with the preset's own colour ramp.
                    var iconRect = new Rect(tile.x + tile.width * 0.5f - 16f, tile.y + 8f, 32f, 32f);
                    if (Event.current.type == EventType.Repaint)
                    {
                        var texture = NekoParticlePresets.PreviewTexture(info.Shape);
                        GUI.DrawTexture(iconRect, texture, ScaleMode.ScaleToFit, true, 0f,
                            Color.Lerp(info.ColorA, info.ColorB, 0.35f), Vector4.zero, Vector4.zero);
                    }

                    GUI.Label(
                        new Rect(tile.x + 2f, tile.yMax - 26f, tile.width - 4f, 22f),
                        info.Name,
                        new GUIStyle(EditorStyles.miniBoldLabel)
                        {
                            alignment = TextAnchor.MiddleCenter,
                            wordWrap = true,
                            normal = { textColor = selected ? Color.white : NekoUI.TextPrimary }
                        });

                    EditorGUIUtility.AddCursorRect(tile, MouseCursor.Link);
                    if (GUI.Button(tile, GUIContent.none, GUIStyle.none))
                    {
                        preset = info.Id;
                        GUI.changed = true;
                    }
                }
            }
        }

        /// <summary>How long the intro actually runs, honouring Match Length To Audio.</summary>
        private float EffectiveIntroLength()
        {
            if (matchLengthToAudio && introAudio != null)
                return Mathf.Max(0.25f, introAudio.length);

            return introDuration;
        }

        private void DrawTimingCard()
        {
            NekoUI.BeginCard("Timing");
            NekoUI.BeginCardBody();

            startDelay = EditorGUILayout.Slider(
                new GUIContent("Start Delay", "Wait this long after spawning before the intro begins."),
                startDelay, 0f, 10f);

            matchLengthToAudio = NekoUI.Toggle("Match Length To Audio", matchLengthToAudio,
                "Run the intro for exactly as long as the audio clip, so a 10 or 30 second track " +
                "plays through once with the particles and both stop together.");

            using (new EditorGUI.DisabledScope(matchLengthToAudio && introAudio != null))
            {
                introDuration = EditorGUILayout.Slider(
                    new GUIContent("Intro Length", "How long the intro runs before the avatar returns to normal."),
                    introDuration, 0.25f, 300f);
            }

            float length = EffectiveIntroLength();

            GUILayout.Space(4f);
            NekoUI.Separator();
            GUILayout.Space(4f);

            if (matchLengthToAudio && introAudio == null)
            {
                NekoUI.FieldLabel("No clip assigned, so Intro Length is used as-is.");
            }

            NekoUI.Notice(
                string.Format(
                    "0.00s  spawn\n" +
                    "{0:0.00}s  intro starts - audio and particles together\n" +
                    "{1:0.00}s  intro ends - both stop, nothing loops",
                    startDelay, startDelay + length),
                NekoUI.AccentA);

            NekoUI.FieldLabel(
                "Particles finish their own lifetime as they fade, so the last few linger for " +
                "a moment after the intro ends rather than vanishing mid-air.");

            NekoUI.EndCardBody();
            NekoUI.EndCard();
        }

        private void DrawAudioCard()
        {
            NekoUI.BeginCard("Intro Audio");
            NekoUI.BeginCardBody();

            introAudio = (AudioClip)EditorGUILayout.ObjectField(
                new GUIContent("Intro Clip", "Optional. Leave empty to use the generated boot chime."),
                introAudio, typeof(AudioClip), false);

            if (introAudio == null)
            {
                generateBootChime = NekoUI.Toggle("Generate Boot Chime", generateBootChime,
                    "Writes a small original WAV into the generated folder.");
            }

            introVolume = EditorGUILayout.Slider("Volume", introVolume, 0f, 1f);

            NekoUI.EndCardBody();
            NekoUI.EndCard();
        }

        private void DrawExtrasCard()
        {
            NekoUI.BeginCard("Extras");
            NekoUI.BeginCardBody();

            extraIntroObject = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent("Extra Object", "An object already on your avatar, enabled only during the intro."),
                extraIntroObject, typeof(GameObject), true);

            GUILayout.Space(4f);

            bool doppelAvailable = NekoInstallerCore.FindDoppelShader() != null;

            using (new EditorGUI.DisabledScope(!doppelAvailable))
            {
                useDoppel = NekoUI.Toggle("Doppel ScreenFX", useDoppel && doppelAvailable,
                    "Adds a Doppelganger Dope Shader screen effect for the length of the intro.");

                if (useDoppel)
                {
                    doppelRenderer = (NekoInstallerCore.DoppelRendererMode)EditorGUILayout.EnumPopup(
                        "Renderer", doppelRenderer);
                }
            }

            if (!doppelAvailable)
            {
                NekoUI.FieldLabel("Doppel Dope Shader not detected in this project.");
            }

            NekoUI.EndCardBody();
            NekoUI.EndCard();
        }

        private void DrawMenuCard()
        {
            NekoUI.BeginCard("Radial Menu");
            NekoUI.BeginCardBody();

            createMenuToggle = NekoUI.Toggle("Menu Toggle", createMenuToggle,
                "Adds an ON/OFF toggle for the intro to your avatar's radial menu.");

            using (new EditorGUI.DisabledScope(!createMenuToggle))
            {
                menuFolderName = EditorGUILayout.TextField("Submenu Name", menuFolderName);
                toggleDefaultOn = NekoUI.Toggle("Default On", toggleDefaultOn);
                toggleSaved = NekoUI.Toggle("Saved Between Worlds", toggleSaved);
                NekoUI.FieldLabel("Synced parameter cost: 1 bit");
            }

            NekoUI.EndCardBody();
            NekoUI.EndCard();
        }

        private void DrawValidation()
        {
            GUILayout.Space(4f);

            if (avatarRoot == null)
            {
                NekoUI.Notice("Select your VRChat avatar root to continue.", NekoUI.Warn);
                return;
            }

            if (avatarRoot.GetComponent<VRCAvatarDescriptor>() == null)
                NekoUI.Notice("Avatar Root needs a VRC Avatar Descriptor.", NekoUI.Bad);

            if (extraIntroObject != null &&
                !NekoInstallerCore.IsDescendantOrSelf(avatarRoot.transform, extraIntroObject.transform))
                NekoUI.Notice("Extra Object must be inside the selected avatar hierarchy.", NekoUI.Bad);

            if (!NekoInstallerCore.BackendAvailable(backend))
                NekoUI.Notice(backend + " is not installed, or its API is unavailable.", NekoUI.Bad);
            else if (CanInstall())
                NekoUI.Notice("Ready to install using " + backend + ".", NekoUI.Good);
        }

        private bool CanInstall()
        {
            if (avatarRoot == null) return false;
            if (avatarRoot.GetComponent<VRCAvatarDescriptor>() == null) return false;
            if (!NekoInstallerCore.BackendAvailable(backend)) return false;

            if (extraIntroObject != null &&
                !NekoInstallerCore.IsDescendantOrSelf(avatarRoot.transform, extraIntroObject.transform))
                return false;

            return true;
        }

        // ------------------------------------------------------------------
        // Install
        // ------------------------------------------------------------------

        private void QueueInstall()
        {
            if (installQueued) return;

            // Asset creation must not run inside the IMGUI layout pass - an import can reload
            // assemblies mid-frame and corrupt the GUILayout stack.
            installQueued = true;
            Repaint();
            EditorApplication.delayCall += RunQueuedInstall;
        }

        private void RunQueuedInstall()
        {
            installQueued = false;

            try
            {
                Install();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("Avatar Intro Installer",
                    "Install failed. See the Console for details.\n\n" + ex.Message, "OK");
            }
            finally
            {
                Repaint();
            }
        }

        private void Install()
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Install Avatar Intro");

            try
            {
                var old = avatarRoot.transform.Find(RootName);
                if (old != null)
                    Undo.DestroyObjectImmediate(old.gameObject);

                string outputFolder = NekoInstallerCore.OutputFolder(avatarRoot, FolderName);
                NekoInstallerCore.RecreateFolder(outputFolder);

                var root = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(root, "Create Intro Root");
                root.transform.SetParent(avatarRoot.transform, false);

                var audioObject = NekoInstallerCore.CreateAudioObject(
                    root.transform, AudioObjectName, introVolume,
                    NekoInstallerCore.FalloffSettings.Default, false);

                var timer = new GameObject(TimerObjectName);
                Undo.RegisterCreatedObjectUndo(timer, "Create Intro Timer");
                timer.transform.SetParent(root.transform, false);
                timer.SetActive(false);

                GameObject particles = null;
                if (createParticles)
                    particles = CreateParticleObject(root.transform, outputFolder);

                GameObject doppel = null;
                if (useDoppel)
                {
                    var shader = NekoInstallerCore.FindDoppelShader();
                    if (shader != null)
                    {
                        doppel = NekoInstallerCore.CreateDoppelEffect(
                            root.transform, outputFolder, shader, "Doppel_IntroFX",
                            NekoInstallerCore.DoppelPreset.DigitalBoot, doppelRenderer);
                    }
                    else
                    {
                        Debug.LogWarning("[NekoSuneVR] Doppel was requested but the Dope Shader was not found.");
                    }
                }

                AudioClip clip = introAudio;
                if (clip == null && generateBootChime)
                    clip = NekoInstallerCore.GenerateBootChime(outputFolder);

                // Put the clip on the AudioSource as well as on the Play Audio behaviour. VRChat
                // drives playback from the behaviour, but leaving the AudioSource empty makes the
                // component look broken in the Inspector and gives nothing to audition with.
                var source = audioObject.GetComponent<AudioSource>();
                if (source != null)
                {
                    source.clip = clip;
                    source.loop = false;
                    EditorUtility.SetDirty(source);
                }

                float introLength = EffectiveIntroLength();
                var managed = new List<GameObject> { timer, particles, extraIntroObject, doppel };

                // The timer object is what gives these clips their length: every managed object
                // gets a constant curve running to the duration, so the state lasts exactly that
                // long even when particles and extras are all switched off.
                var introClip = NekoInstallerCore.CreatePhaseClip(
                    avatarRoot, "NekoIntro_Active", introLength, managed,
                    timer, particles, extraIntroObject, doppel);
                AssetDatabase.CreateAsset(introClip, outputFolder + "/NekoIntro_Active.anim");

                var normalClip = NekoInstallerCore.CreatePhaseClip(
                    avatarRoot, "NekoIntro_Normal", 0.25f, managed);
                AssetDatabase.CreateAsset(normalClip, outputFolder + "/NekoIntro_Normal.anim");

                AnimationClip delayClip = null;
                if (startDelay > 0.01f)
                {
                    delayClip = NekoInstallerCore.CreatePhaseClip(
                        avatarRoot, "NekoIntro_Delay", startDelay, managed);
                    AssetDatabase.CreateAsset(delayClip, outputFolder + "/NekoIntro_Delay.anim");
                }

                AssetDatabase.SaveAssets();

                string controllerPath = outputFolder + "/NekoIntro_FX.controller";
                var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                BuildController(controller, introClip, normalClip, delayClip, audioObject, clip);

                var toggles = new List<NekoInstallerCore.MenuToggle>();
                if (createMenuToggle)
                    toggles.Add(new NekoInstallerCore.MenuToggle { Parameter = EnableParameter, Label = "Intro" });

                VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu menu = null;
                VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters parameters = null;

                if (toggles.Count > 0)
                {
                    NekoInstallerCore.BuildMenuAssets(
                        outputFolder, menuFolderName, toggles, toggleDefaultOn, toggleSaved,
                        out menu, out parameters);
                }

                NekoInstallerCore.Install(
                    backend, root, controller, menu, parameters, toggles, toggleDefaultOn, toggleSaved);

                foreach (var obj in managed.Where(x => x != null).Distinct())
                    obj.SetActive(false);

                EditorUtility.SetDirty(avatarRoot);
                AssetDatabase.SaveAssets();
                Selection.activeGameObject = root;
                Undo.CollapseUndoOperations(group);

                var info = NekoParticlePresets.Get(preset);
                EditorUtility.DisplayDialog(
                    "Avatar Intro Installed",
                    "Done!\n\n" +
                    "Preset: " + info.Name + "\n" +
                    (startDelay > 0.01f ? "Delay: " + startDelay.ToString("0.##") + "s\n" : "") +
                    "Length: " + introLength.ToString("0.##") + "s" +
                    (matchLengthToAudio && introAudio != null ? "  (matched to the clip)" : "") + "\n" +
                    "Audio: " + (clip != null ? clip.name : "none") + "\n" +
                    "Backend: " + backend +
                    (createMenuToggle ? "\nMenu toggle: " + EnableParameter : "") +
                    "\n\nOn spawn the audio and particles run together for the intro length, " +
                    "then both stop. Nothing loops.",
                    "Nice!");
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                throw;
            }
        }

        private GameObject CreateParticleObject(Transform parent, string outputFolder)
        {
            var go = new GameObject(ParticleObjectName);
            Undo.RegisterCreatedObjectUndo(go, "Create Intro Particles");
            go.transform.SetParent(parent, false);

            var ps = Undo.AddComponent<ParticleSystem>(go);
            var info = NekoParticlePresets.Get(preset);

            // One-shot: emit across the intro and stop, so nothing loops once the intro ends.
            NekoParticlePresets.Configure(
                ps, info, EffectiveIntroLength(), intensity, particleScale, worldSpaceParticles, loop: false);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = NekoParticlePresets.CreateMaterial(info, outputFolder);

            go.SetActive(false);
            return go;
        }

        private void BuildController(
            AnimatorController controller,
            AnimationClip introClip,
            AnimationClip normalClip,
            AnimationClip delayClip,
            GameObject audioObject,
            AudioClip clip)
        {
            if (createMenuToggle)
                NekoInstallerCore.AddBoolParameter(controller, EnableParameter, toggleDefaultOn);

            var sm = controller.layers[0].stateMachine;
            sm.name = "NekoSune Avatar Intro";

            foreach (var child in sm.states.ToArray())
                sm.RemoveState(child.state);

            var normal = sm.AddState("Normal", new Vector3(620f, 0f, 0f));
            normal.motion = normalClip;
            normal.writeDefaultValues = true;
            sm.defaultState = normal;

            var intro = sm.AddState("Intro", new Vector3(340f, -140f, 0f));
            intro.motion = introClip;
            intro.writeDefaultValues = true;

            // Audio starts with the intro state, not at spawn, so a start delay keeps them together.
            if (clip != null)
            {
                NekoInstallerCore.AddPlayAudioBehaviour(
                    intro,
                    NekoInstallerCore.GetPathFromAvatarRoot(avatarRoot, audioObject.transform),
                    clip, introVolume, false);
            }

            // Where the avatar starts: a delay state when one was asked for, otherwise the intro.
            AnimatorState entryState = intro;

            if (delayClip != null)
            {
                var delay = sm.AddState("Start Delay", new Vector3(60f, -140f, 0f));
                delay.motion = delayClip;
                delay.writeDefaultValues = true;

                var toIntro = delay.AddTransition(intro);
                toIntro.hasExitTime = true;
                toIntro.exitTime = 1f;
                toIntro.hasFixedDuration = true;
                toIntro.duration = 0f;

                entryState = delay;
            }

            if (createMenuToggle)
            {
                // Normal stays the default state and the entry transition diverts into the intro
                // only while the toggle is on, so turning it off skips the intro entirely.
                var entry = sm.AddEntryTransition(entryState);
                entry.AddCondition(AnimatorConditionMode.If, 0f, EnableParameter);

                NekoInstallerCore.AddTransition(intro, normal, AnimatorConditionMode.IfNot, EnableParameter);

                if (entryState != intro)
                    NekoInstallerCore.AddTransition(entryState, normal, AnimatorConditionMode.IfNot, EnableParameter);
            }
            else
            {
                sm.defaultState = entryState;
            }

            // Timed exit once the intro clip has played through. The clip is exactly as long as
            // the intro, so this is where the audio and particles both stop - and because there
            // is no path back into the intro, neither can loop.
            var finish = intro.AddTransition(normal);
            finish.hasExitTime = true;
            finish.exitTime = 1f;
            finish.hasFixedDuration = true;
            finish.duration = 0f;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
