using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using S = NekoSune.LipSync.Editor.LipSyncStyles;
using G = NekoSune.LipSync.Editor.LipSyncGUI;

namespace NekoSune.LipSync.Editor
{
    /// <summary>
    /// Lip Sync Studio window. Layout follows Motion Converter: header (title, version chip, language picker),
    /// bordered cards for each step, a big accent action button, a result row and the NekoSuneVR footer.
    /// </summary>
    internal class NekoLipSyncWindow : EditorWindow
    {
        const string PrefsKey = "NekoSune.Avatars.LipSync.Settings";
        const string FolderKey = "NekoSune.Avatars.LipSync.Folder";
        const string HeaderIcon = "AudioSource Icon";

        [SerializeField] GameObject _avatar;
        [SerializeField] AudioClip _audio;
        [SerializeField] string _outputFolder = "Assets/NekoSune/LipSync";

        NekoLipSyncSettings _settings = new NekoLipSyncSettings();
        NekoAvatarBinding _binding;
        NekoAudioBuffer _buffer;
        float[] _peakMin, _peakMax;

        bool _foldSettings = true, _foldTargets = true, _foldBinding;
        Vector2 _scroll;
        string _lastClipPath;
        string _error;
        string _statusDetail;
        int _presetIndex;
        List<NekoLipSyncPreset> _userPresets = new List<NekoLipSyncPreset>();

        [MenuItem(NekoPaths.MenuRoot + "Avatar/Lip Sync Studio", false, 20)]
        public static void Open()
        {
            var w = GetWindow<NekoLipSyncWindow>();
            w.titleContent = new GUIContent(NekoLoc.T("lipsync.header"), EditorGUIUtility.IconContent(HeaderIcon).image);
            w.minSize = new Vector2(380f, 520f);
            w.Show();
        }

        [MenuItem("Assets/NekoSune/Lip Sync from this audio", true)]
        static bool FromSelectionValidate()
        {
            return Selection.activeObject is AudioClip;
        }

        [MenuItem("Assets/NekoSune/Lip Sync from this audio", false, 30)]
        static void FromSelection()
        {
            var clip = Selection.activeObject as AudioClip;
            Open();
            var w = GetWindow<NekoLipSyncWindow>();
            if (clip != null) w.SetAudio(clip);
        }

        [MenuItem("GameObject/NekoSune/Lip Sync Studio", false, 20)]
        static void FromHierarchy(MenuCommand cmd)
        {
            var go = cmd.context as GameObject;
            Open();
            var w = GetWindow<NekoLipSyncWindow>();
            if (go != null) w.SetAvatar(go);
        }

        void OnEnable()
        {
            NekoLoc.LanguageChanged += OnLanguageChanged;
            UpdateChecker.Changed += Repaint;
            LoadSettings();
            RefreshPresets();
            if (_avatar != null) Rebind();
        }

        void OnDisable()
        {
            NekoLoc.LanguageChanged -= OnLanguageChanged;
            UpdateChecker.Changed -= Repaint;
            SaveSettings();
        }

        void OnLanguageChanged()
        {
            titleContent.text = NekoLoc.T("lipsync.header");
            Repaint();
        }

        // ------------------------------------------------------------------ state

        public void SetAudio(AudioClip clip)
        {
            _audio = clip;
            _buffer = null;
            _peakMin = _peakMax = null;
            _error = null;
            Repaint();
        }

        void SetAvatar(GameObject go)
        {
            _avatar = go;
            _error = null;
            Rebind();
            Repaint();
        }

        void Rebind()
        {
            _binding = _avatar != null ? NekoAvatarBinder.Bind(_avatar) : null;
        }

        void LoadSettings()
        {
            string json = EditorPrefs.GetString(PrefsKey, null);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var loaded = JsonUtility.FromJson<NekoLipSyncSettings>(json);
                    if (loaded != null) _settings = loaded;
                }
                catch { /* fall back to defaults */ }
            }
            _outputFolder = EditorPrefs.GetString(FolderKey, _outputFolder);
        }

        void SaveSettings()
        {
            try
            {
                EditorPrefs.SetString(PrefsKey, JsonUtility.ToJson(_settings));
                EditorPrefs.SetString(FolderKey, _outputFolder);
            }
            catch { /* prefs are a convenience, never fatal */ }
        }

        void RefreshPresets()
        {
            _userPresets.Clear();
            string[] guids = AssetDatabase.FindAssets("t:NekoLipSyncPreset");
            for (int i = 0; i < guids.Length; i++)
            {
                var p = AssetDatabase.LoadAssetAtPath<NekoLipSyncPreset>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (p != null) _userPresets.Add(p);
            }
        }

        NekoAudioBuffer EnsureBuffer()
        {
            if (_buffer != null || _audio == null) return _buffer;
            string err;
            _buffer = NekoAudioReader.Read(_audio, out err);
            if (_buffer == null) _error = err;
            else NekoAudioReader.BuildPeaks(_buffer, 512, out _peakMin, out _peakMax);
            return _buffer;
        }

        // ------------------------------------------------------------------ GUI

        void OnGUI()
        {
            S.Ensure();
            // Wider label column than Unity's default so longer (and translated) labels aren't cut off.
            EditorGUIUtility.labelWidth = Mathf.Clamp(position.width * 0.42f, 150f, 260f);

            DrawHeader();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.Space(4);

            DrawUpdateBanner();
            DrawInputsCard();
            DrawPresetCard();
            DrawSettingsCard();
            DrawTargetsCard();
            DrawBindingCard();
            DrawOutputCard();
            DrawBakeArea();

            EditorGUILayout.Space(4);
            EditorGUILayout.EndScrollView();
            NekoAboutFooter.DrawWithSeparator();
        }

        void DrawHeader()
        {
            var rect = GUILayoutUtility.GetRect(1, 58, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, S.HeaderBg);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 2, rect.width, 2), S.Accent);

            var icon = EditorGUIUtility.IconContent(HeaderIcon).image;
            if (icon != null) GUI.DrawTexture(new Rect(rect.x + 14, rect.y + 13, 30, 30), icon, ScaleMode.ScaleToFit);

            GUI.Label(new Rect(rect.x + 54, rect.y + 9, rect.width - 200, 24), NekoLoc.T("lipsync.title"), S.Title);
            GUI.Label(new Rect(rect.x + 55, rect.y + 32, rect.width - 200, 16), NekoLoc.T("lipsync.tagline"), S.Subtitle);

            DrawVersionChip(new Rect(rect.x, rect.y + 9, rect.width - 12, 18));

            // Language picker under the version chip.
            List<NekoLanguageInfo> langs = NekoLoc.Languages;
            if (langs != null && langs.Count > 0)
            {
                var names = new string[langs.Count];
                int current = 0;
                for (int i = 0; i < langs.Count; i++)
                {
                    names[i] = langs[i].Display;
                    if (langs[i].Code == NekoLoc.ActiveCode) current = i;
                }
                const float langW = 130f;
                int picked = EditorGUI.Popup(new Rect(rect.xMax - langW - 12, rect.y + 31, langW, 18), current, names);
                if (picked != current) NekoLoc.SetLanguage(langs[picked].Code);
            }
        }

        /// <summary>
        /// Version chip, right-aligned in <paramref name="area"/>. Shows "v1.1.0 → v1.2.0" in orange when an update
        /// is out. Click it to check for updates now.
        /// </summary>
        static void DrawVersionChip(Rect area)
        {
            string current = "v" + UpdateChecker.CurrentVersion;
            string text, tip;
            bool update = UpdateChecker.Status == UpdateChecker.State.UpdateAvailable;
            switch (UpdateChecker.Status)
            {
                case UpdateChecker.State.Checking:
                    text = current + "  ···";
                    tip = NekoLoc.T("update.checking");
                    break;
                case UpdateChecker.State.UpdateAvailable:
                    text = current + "  →  v" + UpdateChecker.LatestVersion;
                    tip = NekoLoc.T("update.title", UpdateChecker.LatestVersion) + "\n" + NekoLoc.T("update.click");
                    break;
                case UpdateChecker.State.UpToDate:
                    text = current;
                    tip = NekoLoc.T("update.uptodate") + "\n" + NekoLoc.T("update.click");
                    break;
                case UpdateChecker.State.Failed:
                    text = current;
                    tip = NekoLoc.T("update.failed");
                    break;
                default:
                    text = current;
                    tip = NekoLoc.T("update.click");
                    break;
            }

            var content = new GUIContent(text, tip);
            var size = S.Chip.CalcSize(content);
            var r = new Rect(area.xMax - size.x, area.y, size.x, area.height);

            var style = S.Chip;
            if (update)
            {
                style = new GUIStyle(S.Chip) { fontStyle = FontStyle.Bold };
                style.normal.textColor = S.Warning;
            }

            EditorGUIUtility.AddCursorRect(r, MouseCursor.Link);
            if (GUI.Button(r, content, style)) UpdateChecker.Check();
        }

        /// <summary>Card shown at the top when a newer version is out.</summary>
        static void DrawUpdateBanner()
        {
            if (!UpdateChecker.ShowBanner) return;

            G.BeginCard("console.infoicon", NekoLoc.T("update.title", UpdateChecker.LatestVersion));

            var bar = GUILayoutUtility.GetRect(1, 3, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(bar, S.Warning);
            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField(NekoLoc.T("update.body", UpdateChecker.CurrentVersion), EditorStyles.wordWrappedLabel);
            if (!string.IsNullOrEmpty(UpdateChecker.Notes))
                EditorGUILayout.LabelField(UpdateChecker.Notes, S.PathLabel);

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                var oldColor = GUI.backgroundColor;
                GUI.backgroundColor = S.Warning;
                if (GUILayout.Button(NekoLoc.T("update.download"), GUILayout.Height(24))) UpdateChecker.OpenDownload();
                GUI.backgroundColor = oldColor;

                if (GUILayout.Button(NekoLoc.T("update.later"), GUILayout.Height(24), GUILayout.ExpandWidth(false))) UpdateChecker.RemindLater();
                if (GUILayout.Button(NekoLoc.T("update.skip"), GUILayout.Height(24), GUILayout.ExpandWidth(false))) UpdateChecker.SkipThisVersion();
            }
            G.EndCard();
        }

        // --- 1. avatar + audio --------------------------------------------------------

        void DrawInputsCard()
        {
            G.BeginCard("Avatar Icon", NekoLoc.T("card.inputs"));

            EditorGUI.BeginChangeCheck();
            var avatar = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent(NekoLoc.T("field.avatar"), NekoLoc.T("slot.avatar.hint")), _avatar, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck())
            {
                if (avatar != null && avatar.GetComponentInChildren<SkinnedMeshRenderer>(true) == null &&
                    avatar.GetComponentInChildren<Animator>(true) == null)
                    _error = NekoLoc.T("err.notAnAvatar");
                else
                    SetAvatar(avatar);
            }

            if (_binding != null)
            {
                G.ChipRow(
                    SourceLabel(_binding.VisemeSource),
                    NekoLoc.T("bind.mapped", _binding.MappedVisemeCount, NekoVisemes.Count),
                    _binding.Jaw != null ? NekoLoc.T("bind.jaw") + ": " + _binding.Jaw.name : null);
            }

            EditorGUILayout.Space(2);
            EditorGUI.BeginChangeCheck();
            var clip = (AudioClip)EditorGUILayout.ObjectField(
                new GUIContent(NekoLoc.T("field.audio"), NekoLoc.T("slot.audio.hint")), _audio, typeof(AudioClip), false);
            if (EditorGUI.EndChangeCheck()) SetAudio(clip);

            if (_audio != null)
            {
                TimeSpan ts = TimeSpan.FromSeconds(_audio.length);
                EnsureBuffer();
                G.ChipRow(
                    string.Format("{0}:{1:00}", (int)ts.TotalMinutes, ts.Seconds),
                    _buffer != null ? _buffer.SampleRate + " Hz" : null,
                    NekoLoc.T(_audio.channels == 1 ? "audio.mono" : "audio.stereo"));
                DrawWaveform();
            }
            else if (_avatar == null)
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField(NekoLoc.T("hint.start"), S.PathLabel);
            }

            G.EndCard();
        }

        void DrawWaveform()
        {
            if (_peakMax == null) return;

            EditorGUILayout.Space(4);
            Rect r = GUILayoutUtility.GetRect(10f, 52f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, S.TrackBg);

            int cols = _peakMax.Length;
            float step = r.width / cols;
            float mid = r.y + r.height * 0.5f;
            float half = r.height * 0.45f;
            for (int c = 0; c < cols; c++)
            {
                float y0 = mid - _peakMax[c] * half;
                float y1 = mid - _peakMin[c] * half;
                EditorGUI.DrawRect(new Rect(r.x + c * step, y0, Mathf.Max(1f, step), Mathf.Max(1f, y1 - y0)), S.Wave);
            }

            // Shade the parts outside the Start / End range.
            if (_buffer != null && _buffer.Length > 0f)
            {
                float len = _buffer.Length;
                float a = Mathf.Clamp01(_settings.startSec / len);
                float b = _settings.endSec > 0f ? Mathf.Clamp01(_settings.endSec / len) : 1f;
                var shade = new Color(0f, 0f, 0f, 0.55f);
                if (a > 0f) EditorGUI.DrawRect(new Rect(r.x, r.y, r.width * a, r.height), shade);
                if (b < 1f) EditorGUI.DrawRect(new Rect(r.x + r.width * b, r.y, r.width * (1f - b), r.height), shade);
            }

            if (NekoAudioPreview.Supported)
            {
                EditorGUILayout.Space(2);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(S.Icon("PlayButton", " " + NekoLoc.T("audio.play")), EditorStyles.miniButtonLeft, GUILayout.Width(80)))
                        NekoAudioPreview.Play(_audio);
                    if (GUILayout.Button(S.Icon("PauseButton", " " + NekoLoc.T("audio.stop")), EditorStyles.miniButtonRight, GUILayout.Width(80)))
                        NekoAudioPreview.Stop();
                    GUILayout.FlexibleSpace();
                }
            }
        }

        // --- 2. preset ----------------------------------------------------------------

        void DrawPresetCard()
        {
            G.BeginCard("Favorite Icon", NekoLoc.T("preset.label"));
            string[] names = BuildPresetNames();
            using (new EditorGUILayout.HorizontalScope())
            {
                int picked = EditorGUILayout.Popup(_presetIndex, names);
                if (picked != _presetIndex)
                {
                    _presetIndex = picked;
                    ApplyPreset(picked);
                }
                if (GUILayout.Button(NekoLoc.T("preset.save"), EditorStyles.miniButtonLeft, GUILayout.ExpandWidth(false)))
                    SaveUserPreset();
                if (GUILayout.Button(NekoLoc.T("preset.refresh"), EditorStyles.miniButtonRight, GUILayout.ExpandWidth(false)))
                    RefreshPresets();
            }
            G.EndCard();
        }

        string[] BuildPresetNames()
        {
            var list = new List<string> { NekoLoc.T("preset.none") };
            for (int i = 0; i < NekoLipSyncSettings.Builtins.Length; i++)
                list.Add(NekoLoc.T(NekoLipSyncSettings.Builtins[i].NameKey));
            for (int i = 0; i < _userPresets.Count; i++)
                list.Add(NekoLoc.T("preset.userPrefix") + " " + _userPresets[i].name);
            return list.ToArray();
        }

        void ApplyPreset(int index)
        {
            if (index <= 0) return;
            int builtinCount = NekoLipSyncSettings.Builtins.Length;
            if (index <= builtinCount)
            {
                _settings.CopyFrom(NekoLipSyncSettings.Builtins[index - 1].Make());
                return;
            }
            int userIndex = index - builtinCount - 1;
            if (userIndex >= 0 && userIndex < _userPresets.Count)
                _settings.CopyFrom(_userPresets[userIndex].settings);
        }

        void SaveUserPreset()
        {
            string folder = NekoAnimClipBuilder.EnsureFolder("Assets/NekoSune/LipSync/Presets");
            string path = EditorUtility.SaveFilePanelInProject(NekoLoc.T("preset.save"), "LipSyncPreset", "asset",
                                                              NekoLoc.T("preset.saveHint"), folder);
            if (string.IsNullOrEmpty(path)) return;

            var asset = ScriptableObject.CreateInstance<NekoLipSyncPreset>();
            asset.settings = _settings.Clone();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            RefreshPresets();
        }

        // --- 3. settings --------------------------------------------------------------

        void DrawSettingsCard()
        {
            _foldSettings = G.BeginFoldoutCard("SettingsIcon", NekoLoc.T("section.settings"), _foldSettings);
            if (_foldSettings)
            {
                G.Group(NekoLoc.T("group.feel"), true);
                _settings.volumeToMouth = EditorGUILayout.Slider(NekoLoc.C("set.volumeToMouth"), _settings.volumeToMouth, 0f, 1f);
                _settings.clarity = EditorGUILayout.Slider(NekoLoc.C("set.clarity"), _settings.clarity, 0.5f, 6f);
                _settings.consonantClose = EditorGUILayout.Slider(NekoLoc.C("set.consonantClose"), _settings.consonantClose, 0f, 1f);
                _settings.strength = EditorGUILayout.Slider(NekoLoc.C("set.strength"), _settings.strength, 0f, 2f);
                _settings.liveliness = EditorGUILayout.Slider(NekoLoc.C("set.liveliness"), _settings.liveliness, 0f, 1f);

                G.Group(NekoLoc.T("group.timing"));
                _settings.offsetMs = Mathf.Round(EditorGUILayout.Slider(NekoLoc.C("set.offsetMs"), _settings.offsetMs, -250f, 250f));
                _settings.attackMs = Mathf.Round(EditorGUILayout.Slider(NekoLoc.C("set.attackMs"), _settings.attackMs, 0f, 200f));
                _settings.releaseMs = Mathf.Round(EditorGUILayout.Slider(NekoLoc.C("set.releaseMs"), _settings.releaseMs, 0f, 400f));
                _settings.silenceThreshold = EditorGUILayout.Slider(NekoLoc.C("set.silence"), _settings.silenceThreshold, 0f, 0.5f);

                G.Group(NekoLoc.T("group.bake"));
                var fpsNames = new GUIContent[NekoLipSyncSettings.FpsOptions.Length];
                for (int i = 0; i < fpsNames.Length; i++) fpsNames[i] = new GUIContent(NekoLipSyncSettings.FpsOptions[i].ToString());
                int fps = Array.IndexOf(NekoLipSyncSettings.FpsOptions, _settings.fps) >= 0 ? _settings.fps : 30;
                _settings.fps = EditorGUILayout.IntPopup(NekoLoc.C("set.fps"), fps, fpsNames, NekoLipSyncSettings.FpsOptions);
                _settings.quality = EditorGUILayout.IntSlider(NekoLoc.C("set.quality"), _settings.quality, 1, 10);
                _settings.cleanVocal = EditorGUILayout.Toggle(NekoLoc.C("set.cleanVocal"), _settings.cleanVocal);
                _settings.loopTime = EditorGUILayout.Toggle(NekoLoc.C("set.loop"), _settings.loopTime);
                _settings.keyReduction = EditorGUILayout.Toggle(NekoLoc.C("set.keyReduction"), _settings.keyReduction);
                if (_settings.keyReduction)
                {
                    EditorGUI.indentLevel++;
                    _settings.keyTolerance = EditorGUILayout.Slider(NekoLoc.C("set.keyTolerance"), _settings.keyTolerance, 0.05f, 5f);
                    EditorGUI.indentLevel--;
                }
                _settings.writeSil = EditorGUILayout.Toggle(NekoLoc.C("set.writeSil"), _settings.writeSil);
                _settings.normalize = EditorGUILayout.Toggle(NekoLoc.C("set.normalize"), _settings.normalize);

                float len = _buffer != null ? _buffer.Length : (_audio != null ? _audio.length : 0f);
                if (len > 0f)
                {
                    G.Group(NekoLoc.T("group.range"));
                    _settings.startSec = EditorGUILayout.Slider(NekoLoc.C("set.startSec"), _settings.startSec, 0f, len);
                    float endShown = _settings.endSec > 0f ? _settings.endSec : len;
                    endShown = EditorGUILayout.Slider(NekoLoc.C("set.endSec"), endShown, 0f, len);
                    _settings.endSec = Mathf.Approximately(endShown, len) ? 0f : endShown;
                    if (_settings.endSec > 0f && _settings.endSec <= _settings.startSec)
                        _settings.endSec = Mathf.Min(len, _settings.startSec + 0.1f);
                }

                EditorGUILayout.Space(6);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(S.Icon("Refresh", " " + NekoLoc.T("common.reset")), GUILayout.ExpandWidth(false)))
                    {
                        _settings.ResetToDefaults();
                        _presetIndex = 0;
                    }
                }
            }
            G.EndCard();
        }

        // --- 4. targets ---------------------------------------------------------------

        void DrawTargetsCard()
        {
            _foldTargets = G.BeginFoldoutCard("SkinnedMeshRenderer Icon", NekoLoc.T("card.targets"), _foldTargets);
            if (_foldTargets)
            {
                var targetNames = new[]
                {
                    new GUIContent(NekoLoc.T("target.auto")), new GUIContent(NekoLoc.T("target.visemes")),
                    new GUIContent(NekoLoc.T("target.jaw")), new GUIContent(NekoLoc.T("target.single"))
                };
                _settings.target = (NekoLipSyncTarget)EditorGUILayout.Popup(NekoLoc.C("target.label"), (int)_settings.target, targetNames);

                if (_settings.target != NekoLipSyncTarget.SingleMouthOpen)
                {
                    _settings.driveJaw = EditorGUILayout.Toggle(NekoLoc.C("target.alsoJaw"), _settings.driveJaw);
                    if (_settings.driveJaw || _settings.target == NekoLipSyncTarget.JawBone)
                    {
                        EditorGUI.indentLevel++;
                        using (new EditorGUI.DisabledScope(_binding == null))
                        {
                            Transform newJaw = (Transform)EditorGUILayout.ObjectField(NekoLoc.C("target.jawBone"),
                                _binding != null ? _binding.Jaw : null, typeof(Transform), true);
                            if (_binding != null && newJaw != _binding.Jaw)
                            {
                                _binding.Jaw = newJaw;
                                _binding.JawPath = newJaw != null && _avatar != null
                                    ? AnimationUtility.CalculateTransformPath(newJaw, _avatar.transform)
                                    : null;
                            }
                        }
                        _settings.jawAxis = (NekoJawAxis)EditorGUILayout.EnumPopup(NekoLoc.C("target.jawAxis"), _settings.jawAxis);
                        _settings.jawMaxAngle = EditorGUILayout.Slider(NekoLoc.C("target.jawAngle"), _settings.jawMaxAngle, 0f, 60f);
                        _settings.jawInvert = EditorGUILayout.Toggle(NekoLoc.C("target.jawInvert"), _settings.jawInvert);
                        EditorGUI.indentLevel--;
                    }
                }

                if (_settings.target != NekoLipSyncTarget.JawBone)
                {
                    _settings.driveSingleShape = EditorGUILayout.Toggle(NekoLoc.C("target.alsoSingle"), _settings.driveSingleShape);
                    if ((_settings.driveSingleShape || _settings.target == NekoLipSyncTarget.SingleMouthOpen) && _binding != null)
                    {
                        EditorGUI.indentLevel++;
                        DrawShapePicker();
                        EditorGUI.indentLevel--;
                    }
                }
            }
            G.EndCard();
        }

        void DrawShapePicker()
        {
            List<KeyValuePair<SkinnedMeshRenderer, string>> shapes = NekoAvatarBinder.AllBlendShapes(_avatar);
            if (shapes.Count == 0)
            {
                EditorGUILayout.HelpBox(NekoLoc.T("target.noShapes"), MessageType.Info);
                return;
            }

            var labels = new GUIContent[shapes.Count];
            int current = -1;
            for (int i = 0; i < shapes.Count; i++)
            {
                labels[i] = new GUIContent(shapes[i].Key.name + " / " + shapes[i].Value);
                if (_binding.SingleRenderer == shapes[i].Key && _binding.SingleShapeName == shapes[i].Value)
                    current = i;
            }

            int picked = EditorGUILayout.Popup(NekoLoc.C("target.mouthShape"), current, labels);
            if (picked >= 0 && picked != current)
            {
                _binding.SingleRenderer = shapes[picked].Key;
                _binding.SingleShapeName = shapes[picked].Value;
                _binding.SingleRendererPath = AnimationUtility.CalculateTransformPath(
                    shapes[picked].Key.transform, _avatar.transform);
            }
        }

        // --- 5. binding ---------------------------------------------------------------

        void DrawBindingCard()
        {
            _foldBinding = G.BeginFoldoutCard("Avatar Icon", NekoLoc.T("section.binding"), _foldBinding);
            if (_foldBinding)
            {
                if (_binding == null)
                {
                    EditorGUILayout.LabelField(NekoLoc.T("bind.noAvatar"), S.PathLabel);
                }
                else
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        G.Chip(NekoLoc.T("bind.source") + ": " + SourceLabel(_binding.VisemeSource));
                        G.Chip(NekoLoc.T("bind.mapped", _binding.MappedVisemeCount, NekoVisemes.Count));
                        GUILayout.FlexibleSpace();
                    }
                    if (_binding.VisemeRenderer != null)
                        EditorGUILayout.LabelField(NekoLoc.T("bind.mesh"), _binding.VisemeRenderer.name);
                    if (_binding.Jaw != null)
                        EditorGUILayout.LabelField(NekoLoc.T("bind.jaw"), _binding.Jaw.name);
                    if (_binding.HasSingleShape)
                        EditorGUILayout.LabelField(NekoLoc.T("bind.single"), _binding.SingleShapeName);

                    EditorGUILayout.Space(4);
                    for (int v = 0; v < NekoVisemes.Count; v++)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            bool mapped = !string.IsNullOrEmpty(_binding.ShapeNames[v]);
                            var chip = new GUIStyle(S.Chip) { fixedWidth = 44 };
                            chip.normal.textColor = mapped ? S.Success : S.Error;
                            GUILayout.Label(NekoVisemes.Names[v], chip);
                            GUILayout.Label(mapped ? _binding.ShapeNames[v] : NekoLoc.T("bind.unmapped"), S.PathLabel);
                        }
                    }

                    for (int i = 0; i < _binding.Notes.Count; i++)
                        EditorGUILayout.HelpBox(_binding.Notes[i], MessageType.None);

                    EditorGUILayout.Space(4);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button(S.Icon("Refresh", " " + NekoLoc.T("bind.rescan")), GUILayout.ExpandWidth(false)))
                            Rebind();
                    }
                }
            }
            G.EndCard();
        }

        string SourceLabel(NekoBindSource s)
        {
            switch (s)
            {
                case NekoBindSource.VrcDescriptor: return NekoLoc.T("bind.srcDescriptor");
                case NekoBindSource.AutoDetected:  return NekoLoc.T("bind.srcAuto");
                case NekoBindSource.Manual:        return NekoLoc.T("bind.srcManual");
                default:                           return NekoLoc.T("bind.srcNone");
            }
        }

        // --- 6. output ----------------------------------------------------------------

        void DrawOutputCard()
        {
            G.BeginCard("Folder Icon", NekoLoc.T("card.output"));
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(NekoLoc.C("out.folder"));
                EditorGUILayout.SelectableLabel(_outputFolder, EditorStyles.textField, GUILayout.Height(18f));
                if (GUILayout.Button("…", EditorStyles.miniButton, GUILayout.Width(26f)))
                {
                    string abs = EditorUtility.SaveFolderPanel(NekoLoc.T("out.folder"), "Assets", "");
                    if (!string.IsNullOrEmpty(abs))
                    {
                        string rel = ToAssetPath(abs);
                        if (rel != null) _outputFolder = rel;
                        else _error = NekoLoc.T("err.folderOutsideProject");
                    }
                }
            }

            if (_audio != null)
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField(NekoLoc.T("out.saveAs"), S.PathLabel);
                G.OutputPath(_outputFolder.TrimEnd('/') + "/" + NekoAnimClipBuilder.Sanitize(_audio.name + "_lipsync") + ".anim");
            }
            G.EndCard();
        }

        // --- 7. bake + result ---------------------------------------------------------

        void DrawBakeArea()
        {
            EditorGUILayout.Space(2);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(8);
                if (G.BigButton(NekoLoc.T("lipsync.bake"), CanBake())) Bake();
                GUILayout.Space(8);
            }

            if (!CanBake() && _avatar != null && _audio != null && _binding != null && !_binding.CanBake)
            {
                EditorGUILayout.Space(2);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(8);
                    EditorGUILayout.HelpBox(NekoLoc.T("bind.noneFound"), MessageType.Warning);
                    GUILayout.Space(8);
                }
            }

            bool hasResult = !string.IsNullOrEmpty(_lastClipPath);
            if (!hasResult && string.IsNullOrEmpty(_error)) return;

            EditorGUILayout.Space(4);
            G.BeginCard(null, null);
            if (!string.IsNullOrEmpty(_error))
            {
                G.ErrorRow(_error);
            }
            else
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(_lastClipPath);
                G.ResultRow(clip, _statusDetail);
            }
            G.EndCard();
        }

        bool CanBake()
        {
            return _audio != null && _avatar != null && _binding != null && _binding.CanBake;
        }

        void Bake()
        {
            _error = null;
            _lastClipPath = null;
            _statusDetail = null;

            try
            {
                NekoAudioBuffer buffer = EnsureBuffer();
                if (buffer == null)
                {
                    if (string.IsNullOrEmpty(_error)) _error = NekoLoc.T("err.readFailed");
                    return;
                }

                NekoLipSyncTrack track = NekoLipSyncAnalyzer.Analyze(buffer, _settings, (p, label) =>
                    EditorUtility.DisplayCancelableProgressBar(NekoLoc.T("lipsync.header"), label, p));

                if (track == null)
                {
                    _error = NekoLoc.T("err.analysisFailed");
                    return;
                }

                EditorUtility.DisplayProgressBar(NekoLoc.T("lipsync.header"), NekoLoc.T("progress.writing"), 0.96f);

                NekoBakeReport report;
                string clipName = _audio.name + "_lipsync";
                AnimationClip clip = NekoAnimClipBuilder.Build(track, _binding, _settings, clipName, out report);
                if (clip == null)
                {
                    _error = NekoLoc.T("err.noCurves");
                    return;
                }

                string folder = NekoAnimClipBuilder.EnsureFolder(_outputFolder);
                _outputFolder = folder;
                _lastClipPath = NekoAnimClipBuilder.Save(clip, folder, clipName);
                report.ClipPath = _lastClipPath;

                _statusDetail = NekoLoc.T("status.detail",
                    report.CurveCount, report.KeyCount, report.Duration.ToString("0.00"),
                    Mathf.RoundToInt(report.Reduction * 100f));

                SaveSettings();
            }
            catch (Exception e)
            {
                _error = e.Message;
                Debug.LogException(e);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        static string ToAssetPath(string absolute)
        {
            if (string.IsNullOrEmpty(absolute)) return null;
            string dataPath = Application.dataPath.Replace('\\', '/');
            string norm = absolute.Replace('\\', '/');
            if (!norm.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase)) return null;
            return "Assets" + norm.Substring(dataPath.Length);
        }
    }
}
