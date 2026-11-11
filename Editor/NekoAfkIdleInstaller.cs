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
    /// Installs the AFK sequence: a short loading phase with visuals, then looping idle audio with
    /// a tuned distance falloff - full and bassy up close, quiet and soft across the room.
    ///
    /// Independent of the Avatar Intro installer: separate root, generated folder and controller.
    /// </summary>
    public class NekoAfkIdleInstaller : EditorWindow
    {
        private const string RootName = "NekoSune_IdleOnly";
        private const string FolderName = "Idle";
        private const string AudioObjectName = "AvatarAudioSource";
        private const string LoadingParticleObjectName = "LoadingParticles";
        private const string LoadingTimerObjectName = "LoadingTimer";
        private const string EnableParameter = "NekoIdleEnable";
        private const string AfkParameter = "AFK";

        [SerializeField] private GameObject avatarRoot;
        [SerializeField] private InstallBackend backend = InstallBackend.VRCFury;

        [SerializeField] private List<AudioClip> playlist = new List<AudioClip>();
        [SerializeField] private bool loopPlaylist = true;
        [SerializeField, Range(0f, 1f)] private float volume = 0.55f;

        [SerializeField, Range(0.1f, 10f)] private float nearRadius = 1.5f;
        [SerializeField, Range(2f, 40f)] private float farRadius = 18f;
        [SerializeField, Range(0f, 1f)] private float closeVolume = 0.85f;
        [SerializeField, Range(0f, 1f)] private float farVolume = 0.08f;
        [SerializeField, Range(0f, 1f)] private float body = 0.55f;
        [SerializeField, Range(300f, 22000f)] private float lowPassAtFar = 1200f;

        [SerializeField, Range(0.1f, 10f)] private float loadingDuration = 1.5f;
        [SerializeField] private bool createLoadingParticles = true;
        [SerializeField] private IntroPreset loadingPreset = IntroPreset.DigitalBoot;
        [SerializeField, Range(0f, 1f)] private float loadingIntensity = 0.4f;
        [SerializeField] private GameObject extraLoadingObject;

        [SerializeField] private bool useDoppel;
        [SerializeField] private NekoInstallerCore.DoppelRendererMode doppelRenderer =
            NekoInstallerCore.DoppelRendererMode.ParticleSystem;

        [SerializeField] private bool createMenuToggle = true;
        [SerializeField] private string menuFolderName = "Neko Audio";
        [SerializeField] private bool toggleDefaultOn = true;
        [SerializeField] private bool toggleSaved = true;

        private Vector2 scroll;
        private bool installQueued;
        private bool showCurve = true;

        [MenuItem("Tools/NekoSuneVR/AFK Idle Audio Installer", false, 22)]
        public static void Open()
        {
            var window = GetWindow<NekoAfkIdleInstaller>(true, "AFK Idle Audio", true);
            window.minSize = new Vector2(520, 760);
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

        private NekoInstallerCore.FalloffSettings Falloff
        {
            get
            {
                return new NekoInstallerCore.FalloffSettings
                {
                    NearRadius = nearRadius,
                    FarRadius = farRadius,
                    CloseVolume = closeVolume,
                    FarVolume = farVolume,
                    Body = body,
                    LowPassAtFar = lowPassAtFar
                };
            }
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

            NekoPackageChrome.Header("AFK Idle Audio", "Looping idle music with tuned distance falloff");
            NekoPackageChrome.UpdateBanner();

            DrawAvatarCard();
            DrawAudioCard();
            DrawFalloffCard();
            DrawLoadingCard();
            DrawMenuCard();
            DrawValidation();

            GUILayout.Space(6f);
            if (NekoUI.PrimaryButton(installQueued ? "Installing..." : "Install AFK Idle Audio", 42f,
                    CanInstall() && !installQueued))
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

        private List<AudioClip> ValidTracks()
        {
            return playlist.Where(c => c != null).ToList();
        }

        private void DrawAudioCard()
        {
            NekoUI.BeginCard("Idle Playlist");
            NekoUI.BeginCardBody();

            NekoUI.FieldLabel(
                "Songs play one after another while you are AFK, then start over. Drop several " +
                "clips in at once, or add them one at a time.");

            GUILayout.Space(4f);
            DrawPlaylistDropArea();
            GUILayout.Space(4f);

            int removeAt = -1;
            for (int i = 0; i < playlist.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label((i + 1).ToString(), NekoUI.Muted, GUILayout.Width(18f));

                    playlist[i] = (AudioClip)EditorGUILayout.ObjectField(
                        playlist[i], typeof(AudioClip), false);

                    if (playlist[i] != null)
                    {
                        GUILayout.Label(
                            FormatTime(playlist[i].length), NekoUI.Muted, GUILayout.Width(44f));
                    }

                    using (new EditorGUI.DisabledScope(i == 0))
                    {
                        if (GUILayout.Button("▲", EditorStyles.miniButtonLeft, GUILayout.Width(22f)))
                        {
                            var moved = playlist[i];
                            playlist[i] = playlist[i - 1];
                            playlist[i - 1] = moved;
                        }
                    }

                    using (new EditorGUI.DisabledScope(i == playlist.Count - 1))
                    {
                        if (GUILayout.Button("▼", EditorStyles.miniButtonMid, GUILayout.Width(22f)))
                        {
                            var moved = playlist[i];
                            playlist[i] = playlist[i + 1];
                            playlist[i + 1] = moved;
                        }
                    }

                    if (GUILayout.Button("✕", EditorStyles.miniButtonRight, GUILayout.Width(22f)))
                        removeAt = i;
                }
            }

            if (removeAt >= 0)
                playlist.RemoveAt(removeAt);

            GUILayout.Space(4f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (NekoUI.GhostButton("Add Track", 96f))
                    playlist.Add(null);

                GUILayout.Space(5f);

                if (NekoUI.GhostButton("Add Selected", 110f))
                    AddClips(Selection.GetFiltered<AudioClip>(SelectionMode.DeepAssets).ToList());

                if (playlist.Count > 0)
                {
                    GUILayout.Space(5f);
                    if (NekoUI.GhostButton("Clear", 70f))
                        playlist.Clear();
                }

                GUILayout.FlexibleSpace();
            }

            var tracks = ValidTracks();
            if (tracks.Count > 1)
            {
                float total = tracks.Sum(c => c.length);
                NekoUI.FieldLabel(string.Format(
                    "{0} tracks, {1} total.", tracks.Count, FormatTime(total)));
            }

            GUILayout.Space(4f);

            loopPlaylist = NekoUI.Toggle("Loop Playlist", loopPlaylist,
                "On: the playlist starts again after the last track. Off: it plays through once " +
                "and then goes quiet until you leave AFK and come back.");

            volume = EditorGUILayout.Slider(
                new GUIContent("Master Volume", "Overall level. The falloff curve scales this by distance."),
                volume, 0f, 1f);

            NekoUI.EndCardBody();
            NekoUI.EndCard();
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.RoundToInt(seconds);
            return string.Format("{0}:{1:00}", total / 60, total % 60);
        }

        private void DrawPlaylistDropArea()
        {
            Rect area = GUILayoutUtility.GetRect(0, int.MaxValue, 30f, 30f);

            bool hover = area.Contains(Event.current.mousePosition);
            NekoUI.RoundedBox(area, hover ? NekoUI.CardHover : new Color(1f, 1f, 1f, 0.04f), 6f, 1f, NekoUI.Line);

            GUI.Label(area, "Drop audio clips here", new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = NekoUI.TextMuted }
            });

            Event evt = Event.current;
            if (!hover) return;

            if (evt.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = DragAndDrop.objectReferences.Any(o => o is AudioClip)
                    ? DragAndDropVisualMode.Copy
                    : DragAndDropVisualMode.Rejected;
                evt.Use();
            }
            else if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                AddClips(DragAndDrop.objectReferences.OfType<AudioClip>().ToList());
                evt.Use();
            }
        }

        private void AddClips(List<AudioClip> clips)
        {
            foreach (var clip in clips)
            {
                if (clip != null && !playlist.Contains(clip))
                    playlist.Add(clip);
            }

            Repaint();
        }

        private void DrawFalloffCard()
        {
            NekoUI.BeginCard("Distance Falloff");
            NekoUI.BeginCardBody();

            NekoUI.FieldLabel(
                "Full, bassy level inside the Near Radius. Past it the volume drops toward Far Volume " +
                "and the highs roll off, so people across the room hear it quietly and softly rather " +
                "than at full brightness.");

            GUILayout.Space(4f);

            nearRadius = EditorGUILayout.Slider(
                new GUIContent("Near Radius", "Metres where the audio still plays at full Close Volume."),
                nearRadius, 0.1f, 10f);

            farRadius = EditorGUILayout.Slider(
                new GUIContent("Far Radius", "Metres where the audio reaches Far Volume and stops getting quieter."),
                farRadius, 2f, 40f);

            if (farRadius <= nearRadius)
                farRadius = nearRadius + 0.5f;

            closeVolume = EditorGUILayout.Slider("Close Volume", closeVolume, 0f, 1f);
            farVolume = EditorGUILayout.Slider("Far Volume", farVolume, 0f, 1f);

            if (farVolume > closeVolume)
                farVolume = closeVolume;

            body = EditorGUILayout.Slider(
                new GUIContent("Close-Range Body",
                    "Widens the VRChat volumetric radius and lifts the low-pass resonance so the sound " +
                    "feels like it is around your head instead of coming from a point. This shapes " +
                    "presence, not gain - it is not an EQ band."),
                body, 0f, 1f);

            lowPassAtFar = EditorGUILayout.Slider(
                new GUIContent("Low Pass At Far", "Cutoff in Hz at the Far Radius. Lower is more muffled at distance."),
                lowPassAtFar, 300f, 22000f);

            GUILayout.Space(4f);
            showCurve = EditorGUILayout.Foldout(showCurve, "Preview", true);
            if (showCurve)
                DrawFalloffPreview();

            NekoUI.EndCardBody();
            NekoUI.EndCard();
        }

        private void DrawFalloffPreview()
        {
            Rect area = GUILayoutUtility.GetRect(0, int.MaxValue, 92f, 92f);
            NekoUI.RoundedBox(area, new Color(0f, 0f, 0f, 0.35f), 6f, 1f, NekoUI.Line);

            if (Event.current.type != EventType.Repaint)
                return;

            var curve = NekoInstallerCore.BuildRolloffCurve(Falloff);

            // Near-radius marker
            float plateauX = area.x + area.width * Mathf.Clamp01(nearRadius / farRadius);
            EditorGUI.DrawRect(new Rect(plateauX, area.y + 4f, 1f, area.height - 8f), new Color(1f, 1f, 1f, 0.18f));

            Handles.BeginGUI();
            Handles.color = NekoUI.AccentB;

            Vector3 previous = Vector3.zero;
            for (int i = 0; i <= 90; i++)
            {
                float t = i / 90f;
                var point = new Vector3(
                    area.x + area.width * t,
                    area.yMax - (area.height - 14f) * Mathf.Clamp01(curve.Evaluate(t)) - 6f);

                if (i > 0)
                    Handles.DrawAAPolyLine(2.5f, previous, point);
                previous = point;
            }

            Handles.EndGUI();

            GUI.Label(new Rect(area.x + 6f, area.y + 2f, 120f, 14f),
                "0 m  -  full", NekoUI.Muted);
            GUI.Label(new Rect(area.xMax - 96f, area.y + 2f, 90f, 14f),
                farRadius.ToString("0.#") + " m  -  quiet",
                new GUIStyle(NekoUI.Muted) { alignment = TextAnchor.MiddleRight });
        }

        private void DrawLoadingCard()
        {
            NekoUI.BeginCard("AFK Loading Phase");
            NekoUI.BeginCardBody();

            NekoUI.FieldLabel("A short visual beat between going AFK and the idle music starting.");
            GUILayout.Space(4f);

            loadingDuration = EditorGUILayout.Slider("Length", loadingDuration, 0.1f, 10f);

            createLoadingParticles = NekoUI.Toggle("Loading Particles", createLoadingParticles);

            using (new EditorGUI.DisabledScope(!createLoadingParticles))
            {
                loadingPreset = (IntroPreset)EditorGUILayout.EnumPopup("Preset", loadingPreset);
                loadingIntensity = EditorGUILayout.Slider("Intensity", loadingIntensity, 0f, 1f);
            }

            extraLoadingObject = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent("Extra Object", "An object on your avatar enabled only while entering AFK."),
                extraLoadingObject, typeof(GameObject), true);

            GUILayout.Space(4f);

            bool doppelAvailable = NekoInstallerCore.FindDoppelShader() != null;
            using (new EditorGUI.DisabledScope(!doppelAvailable))
            {
                useDoppel = NekoUI.Toggle("Doppel ScreenFX", useDoppel && doppelAvailable);
                if (useDoppel)
                {
                    doppelRenderer = (NekoInstallerCore.DoppelRendererMode)EditorGUILayout.EnumPopup(
                        "Renderer", doppelRenderer);
                }
            }

            if (!doppelAvailable)
                NekoUI.FieldLabel("Doppel Dope Shader not detected in this project.");

            NekoUI.EndCardBody();
            NekoUI.EndCard();
        }

        private void DrawMenuCard()
        {
            NekoUI.BeginCard("Radial Menu");
            NekoUI.BeginCardBody();

            createMenuToggle = NekoUI.Toggle("Menu Toggle", createMenuToggle,
                "Adds an ON/OFF toggle for the idle audio to your avatar's radial menu.");

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

            if (ValidTracks().Count == 0)
                NekoUI.Notice("Add at least one song to the playlist.", NekoUI.Warn);

            if (extraLoadingObject != null &&
                !NekoInstallerCore.IsDescendantOrSelf(avatarRoot.transform, extraLoadingObject.transform))
                NekoUI.Notice("Extra Object must be inside the selected avatar hierarchy.", NekoUI.Bad);

            if (!NekoInstallerCore.BackendAvailable(backend))
                NekoUI.Notice(backend + " is not installed, or its API is unavailable.", NekoUI.Bad);
            else if (CanInstall())
                NekoUI.Notice("Ready to install using " + backend + ".", NekoUI.Good);
        }

        private bool CanInstall()
        {
            if (avatarRoot == null || ValidTracks().Count == 0) return false;
            if (avatarRoot.GetComponent<VRCAvatarDescriptor>() == null) return false;
            if (!NekoInstallerCore.BackendAvailable(backend)) return false;

            if (extraLoadingObject != null &&
                !NekoInstallerCore.IsDescendantOrSelf(avatarRoot.transform, extraLoadingObject.transform))
                return false;

            return true;
        }

        // ------------------------------------------------------------------
        // Install
        // ------------------------------------------------------------------

        private void QueueInstall()
        {
            if (installQueued) return;

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
                EditorUtility.DisplayDialog("AFK Idle Audio Installer",
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
            Undo.SetCurrentGroupName("Install AFK Idle Audio");

            try
            {
                var old = avatarRoot.transform.Find(RootName);
                if (old != null)
                    Undo.DestroyObjectImmediate(old.gameObject);

                // Clean the v1.0 root name so upgrading does not leave a duplicate controller.
                var legacy = avatarRoot.transform.Find("NekoSune_IdleAudio");
                if (legacy != null)
                    Undo.DestroyObjectImmediate(legacy.gameObject);

                string outputFolder = NekoInstallerCore.OutputFolder(avatarRoot, FolderName);
                NekoInstallerCore.RecreateFolder(outputFolder);

                var root = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(root, "Create Idle Root");
                root.transform.SetParent(avatarRoot.transform, false);

                var audioObject = NekoInstallerCore.CreateAudioObject(
                    root.transform, AudioObjectName, volume, Falloff, true);

                var timer = new GameObject(LoadingTimerObjectName);
                Undo.RegisterCreatedObjectUndo(timer, "Create Loading Timer");
                timer.transform.SetParent(root.transform, false);
                timer.SetActive(false);

                GameObject particles = null;
                if (createLoadingParticles)
                    particles = CreateLoadingParticles(root.transform, outputFolder);

                GameObject doppel = null;
                if (useDoppel)
                {
                    var shader = NekoInstallerCore.FindDoppelShader();
                    if (shader != null)
                    {
                        doppel = NekoInstallerCore.CreateDoppelEffect(
                            root.transform, outputFolder, shader, "Doppel_AFKLoadingFX",
                            NekoInstallerCore.DoppelPreset.AFKLoading, doppelRenderer);
                    }
                    else
                    {
                        Debug.LogWarning("[NekoSuneVR] Doppel was requested but the Dope Shader was not found.");
                    }
                }

                var managed = new List<GameObject> { timer, particles, extraLoadingObject, doppel };

                var normalClip = NekoInstallerCore.CreatePhaseClip(
                    avatarRoot, "NekoIdle_Normal", 0.25f, managed);
                AssetDatabase.CreateAsset(normalClip, outputFolder + "/NekoIdle_Normal.anim");

                var loadingClip = NekoInstallerCore.CreatePhaseClip(
                    avatarRoot, "NekoIdle_Loading", loadingDuration, managed,
                    timer, particles, extraLoadingObject, doppel);
                AssetDatabase.CreateAsset(loadingClip, outputFolder + "/NekoIdle_Loading.anim");

                var tracks = ValidTracks();

                // One clip per track, each exactly as long as its song. The animator uses that
                // length as the state's duration, which is what lets the playlist advance on its
                // own - there is no other way to know when a song has finished.
                var trackClips = new List<AnimationClip>();
                for (int i = 0; i < tracks.Count; i++)
                {
                    var trackClip = NekoInstallerCore.CreatePhaseClip(
                        avatarRoot, "NekoIdle_Track" + (i + 1), Mathf.Max(0.25f, tracks[i].length), managed);

                    AssetDatabase.CreateAsset(trackClip, outputFolder + "/NekoIdle_Track" + (i + 1) + ".anim");
                    trackClips.Add(trackClip);
                }

                AssetDatabase.SaveAssets();

                string controllerPath = outputFolder + "/NekoIdle_FX.controller";
                var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                BuildController(controller, normalClip, loadingClip, trackClips, tracks, audioObject);

                var toggles = new List<NekoInstallerCore.MenuToggle>();
                if (createMenuToggle)
                    toggles.Add(new NekoInstallerCore.MenuToggle { Parameter = EnableParameter, Label = "Idle Audio" });

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

                EditorUtility.DisplayDialog(
                    "AFK Idle Audio Installed",
                    "Done!\n\n" +
                    "1. AFK turns ON  ->  loading visuals\n" +
                    "2. Loading ends  ->  playlist starts\n" +
                    "3. Each song ends ->  next one plays" + (loopPlaylist ? ", looping forever" : ", stopping after the last") + "\n" +
                    "4. Leaving AFK   ->  audio stops, visuals off\n\n" +
                    string.Format("Playlist: {0} track(s), {1} total.\n",
                        tracks.Count, FormatTime(tracks.Sum(c => c.length))) +
                    string.Format("Falloff: {0:0.#} m full ({1:P0})  ->  {2:0.#} m quiet ({3:P0}), low pass to {4:0} Hz.\n\n",
                        nearRadius, closeVolume, farRadius, farVolume, lowPassAtFar) +
                    "Backend: " + backend +
                    (createMenuToggle ? "\nMenu toggle: " + EnableParameter : ""),
                    "Nice!");
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                throw;
            }
        }

        private GameObject CreateLoadingParticles(Transform parent, string outputFolder)
        {
            var go = new GameObject(LoadingParticleObjectName);
            Undo.RegisterCreatedObjectUndo(go, "Create Loading Particles");
            go.transform.SetParent(parent, false);

            var ps = Undo.AddComponent<ParticleSystem>(go);
            var info = NekoParticlePresets.Get(loadingPreset);

            NekoParticlePresets.Configure(ps, info, loadingDuration, loadingIntensity, 1f, false);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = NekoParticlePresets.CreateMaterial(info, outputFolder);

            go.SetActive(false);
            return go;
        }

        private void BuildController(
            AnimatorController controller,
            AnimationClip normalClip,
            AnimationClip loadingClip,
            List<AnimationClip> trackClips,
            List<AudioClip> tracks,
            GameObject audioObject)
        {
            controller.AddParameter(AfkParameter, AnimatorControllerParameterType.Bool);

            if (createMenuToggle)
                NekoInstallerCore.AddBoolParameter(controller, EnableParameter, toggleDefaultOn);

            var sm = controller.layers[0].stateMachine;
            sm.name = "NekoSune AFK Idle Audio";

            foreach (var child in sm.states.ToArray())
                sm.RemoveState(child.state);

            var normal = sm.AddState("Normal", new Vector3(300f, 0f, 0f));
            normal.motion = normalClip;
            normal.writeDefaultValues = true;
            sm.defaultState = normal;

            var loading = sm.AddState("AFK Loading", new Vector3(600f, 0f, 0f));
            loading.motion = loadingClip;
            loading.writeDefaultValues = true;

            // One state per song, chained so each hands over when its clip ends.
            string audioPath = NekoInstallerCore.GetPathFromAvatarRoot(avatarRoot, audioObject.transform);
            var trackStates = new List<AnimatorState>();

            for (int i = 0; i < tracks.Count; i++)
            {
                var state = sm.AddState(
                    "Track " + (i + 1) + " - " + tracks[i].name,
                    new Vector3(880f, i * 70f, 0f));

                state.motion = trackClips[i];
                state.writeDefaultValues = true;

                // Never loop an individual track: looping is the playlist advancing, not one
                // song repeating.
                NekoInstallerCore.AddPlayAudioBehaviour(state, audioPath, tracks[i], volume, false);
                trackStates.Add(state);
            }

            var idle = trackStates[0];

            for (int i = 0; i < trackStates.Count; i++)
            {
                bool last = i == trackStates.Count - 1;
                if (last && !loopPlaylist)
                    continue;

                var next = trackStates[(i + 1) % trackStates.Count];

                var advance = trackStates[i].AddTransition(next);
                advance.hasExitTime = true;
                advance.exitTime = 1f;
                advance.hasFixedDuration = true;
                advance.duration = 0f;
                advance.canTransitionToSelf = false;
                advance.AddCondition(AnimatorConditionMode.If, 0f, AfkParameter);
            }

            var toLoading = normal.AddTransition(loading);
            toLoading.hasExitTime = false;
            toLoading.hasFixedDuration = true;
            toLoading.duration = 0f;
            toLoading.canTransitionToSelf = false;
            toLoading.AddCondition(AnimatorConditionMode.If, 0f, AfkParameter);
            if (createMenuToggle)
                toLoading.AddCondition(AnimatorConditionMode.If, 0f, EnableParameter);

            NekoInstallerCore.AddTransition(loading, normal, AnimatorConditionMode.IfNot, AfkParameter);

            var finishLoading = loading.AddTransition(idle);
            finishLoading.hasExitTime = true;
            finishLoading.exitTime = 1f;
            finishLoading.hasFixedDuration = true;
            finishLoading.duration = 0f;
            finishLoading.AddCondition(AnimatorConditionMode.If, 0f, AfkParameter);

            // Leaving AFK drops out of whichever song happens to be playing.
            foreach (var state in trackStates)
            {
                NekoInstallerCore.AddTransition(state, normal, AnimatorConditionMode.IfNot, AfkParameter);

                if (createMenuToggle)
                {
                    // A separate transition, so it acts as an OR with the AFK condition above.
                    NekoInstallerCore.AddTransition(state, normal, AnimatorConditionMode.IfNot, EnableParameter);
                }
            }

            if (createMenuToggle)
                NekoInstallerCore.AddTransition(loading, normal, AnimatorConditionMode.IfNot, EnableParameter);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
