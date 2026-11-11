#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NekoSuneVR.BeatKeyframing
{
    /// <summary>
    /// Audio timeline for keyframing to music.
    ///
    /// Shows a peak-envelope waveform plus one lane per frequency band, so the sub/bass energy
    /// that drives a drop is visible at a glance instead of being guessed from a mono waveform.
    /// Detected drops get markers you can jump between, and the whole strip lines up with
    /// Unity's Animation window so the playhead matches your dope sheet.
    /// </summary>
    public class NekoBeatKeyframingWindow : EditorWindow
    {
        private const string PrefPrefix = "NekoBeat.";
        private const string ImportFolder = "Assets/NekoSuneVR/NekoBeatKeyframing/ImportedAudio";

        private static readonly string[] SupportedExtensions =
        {
            ".mp3", ".ogg", ".wav", ".aif", ".aiff", ".mod", ".it", ".s3m", ".xm"
        };

        private AudioClip clip;
        private NekoAudioAnalysis analysis;
        private string analysisError;

        // Playback sync, carried over from the original tool's behaviour.
        private float currentTime;
        private float lastTime;
        private float lastLastTime;
        private bool isPlaying;
        private bool hasStopped;

        // View
        private bool followAnimationWindow = true;
        private float standaloneZoom = 1f;
        private float standaloneScroll;
        private bool showBands = true;
        private bool showDrops = true;
        private bool showBeatGrid = false;
        private bool instantReplay;
        private bool clickToScrub = true;

        private readonly bool[] bandEnabled = { true, true, true, true, true };

        private Color waveformColor = new Color(0.85f, 0.85f, 0.88f);
        private Color backgroundColor = new Color(0.13f, 0.13f, 0.15f);

        private float laneHeight = 26f;
        private float waveformWeight = 0.45f;

        private Vector2 lastMousePosition;
        private bool mouseInside;
        private Rect aboutButtonRect;

        [MenuItem("Tools/NekoSuneVR/Beat Keyframing")]
        public static void Open()
        {
            var window = GetWindow<NekoBeatKeyframingWindow>("Beat Keyframing");
            window.minSize = new Vector2(520, 150);
            window.maxSize = new Vector2(10000, 2000);
            window.Show();
        }

        private void OnEnable()
        {
            wantsMouseMove = true;
            UpdateChecker.Changed += Repaint;

            followAnimationWindow = EditorPrefs.GetBool(PrefPrefix + "Follow", true);
            showBands = EditorPrefs.GetBool(PrefPrefix + "ShowBands", true);
            showDrops = EditorPrefs.GetBool(PrefPrefix + "ShowDrops", true);
            showBeatGrid = EditorPrefs.GetBool(PrefPrefix + "ShowBeatGrid", false);
            instantReplay = EditorPrefs.GetBool(PrefPrefix + "InstantReplay", false);
            clickToScrub = EditorPrefs.GetBool(PrefPrefix + "ClickToScrub", true);
            laneHeight = EditorPrefs.GetFloat(PrefPrefix + "LaneHeight", 26f);

            for (int i = 0; i < bandEnabled.Length; i++)
                bandEnabled[i] = EditorPrefs.GetBool(PrefPrefix + "Band" + i, true);

            waveformColor = ReadColor("Waveform", new Color(0.85f, 0.85f, 0.88f));
            backgroundColor = ReadColor("Background", new Color(0.13f, 0.13f, 0.15f));

            string path = EditorPrefs.GetString(PrefPrefix + "ClipPath", string.Empty);
            if (!string.IsNullOrEmpty(path))
            {
                clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null)
                    Analyse();
            }
        }

        private void OnDisable()
        {
            UpdateChecker.Changed -= Repaint;
            SavePrefs();
            NekoAudioPreview.StopAll();
        }

        private void SavePrefs()
        {
            EditorPrefs.SetBool(PrefPrefix + "Follow", followAnimationWindow);
            EditorPrefs.SetBool(PrefPrefix + "ShowBands", showBands);
            EditorPrefs.SetBool(PrefPrefix + "ShowDrops", showDrops);
            EditorPrefs.SetBool(PrefPrefix + "ShowBeatGrid", showBeatGrid);
            EditorPrefs.SetBool(PrefPrefix + "InstantReplay", instantReplay);
            EditorPrefs.SetBool(PrefPrefix + "ClickToScrub", clickToScrub);
            EditorPrefs.SetFloat(PrefPrefix + "LaneHeight", laneHeight);

            for (int i = 0; i < bandEnabled.Length; i++)
                EditorPrefs.SetBool(PrefPrefix + "Band" + i, bandEnabled[i]);

            WriteColor("Waveform", waveformColor);
            WriteColor("Background", backgroundColor);
        }

        private static Color ReadColor(string key, Color fallback)
        {
            if (!EditorPrefs.HasKey(PrefPrefix + key + "_R"))
                return fallback;

            return new Color(
                EditorPrefs.GetFloat(PrefPrefix + key + "_R", fallback.r),
                EditorPrefs.GetFloat(PrefPrefix + key + "_G", fallback.g),
                EditorPrefs.GetFloat(PrefPrefix + key + "_B", fallback.b),
                EditorPrefs.GetFloat(PrefPrefix + key + "_A", fallback.a));
        }

        private static void WriteColor(string key, Color value)
        {
            EditorPrefs.SetFloat(PrefPrefix + key + "_R", value.r);
            EditorPrefs.SetFloat(PrefPrefix + key + "_G", value.g);
            EditorPrefs.SetFloat(PrefPrefix + key + "_B", value.b);
            EditorPrefs.SetFloat(PrefPrefix + key + "_A", value.a);
        }

        // ------------------------------------------------------------------
        // Playback sync
        // ------------------------------------------------------------------

        private void Update()
        {
            if (clip == null)
            {
                isPlaying = false;
                return;
            }

            if (EditorApplication.isPlaying)
                return;

            lastLastTime = lastTime;
            lastTime = currentTime;
            currentTime = NekoAnimationWindowLink.CurrentTime;

            bool animPlaying = NekoAnimationWindowLink.IsPlaying;
            bool timeMoved = !Mathf.Approximately(lastTime, currentTime);

            // Only redraw when something actually moved. Repainting unconditionally every editor
            // tick keeps the whole Editor busy for a window that is usually just sitting there.
            if (timeMoved || animPlaying || mouseInside)
                Repaint();

            bool inRange = currentTime <= clip.length;

            if (animPlaying && timeMoved && !isPlaying && inRange)
            {
                isPlaying = true;
                hasStopped = false;
                NekoAudioPreview.Play(clip);
                NekoAudioPreview.SeekToTime(clip, currentTime);
            }
            else if (timeMoved && !animPlaying && inRange && instantReplay)
            {
                if (!isPlaying)
                {
                    hasStopped = false;
                    NekoAudioPreview.Play(clip);
                    isPlaying = true;
                }
                NekoAudioPreview.SeekToTime(clip, currentTime);
            }
            else if ((!animPlaying && Mathf.Approximately(lastLastTime, currentTime)) || lastTime > currentTime)
            {
                if (!hasStopped)
                    NekoAudioPreview.StopAll();

                isPlaying = false;
                hasStopped = true;
            }
        }

        // ------------------------------------------------------------------
        // GUI
        // ------------------------------------------------------------------

        private void OnGUI()
        {
            DrawToolbar();

            Rect timeline = GUILayoutUtility.GetRect(
                0, position.width,
                0, Mathf.Max(60f, position.height),
                GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));

            EditorGUI.DrawRect(timeline, backgroundColor);

            HandleDragAndDrop(timeline);

            if (clip == null)
            {
                DrawCentredHint(timeline, "Drag an audio file here\nmp3, ogg, wav, aiff - from the Project window or straight from Explorer");

                // Nothing to visualise yet, so there's room for the full NekoSuneVR footer.
                // Decide on the window height, not the timeline rect: the rect is only valid during
                // Repaint, and the Layout and Repaint passes must draw the same controls.
                if (position.height >= 170f)
                {
                    // Anchored to the window bottom: the timeline rect may extend past the visible area.
                    GUILayout.BeginArea(new Rect(0f, position.height - 40f, position.width, 40f));
                    NekoAboutFooter.Draw();
                    GUILayout.EndArea();
                }
                return;
            }

            if (analysis == null)
            {
                DrawCentredHint(
                    timeline,
                    string.IsNullOrEmpty(analysisError)
                        ? "Press Analyse to read this clip"
                        : "Could not read this clip:\n" + analysisError);
                return;
            }

            DrawTimeline(timeline);
            HandleTimelineInput(timeline);
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUI.BeginChangeCheck();
                var picked = (AudioClip)EditorGUILayout.ObjectField(
                    clip, typeof(AudioClip), false, GUILayout.Width(180));
                if (EditorGUI.EndChangeCheck())
                    SetClip(picked);

                if (GUILayout.Button(
                        new GUIContent("Re-analyse", "Discard the cached result and analyse this clip again."),
                        EditorStyles.toolbarButton, GUILayout.Width(74)))
                    Analyse(true);

                GUILayout.Space(8);

                followAnimationWindow = GUILayout.Toggle(
                    followAnimationWindow,
                    new GUIContent("Follow Anim", "Line the timeline up with Unity's Animation window."),
                    EditorStyles.toolbarButton, GUILayout.Width(80));

                showBands = GUILayout.Toggle(showBands,
                    new GUIContent("Bands", "Show the frequency lanes."),
                    EditorStyles.toolbarButton, GUILayout.Width(50));

                showDrops = GUILayout.Toggle(showDrops,
                    new GUIContent("Drops", "Mark detected bass drops."),
                    EditorStyles.toolbarButton, GUILayout.Width(50));

                showBeatGrid = GUILayout.Toggle(showBeatGrid,
                    new GUIContent("Beats", "Overlay the estimated beat grid."),
                    EditorStyles.toolbarButton, GUILayout.Width(50));

                instantReplay = GUILayout.Toggle(instantReplay,
                    new GUIContent("Scrub Audio", "Play audio while dragging the animation playhead."),
                    EditorStyles.toolbarButton, GUILayout.Width(80));

                GUILayout.Space(8);

                if (analysis != null && analysis.Drops.Count > 0)
                {
                    if (GUILayout.Button(new GUIContent("|< Drop", "Jump to the previous drop"),
                            EditorStyles.toolbarButton, GUILayout.Width(60)))
                        JumpToDrop(-1);

                    if (GUILayout.Button(new GUIContent("Drop >|", "Jump to the next drop"),
                            EditorStyles.toolbarButton, GUILayout.Width(60)))
                        JumpToDrop(1);
                }

                GUILayout.FlexibleSpace();

                if (analysis != null)
                {
                    string tempo = analysis.EstimatedBpm > 0f
                        ? analysis.EstimatedBpm.ToString("0.0") + " BPM (est)"
                        : "BPM unknown";

                    GUILayout.Label(
                        string.Format("{0}  |  {1:0.00}s  |  {2} drops  |  {3}",
                            analysis.ClipName, analysis.Duration, analysis.Drops.Count, tempo),
                        EditorStyles.miniLabel);
                }

                if (GUILayout.Button("Settings", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    ShowSettingsMenu();

                DrawVersionChip();

                if (GUILayout.Button(new GUIContent("NS", "NekoSuneVR - creator of this tool. Click for links."),
                        EditorStyles.toolbarButton, GUILayout.Width(28)))
                    PopupWindow.Show(aboutButtonRect, new NekoAboutPopup());
                if (Event.current.type == EventType.Repaint)
                    aboutButtonRect = GUILayoutUtility.GetLastRect();
            }

            DrawUpdateRow();

            if (!followAnimationWindow && clip != null)
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    GUILayout.Label("Zoom", EditorStyles.miniLabel, GUILayout.Width(36));
                    standaloneZoom = GUILayout.HorizontalSlider(standaloneZoom, 1f, 40f, GUILayout.Width(140));

                    GUILayout.Label("Scroll", EditorStyles.miniLabel, GUILayout.Width(40));
                    float maxScroll = Mathf.Max(0f, 1f - 1f / standaloneZoom);
                    standaloneScroll = Mathf.Clamp(
                        GUILayout.HorizontalSlider(standaloneScroll, 0f, Mathf.Max(0.0001f, maxScroll)),
                        0f, maxScroll);

                    GUILayout.FlexibleSpace();
                    GUILayout.Label("Scroll wheel zooms, middle-drag pans", EditorStyles.miniLabel);
                }
            }
        }

        /// <summary>Toolbar version chip. Amber "v1.0.0 → v1.1.0" when an update is out; click to check now.</summary>
        private static void DrawVersionChip()
        {
            string current = "v" + UpdateChecker.CurrentVersion;
            string text = current, tip;
            bool update = UpdateChecker.Status == UpdateChecker.State.UpdateAvailable;
            switch (UpdateChecker.Status)
            {
                case UpdateChecker.State.Checking: text = current + " ···"; tip = "Checking for updates..."; break;
                case UpdateChecker.State.UpdateAvailable:
                    text = current + " → v" + UpdateChecker.LatestVersion;
                    tip = "Update available: v" + UpdateChecker.LatestVersion + "\nClick to check for updates.";
                    break;
                case UpdateChecker.State.UpToDate: tip = "You're up to date.\nClick to check for updates."; break;
                case UpdateChecker.State.Failed: tip = "Couldn't check for updates (offline?). Click to try again."; break;
                default: tip = "Click to check for updates."; break;
            }

            var style = new GUIStyle(EditorStyles.toolbarButton);
            if (update)
            {
                style.fontStyle = FontStyle.Bold;
                style.normal.textColor = new Color(0.95f, 0.70f, 0.25f);
            }

            if (GUILayout.Button(new GUIContent(text, tip), style, GUILayout.ExpandWidth(false)))
                UpdateChecker.Check();
        }

        /// <summary>Slim toolbar row under the main toolbar, only while an update is waiting.</summary>
        private static void DrawUpdateRow()
        {
            if (!UpdateChecker.ShowBanner) return;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var label = new GUIStyle(EditorStyles.miniBoldLabel);
                label.normal.textColor = new Color(0.95f, 0.70f, 0.25f);
                string notes = string.IsNullOrEmpty(UpdateChecker.Notes) ? string.Empty : "  -  " + UpdateChecker.Notes;
                GUILayout.Label(
                    new GUIContent("Update available: v" + UpdateChecker.LatestVersion + " (you have v" + UpdateChecker.CurrentVersion + ")" + notes,
                        UpdateChecker.Notes),
                    label);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Download update", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false))) UpdateChecker.OpenDownload();
                if (GUILayout.Button("Later", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false))) UpdateChecker.RemindLater();
                if (GUILayout.Button("Skip this version", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false))) UpdateChecker.SkipThisVersion();
            }
        }

        private void ShowSettingsMenu()
        {
            var menu = new GenericMenu();

            for (int i = 0; i < NekoAudioAnalysis.BandSet.Length && i < bandEnabled.Length; i++)
            {
                int index = i;
                var band = NekoAudioAnalysis.BandSet[i];
                menu.AddItem(
                    new GUIContent(string.Format("Lanes/{0}  ({1:0}-{2:0} Hz)", band.Name, band.LowHz, band.HighHz)),
                    bandEnabled[index],
                    () => { bandEnabled[index] = !bandEnabled[index]; SavePrefs(); Repaint(); });
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Click To Scrub"), clickToScrub,
                () => { clickToScrub = !clickToScrub; SavePrefs(); });

            menu.AddSeparator(string.Empty);
            foreach (float height in new[] { 18f, 26f, 36f, 48f })
            {
                float value = height;
                menu.AddItem(new GUIContent("Lane Height/" + height + " px"),
                    Mathf.Approximately(laneHeight, value),
                    () => { laneHeight = value; SavePrefs(); Repaint(); });
            }

            menu.AddSeparator(string.Empty);

            long cacheBytes = NekoAnalysisCache.CacheSizeBytes();
            menu.AddItem(
                new GUIContent(string.Format("Clear Analysis Cache  ({0:0.0} MB)", cacheBytes / 1048576f)),
                false,
                () =>
                {
                    NekoAnalysisCache.Clear();
                    Debug.Log("[NekoBeat] Analysis cache cleared.");
                });

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Reset Colours"), false, () =>
            {
                waveformColor = new Color(0.85f, 0.85f, 0.88f);
                backgroundColor = new Color(0.13f, 0.13f, 0.15f);
                SavePrefs();
                Repaint();
            });

            if (analysis != null)
            {
                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("Log Drop Times"), false, () =>
                {
                    var lines = analysis.Drops.Select(d => string.Format("{0:0.000}s (strength {1:0.00})", d.Time, d.Strength));
                    Debug.Log("[NekoBeat] Drops in " + analysis.ClipName + ":\n" + string.Join("\n", lines.ToArray()));
                });
            }

            menu.ShowAsContext();
        }

        private static void DrawCentredHint(Rect area, string message)
        {
            var style = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                normal = { textColor = new Color(0.65f, 0.65f, 0.7f) }
            };
            GUI.Label(area, message, style);
        }

        // ------------------------------------------------------------------
        // Time <-> pixel mapping
        // ------------------------------------------------------------------

        private float pixelsPerSecond;
        private float originX;

        private void RecalculateMapping(Rect area)
        {
            if (followAnimationWindow && NekoAnimationWindowLink.IsAvailable)
            {
                Rect shown = NekoAnimationWindowLink.ShownArea;
                Rect dope = NekoAnimationWindowLink.DopeSheetRect;
                Vector2 translation = NekoAnimationWindowLink.Translation;

                if (shown.width > 0.0001f && dope.width > 0.0001f)
                {
                    pixelsPerSecond = dope.width / shown.width;
                    originX = dope.x + translation.x;
                    return;
                }
            }

            // Standalone: fit the clip to the window, honouring zoom and scroll.
            float visibleSeconds = analysis.Duration / standaloneZoom;
            pixelsPerSecond = area.width / Mathf.Max(0.0001f, visibleSeconds);
            originX = area.x - standaloneScroll * analysis.Duration * pixelsPerSecond;
        }

        private float TimeToX(float time) => originX + time * pixelsPerSecond;

        private float XToTime(float x) => (x - originX) / Mathf.Max(0.0001f, pixelsPerSecond);

        // ------------------------------------------------------------------
        // Timeline drawing
        // ------------------------------------------------------------------

        private void DrawTimeline(Rect area)
        {
            RecalculateMapping(area);

            var activeBands = new List<int>();
            if (showBands)
            {
                for (int i = 0; i < NekoAudioAnalysis.BandSet.Length && i < bandEnabled.Length; i++)
                {
                    if (bandEnabled[i])
                        activeBands.Add(i);
                }
            }

            float bandsHeight = activeBands.Count * laneHeight;
            float waveHeight = Mathf.Max(30f, area.height - bandsHeight);

            var waveRect = new Rect(area.x, area.y, area.width, waveHeight);

            if (Event.current.type == EventType.Repaint)
            {
                DrawWaveform(waveRect);

                float y = waveRect.yMax;
                foreach (int bandIndex in activeBands)
                {
                    DrawBandLane(new Rect(area.x, y, area.width, laneHeight), bandIndex);
                    y += laneHeight;
                }
            }

            // Labels sit outside the GL block so IMGUI text renders on top.
            float labelY = waveRect.yMax;
            foreach (int bandIndex in activeBands)
            {
                var band = NekoAudioAnalysis.BandSet[bandIndex];
                var labelRect = new Rect(area.x + 4f, labelY + 2f, 70f, 14f);

                EditorGUI.DrawRect(new Rect(labelRect.x - 2f, labelRect.y, 66f, 13f), new Color(0, 0, 0, 0.55f));
                GUI.Label(labelRect, new GUIContent(band.Name, band.Tooltip), MiniLabel(band.Color));
                labelY += laneHeight;
            }

            if (showBeatGrid) DrawBeatGrid(area);
            if (showDrops) DrawDropMarkers(area, waveRect);

            DrawPlayhead(area);
            DrawCursorReadout(area);
        }

        private static GUIStyle MiniLabel(Color color)
        {
            return new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = color } };
        }

        private static Material lineMaterial;

        /// <summary>
        /// GL drawing needs a material bound before any vertices are submitted.
        ///
        /// HandleUtility.ApplyWireMaterial would do this, but it is not public in every Unity
        /// version, so the window builds its own from the built-in Internal-Colored shader -
        /// the same one Unity uses for editor line drawing.
        /// </summary>
        private static void EnsureLineMaterial()
        {
            if (lineMaterial != null)
                return;

            var shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
            {
                Debug.LogWarning("[NekoBeat] Hidden/Internal-Colored not found; the timeline cannot draw.");
                return;
            }

            lineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            lineMaterial.SetInt("_ZWrite", 0);
        }

        private static void BeginGL()
        {
            EnsureLineMaterial();
            if (lineMaterial != null)
                lineMaterial.SetPass(0);

            GL.PushMatrix();
            GL.MultMatrix(GUI.matrix);
        }

        private static void EndGL()
        {
            GL.PopMatrix();
        }

        /// <summary>
        /// Peak envelope: one vertical line per screen pixel spanning the min and max sample in
        /// that pixel's slice of time. Sampling a single value per column - what the original did -
        /// aliases badly and makes a dense track look like noise.
        /// </summary>
        private void DrawWaveform(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.18f));

            float midY = rect.y + rect.height * 0.5f;
            float halfHeight = rect.height * 0.5f - 2f;

            BeginGL();
            GL.Begin(GL.LINES);

            // Centre line
            GL.Color(new Color(1f, 1f, 1f, 0.12f));
            GL.Vertex3(rect.x, midY, 0);
            GL.Vertex3(rect.xMax, midY, 0);

            int first = Mathf.FloorToInt(rect.x);
            int last = Mathf.CeilToInt(rect.xMax);

            for (int x = first; x <= last; x++)
            {
                float t0 = XToTime(x);
                float t1 = XToTime(x + 1);

                if (t1 < 0f || t0 > analysis.Duration)
                    continue;

                int c0 = Mathf.Clamp(Mathf.FloorToInt(t0 / analysis.SecondsPerColumn), 0, analysis.Columns - 1);
                int c1 = Mathf.Clamp(Mathf.CeilToInt(t1 / analysis.SecondsPerColumn), 0, analysis.Columns - 1);

                float min = 0f;
                float max = 0f;
                float rms = 0f;

                for (int c = c0; c <= c1; c++)
                {
                    if (analysis.PeakMin[c] < min) min = analysis.PeakMin[c];
                    if (analysis.PeakMax[c] > max) max = analysis.PeakMax[c];
                    if (analysis.Rms[c] > rms) rms = analysis.Rms[c];
                }

                GL.Color(waveformColor);
                GL.Vertex3(x, midY - max * halfHeight, 0);
                GL.Vertex3(x, midY - min * halfHeight, 0);

                // Brighter RMS core, so sustained loudness reads differently from stray peaks.
                GL.Color(new Color(waveformColor.r, waveformColor.g, waveformColor.b, 0.55f));
                GL.Vertex3(x, midY - rms * halfHeight, 0);
                GL.Vertex3(x, midY + rms * halfHeight, 0);
            }

            GL.End();
            EndGL();
        }

        private void DrawBandLane(Rect rect, int bandIndex)
        {
            var band = NekoAudioAnalysis.BandSet[bandIndex];
            float[] values = analysis.Bands[bandIndex];

            EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.28f));
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), new Color(1f, 1f, 1f, 0.06f));

            BeginGL();
            GL.Begin(GL.LINES);

            int first = Mathf.FloorToInt(rect.x);
            int last = Mathf.CeilToInt(rect.xMax);
            float usable = rect.height - 3f;

            for (int x = first; x <= last; x++)
            {
                float t0 = XToTime(x);
                float t1 = XToTime(x + 1);

                if (t1 < 0f || t0 > analysis.Duration)
                    continue;

                int c0 = Mathf.Clamp(Mathf.FloorToInt(t0 / analysis.SecondsPerColumn), 0, analysis.Columns - 1);
                int c1 = Mathf.Clamp(Mathf.CeilToInt(t1 / analysis.SecondsPerColumn), 0, analysis.Columns - 1);

                float peak = 0f;
                for (int c = c0; c <= c1; c++)
                {
                    if (values[c] > peak) peak = values[c];
                }

                if (peak <= 0.001f)
                    continue;

                float barHeight = peak * usable;

                // Body fades with level; the cap stays solid so quiet detail is still visible.
                GL.Color(new Color(band.Color.r, band.Color.g, band.Color.b, 0.35f + 0.45f * peak));
                GL.Vertex3(x, rect.yMax - 1f, 0);
                GL.Vertex3(x, rect.yMax - 1f - barHeight, 0);

                GL.Color(band.Color);
                GL.Vertex3(x, rect.yMax - 1f - barHeight, 0);
                GL.Vertex3(x, rect.yMax - 2f - barHeight, 0);
            }

            GL.End();
            EndGL();
        }

        private void DrawBeatGrid(Rect area)
        {
            if (analysis.EstimatedBpm <= 0f)
                return;

            float beatSeconds = 60f / analysis.EstimatedBpm;
            if (beatSeconds * pixelsPerSecond < 4f)
                return; // Too dense to read; skip rather than smear the view.

            BeginGL();
            GL.Begin(GL.LINES);

            int beat = 0;
            for (float t = analysis.BeatOffset; t <= analysis.Duration; t += beatSeconds, beat++)
            {
                float x = TimeToX(t);
                if (x < area.x - 2f || x > area.xMax + 2f)
                    continue;

                // Emphasise every fourth beat - that is the bar line you actually key to.
                bool barLine = beat % 4 == 0;
                GL.Color(new Color(1f, 1f, 1f, barLine ? 0.22f : 0.08f));
                GL.Vertex3(x, area.y, 0);
                GL.Vertex3(x, area.yMax, 0);
            }

            GL.End();
            EndGL();
        }

        private void DrawDropMarkers(Rect area, Rect waveRect)
        {
            if (analysis.Drops.Count == 0)
                return;

            foreach (var drop in analysis.Drops)
            {
                float x = TimeToX(drop.Time);
                if (x < area.x - 40f || x > area.xMax + 40f)
                    continue;

                EditorGUI.DrawRect(new Rect(x, area.y, 1f, area.height), new Color(1f, 0.35f, 0.45f, 0.5f));

                var tag = new Rect(x + 2f, waveRect.y + 2f, 46f, 13f);
                EditorGUI.DrawRect(tag, new Color(1f, 0.25f, 0.38f, 0.85f));
                GUI.Label(tag, "DROP", new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    normal = { textColor = Color.white },
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 9
                });
            }
        }

        private void DrawPlayhead(Rect area)
        {
            float x = TimeToX(currentTime);
            if (x < area.x || x > area.xMax)
                return;

            EditorGUI.DrawRect(new Rect(x, area.y, 1f, area.height), Color.white);

            var label = new Rect(x + 3f, area.yMax - 15f, 60f, 13f);
            EditorGUI.DrawRect(label, new Color(0f, 0f, 0f, 0.7f));
            GUI.Label(label, currentTime.ToString("0.000") + "s", EditorStyles.miniLabel);
        }

        private void DrawCursorReadout(Rect area)
        {
            if (!mouseInside)
                return;

            float x = Mathf.Clamp(lastMousePosition.x, area.x, area.xMax);
            float time = XToTime(x);
            if (time < 0f || time > analysis.Duration)
                return;

            EditorGUI.DrawRect(new Rect(x, area.y, 1f, area.height), new Color(1f, 1f, 0.4f, 0.45f));

            var lines = new List<string> { time.ToString("0.000") + "s" };
            for (int i = 0; i < NekoAudioAnalysis.BandSet.Length && i < bandEnabled.Length; i++)
            {
                if (!bandEnabled[i]) continue;
                lines.Add(string.Format("{0} {1,3:0}%",
                    NekoAudioAnalysis.BandSet[i].Name, analysis.SampleBand(i, time) * 100f));
            }

            string text = string.Join("   ", lines.ToArray());
            var size = EditorStyles.miniLabel.CalcSize(new GUIContent(text));

            float boxX = Mathf.Min(x + 6f, area.xMax - size.x - 6f);
            var box = new Rect(boxX, area.y + 2f, size.x + 8f, size.y + 2f);

            EditorGUI.DrawRect(box, new Color(0f, 0f, 0f, 0.82f));
            GUI.Label(new Rect(box.x + 4f, box.y, size.x, size.y), text, EditorStyles.miniLabel);
        }

        // ------------------------------------------------------------------
        // Input
        // ------------------------------------------------------------------

        private void HandleTimelineInput(Rect area)
        {
            Event evt = Event.current;
            mouseInside = area.Contains(evt.mousePosition);

            if (evt.type == EventType.MouseMove || evt.type == EventType.MouseDrag)
            {
                lastMousePosition = evt.mousePosition;
                Repaint();
            }

            if (!mouseInside)
                return;

            if (!followAnimationWindow)
            {
                if (evt.type == EventType.ScrollWheel)
                {
                    float anchorTime = XToTime(evt.mousePosition.x);
                    standaloneZoom = Mathf.Clamp(standaloneZoom * (1f - evt.delta.y * 0.05f), 1f, 40f);

                    // Keep the time under the cursor pinned while zooming.
                    RecalculateMapping(area);
                    float drift = XToTime(evt.mousePosition.x) - anchorTime;
                    standaloneScroll = Mathf.Clamp(
                        standaloneScroll + drift / Mathf.Max(0.0001f, analysis.Duration),
                        0f, Mathf.Max(0f, 1f - 1f / standaloneZoom));

                    evt.Use();
                    Repaint();
                    return;
                }

                if (evt.type == EventType.MouseDrag && evt.button == 2)
                {
                    standaloneScroll = Mathf.Clamp(
                        standaloneScroll - evt.delta.x / Mathf.Max(1f, area.width * standaloneZoom),
                        0f, Mathf.Max(0f, 1f - 1f / standaloneZoom));
                    evt.Use();
                    Repaint();
                    return;
                }
            }

            bool scrubbing = evt.button == 0
                             && (evt.type == EventType.MouseDown || evt.type == EventType.MouseDrag);

            if (clickToScrub && scrubbing)
            {
                float time = Mathf.Clamp(XToTime(evt.mousePosition.x), 0f, analysis.Duration);
                if (NekoAnimationWindowLink.TrySetCurrentTime(time))
                {
                    currentTime = time;
                    evt.Use();
                    Repaint();
                }
            }
        }

        private void JumpToDrop(int direction)
        {
            if (analysis == null || analysis.Drops.Count == 0)
                return;

            float now = currentTime;
            float target = now;

            if (direction > 0)
            {
                var next = analysis.Drops.FirstOrDefault(d => d.Time > now + 0.01f);
                target = next.Time > 0f ? next.Time : analysis.Drops[analysis.Drops.Count - 1].Time;
            }
            else
            {
                var previous = analysis.Drops.LastOrDefault(d => d.Time < now - 0.01f);
                target = previous.Time > 0f ? previous.Time : analysis.Drops[0].Time;
            }

            if (NekoAnimationWindowLink.TrySetCurrentTime(target))
                currentTime = target;

            NekoAudioPreview.SeekToTime(clip, target);
            Repaint();
        }

        // ------------------------------------------------------------------
        // Clip assignment
        // ------------------------------------------------------------------

        private void HandleDragAndDrop(Rect area)
        {
            Event evt = Event.current;
            if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform)
                return;

            if (!area.Contains(evt.mousePosition))
                return;

            bool hasClip = DragAndDrop.objectReferences.Any(o => o is AudioClip);
            bool hasFile = DragAndDrop.paths != null
                           && DragAndDrop.paths.Any(p => SupportedExtensions.Contains(
                               Path.GetExtension(p ?? string.Empty).ToLowerInvariant()));

            if (!hasClip && !hasFile)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                return;
            }

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            if (evt.type != EventType.DragPerform)
                return;

            DragAndDrop.AcceptDrag();
            evt.Use();

            var dropped = DragAndDrop.objectReferences.OfType<AudioClip>().FirstOrDefault();
            if (dropped != null)
            {
                SetClip(dropped);
                return;
            }

            // Dragged straight from Explorer/Finder: copy it into the project so Unity can
            // decode it, then use the imported clip.
            string source = DragAndDrop.paths.FirstOrDefault(p =>
                SupportedExtensions.Contains(Path.GetExtension(p ?? string.Empty).ToLowerInvariant()));

            if (!string.IsNullOrEmpty(source))
                SetClip(ImportExternalAudio(source));
        }

        private AudioClip ImportExternalAudio(string sourcePath)
        {
            try
            {
                if (sourcePath.Replace('\\', '/').StartsWith(Application.dataPath.Replace('\\', '/')))
                {
                    // Already inside the project - just load it.
                    string relative = "Assets" + sourcePath.Replace('\\', '/')
                        .Substring(Application.dataPath.Replace('\\', '/').Length);
                    return AssetDatabase.LoadAssetAtPath<AudioClip>(relative);
                }

                EnsureFolder(ImportFolder);

                string fileName = Path.GetFileName(sourcePath);
                string destination = ImportFolder + "/" + fileName;
                destination = AssetDatabase.GenerateUniqueAssetPath(destination);

                File.Copy(sourcePath, Path.Combine(
                    Directory.GetParent(Application.dataPath).FullName, destination), false);

                AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);

                var imported = AssetDatabase.LoadAssetAtPath<AudioClip>(destination);
                if (imported == null)
                {
                    EditorUtility.DisplayDialog(
                        "Beat Keyframing",
                        "Unity could not import that file as audio:\n\n" + fileName,
                        "OK");
                }
                else
                {
                    Debug.Log("[NekoBeat] Imported " + destination);
                }

                return imported;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("Beat Keyframing", "Could not import that file:\n\n" + ex.Message, "OK");
                return null;
            }
        }

        private static void EnsureFolder(string folder)
        {
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        private void SetClip(AudioClip value)
        {
            if (value == clip)
                return;

            clip = value;
            analysis = null;
            analysisError = null;

            EditorPrefs.SetString(PrefPrefix + "ClipPath",
                clip != null ? AssetDatabase.GetAssetPath(clip) : string.Empty);

            if (clip != null)
                Analyse();

            Repaint();
        }

        private void Analyse(bool force = false)
        {
            if (clip == null)
                return;

            // Reuse the cached result unless the user explicitly asked for a fresh pass. This is
            // what makes the window survive a script recompile without redoing several seconds of
            // reimporting and FFT work.
            if (!force && NekoAnalysisCache.TryLoad(clip, out analysis))
            {
                analysisError = null;
                Repaint();
                return;
            }

            analysis = NekoAudioAnalysis.Analyse(clip, out analysisError);

            if (analysis != null)
                NekoAnalysisCache.Save(clip, analysis);
            else if (!string.IsNullOrEmpty(analysisError))
                Debug.LogWarning("[NekoBeat] " + analysisError);

            // The clip asset is replaced during the reimport the analyser may have performed.
            string path = EditorPrefs.GetString(PrefPrefix + "ClipPath", string.Empty);
            if (!string.IsNullOrEmpty(path))
            {
                var reloaded = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (reloaded != null)
                    clip = reloaded;
            }

            Repaint();
        }
    }
}
#endif
