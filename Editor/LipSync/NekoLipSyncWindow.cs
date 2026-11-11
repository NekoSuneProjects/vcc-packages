using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NekoSune.LipSync.Editor
{
    internal class NekoLipSyncWindow : EditorWindow
    {
        const string PrefsKey = "NekoSune.Avatars.LipSync.Settings";
        const string FolderKey = "NekoSune.Avatars.LipSync.Folder";

        [SerializeField] GameObject _avatar;
        [SerializeField] AudioClip _audio;
        [SerializeField] string _outputFolder = "Assets/NekoSune/LipSync";

        NekoLipSyncSettings _settings = new NekoLipSyncSettings();
        NekoAvatarBinding _binding;
        NekoAudioBuffer _buffer;
        float[] _peakMin, _peakMax;

        bool _foldPreset, _foldSettings = true, _foldTargets, _foldBinding;
        Vector2 _scroll;
        string _lastClipPath;
        string _error;
        string _statusDetail;
        int _presetIndex;
        List<NekoLipSyncPreset> _userPresets = new List<NekoLipSyncPreset>();

        [MenuItem(NekoPaths.MenuRoot + "Avatar/Lip Sync Studio", false, 20)]
        public static void Open()
        {
            var w = GetWindow<NekoLipSyncWindow>(false, "Lip Sync", true);
            w.minSize = new Vector2(470f, 460f);
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
            NekoLoc.LanguageChanged += Repaint;
            LoadSettings();
            RefreshPresets();
            if (_avatar != null) Rebind();
        }

        void OnDisable()
        {
            NekoLoc.LanguageChanged -= Repaint;
            SaveSettings();
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
            NekoStyles.Ensure();

            EditorGUILayout.BeginHorizontal();
            NekoStyles.HeaderBar(NekoLoc.T("lipsync.header"), "VRC", NekoLoc.T("lipsync.tagline"));
            DrawLanguagePopup();
            EditorGUILayout.EndHorizontal();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawAvatarSlot();
            DrawAudioSlot();
            DrawWaveform();

            GUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(!CanBake()))
            {
                if (GUILayout.Button(NekoLoc.T("lipsync.bake"), NekoStyles.PrimaryButton, GUILayout.Height(30f)))
                    Bake();
            }
            EditorGUILayout.EndHorizontal();

            DrawStatus();
            DrawPresetSection();
            DrawSettingsSection();
            DrawTargetSection();
            DrawBindingSection();

            GUILayout.Space(10f);
            EditorGUILayout.EndScrollView();
        }

        void DrawLanguagePopup()
        {
            List<NekoLanguageInfo> langs = NekoLoc.Languages;
            if (langs == null || langs.Count == 0) return;

            EditorGUILayout.BeginVertical(GUILayout.Width(170f));
            GUILayout.Space(12f);
            var names = new string[langs.Count];
            int current = 0;
            for (int i = 0; i < langs.Count; i++)
            {
                names[i] = langs[i].Display;
                if (langs[i].Code == NekoLoc.ActiveCode) current = i;
            }
            int picked = EditorGUILayout.Popup(current, names);
            if (picked != current) NekoLoc.SetLanguage(langs[picked].Code);
            EditorGUILayout.EndVertical();
        }

        void DrawAvatarSlot()
        {
            string title = _avatar != null ? _avatar.name : NekoLoc.T("slot.avatar.empty");
            string meta = _avatar != null ? NekoLoc.T("slot.avatar.filled") : NekoLoc.T("slot.avatar.hint");

            SlotResult r = DrawSlot("◉", title, meta, _avatar != null);
            if (r == SlotResult.Clear) SetAvatar(null);
            if (r == SlotResult.Clicked && _avatar != null) EditorGUIUtility.PingObject(_avatar);

            HandleDrop<GameObject>(GUILayoutUtility.GetLastRect(), go =>
            {
                if (go.GetComponentInChildren<SkinnedMeshRenderer>(true) == null &&
                    go.GetComponentInChildren<Animator>(true) == null)
                    return false;
                SetAvatar(go);
                return true;
            });
        }

        void DrawAudioSlot()
        {
            string title = _audio != null ? _audio.name : NekoLoc.T("slot.audio.empty");
            string meta;
            if (_audio != null)
            {
                TimeSpan ts = TimeSpan.FromSeconds(_audio.length);
                meta = string.Format("{0}:{1:00}  ·  {2}", (int)ts.TotalMinutes, ts.Seconds,
                                     NekoLoc.T("slot.audio.filled"));
            }
            else meta = NekoLoc.T("slot.audio.hint");

            SlotResult r = DrawSlot("♪", title, meta, _audio != null);
            if (r == SlotResult.Clear) SetAudio(null);
            if (r == SlotResult.Clicked && _audio != null) EditorGUIUtility.PingObject(_audio);

            HandleDrop<AudioClip>(GUILayoutUtility.GetLastRect(), clip => { SetAudio(clip); return true; });
        }

        enum SlotResult { None, Clicked, Clear }

        SlotResult DrawSlot(string glyph, string title, string meta, bool filled)
        {
            SlotResult result = SlotResult.None;

            EditorGUILayout.BeginHorizontal(NekoStyles.Card, GUILayout.Height(46f));
            GUILayout.Label(glyph, NekoStyles.IconBig, GUILayout.Width(30f), GUILayout.Height(38f));

            EditorGUILayout.BeginVertical();
            GUILayout.Space(3f);
            GUILayout.Label(title, NekoStyles.SlotName);
            GUILayout.Label(meta, NekoStyles.SlotMeta);
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            if (filled && GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(24f), GUILayout.Height(22f)))
                result = SlotResult.Clear;
            EditorGUILayout.EndHorizontal();

            Rect rect = GUILayoutUtility.GetLastRect();
            if (!filled)
                NekoStyles.Outline(rect, new Color(NekoStyles.Accent.r, NekoStyles.Accent.g, NekoStyles.Accent.b, 0.35f));

            if (result == SlotResult.None &&
                Event.current.type == EventType.MouseDown && Event.current.button == 0 &&
                rect.Contains(Event.current.mousePosition))
            {
                result = SlotResult.Clicked;
            }

            return result;
        }

        void HandleDrop<T>(Rect rect, Func<T, bool> accept) where T : UnityEngine.Object
        {
            Event e = Event.current;
            if (e.type != EventType.DragUpdated && e.type != EventType.DragPerform) return;
            if (!rect.Contains(e.mousePosition)) return;

            T found = null;
            UnityEngine.Object[] objs = DragAndDrop.objectReferences;
            for (int i = 0; i < objs.Length; i++)
            {
                var t = objs[i] as T;
                if (t != null) { found = t; break; }
            }

            if (found == null)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                return;
            }

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                if (!accept(found)) _error = NekoLoc.T("err.notAnAvatar");
                e.Use();
            }
        }

        void DrawWaveform()
        {
            if (_audio == null) return;
            EnsureBuffer();
            if (_peakMax == null) return;

            Rect r = GUILayoutUtility.GetRect(10f, 46f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, NekoStyles.IsDark ? new Color(0.12f, 0.13f, 0.15f) : new Color(0.80f, 0.81f, 0.84f));

            int cols = _peakMax.Length;
            float step = r.width / cols;
            float mid = r.y + r.height * 0.5f;
            float half = r.height * 0.45f;
            for (int c = 0; c < cols; c++)
            {
                float lo = _peakMin[c], hi = _peakMax[c];
                float y0 = mid - hi * half;
                float y1 = mid - lo * half;
                float h = Mathf.Max(1f, y1 - y0);
                EditorGUI.DrawRect(new Rect(r.x + c * step, y0, Mathf.Max(1f, step), h), NekoStyles.WaveColor);
            }

            // Trim markers
            if (_buffer != null && _buffer.Length > 0f)
            {
                float len = _buffer.Length;
                float a = Mathf.Clamp01(_settings.startSec / len);
                float b = _settings.endSec > 0f ? Mathf.Clamp01(_settings.endSec / len) : 1f;
                var shade = new Color(0f, 0f, 0f, 0.55f);
                if (a > 0f) EditorGUI.DrawRect(new Rect(r.x, r.y, r.width * a, r.height), shade);
                if (b < 1f) EditorGUI.DrawRect(new Rect(r.x + r.width * b, r.y, r.width * (1f - b), r.height), shade);
            }

            EditorGUILayout.BeginHorizontal();
            if (NekoAudioPreview.Supported)
            {
                if (GUILayout.Button(NekoLoc.T("audio.play"), EditorStyles.miniButtonLeft, GUILayout.Width(60f)))
                    NekoAudioPreview.Play(_audio);
                if (GUILayout.Button(NekoLoc.T("audio.stop"), EditorStyles.miniButtonRight, GUILayout.Width(60f)))
                    NekoAudioPreview.Stop();
            }
            GUILayout.FlexibleSpace();
            if (_buffer != null)
                GUILayout.Label(string.Format("{0} Hz · {1}", _buffer.SampleRate,
                                _audio.channels == 1 ? NekoLoc.T("audio.mono") : NekoLoc.T("audio.stereo")),
                                NekoStyles.SlotMeta);
            EditorGUILayout.EndHorizontal();
        }

        void DrawStatus()
        {
            if (!string.IsNullOrEmpty(_error))
            {
                EditorGUILayout.HelpBox(_error, MessageType.Error);
                return;
            }

            if (string.IsNullOrEmpty(_lastClipPath)) return;

            EditorGUILayout.BeginVertical(NekoStyles.Banner);
            EditorGUILayout.BeginHorizontal();
            var done = new GUIStyle(NekoStyles.SlotName);
            done.normal.textColor = NekoStyles.Good;
            GUILayout.Label(NekoLoc.T("status.done") + "  ✓   " + System.IO.Path.GetFileName(_lastClipPath), done);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_statusDetail))
                GUILayout.Label(_statusDetail, NekoStyles.SlotMeta);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(NekoLoc.T("status.reveal"), NekoStyles.Link, GUILayout.Width(150f)))
            {
                var obj = AssetDatabase.LoadAssetAtPath<AnimationClip>(_lastClipPath);
                if (obj != null) { Selection.activeObject = obj; EditorGUIUtility.PingObject(obj); }
            }
            EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(), MouseCursor.Link);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        void DrawPresetSection()
        {
            string[] names = BuildPresetNames();
            _foldPreset = EditorGUILayout.Foldout(_foldPreset,
                NekoLoc.T("preset.label") + ": " + (_presetIndex >= 0 && _presetIndex < names.Length ? names[_presetIndex] : NekoLoc.T("preset.none")),
                true, NekoStyles.SectionHeader);
            if (!_foldPreset) return;

            EditorGUI.indentLevel++;
            EditorGUILayout.BeginHorizontal();
            int picked = EditorGUILayout.Popup(_presetIndex, names);
            if (picked != _presetIndex)
            {
                _presetIndex = picked;
                ApplyPreset(picked);
            }
            if (GUILayout.Button(NekoLoc.T("preset.save"), EditorStyles.miniButton, GUILayout.Width(90f)))
                SaveUserPreset();
            if (GUILayout.Button(NekoLoc.T("preset.refresh"), EditorStyles.miniButton, GUILayout.Width(70f)))
                RefreshPresets();
            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel--;
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

        void DrawSettingsSection()
        {
            _foldSettings = EditorGUILayout.Foldout(_foldSettings, NekoLoc.T("section.settings"), true, NekoStyles.SectionHeader);
            if (!_foldSettings) return;

            EditorGUILayout.BeginVertical(NekoStyles.Card);

            _settings.volumeToMouth = NekoStyles.SliderRow(NekoLoc.C("set.volumeToMouth"), _settings.volumeToMouth, 0f, 1f);
            _settings.clarity = NekoStyles.SliderRow(NekoLoc.C("set.clarity"), _settings.clarity, 0.5f, 6f);
            _settings.consonantClose = NekoStyles.SliderRow(NekoLoc.C("set.consonantClose"), _settings.consonantClose, 0f, 1f);
            _settings.strength = NekoStyles.SliderRow(NekoLoc.C("set.strength"), _settings.strength, 0f, 2f);
            _settings.offsetMs = NekoStyles.SliderRow(NekoLoc.C("set.offsetMs"), _settings.offsetMs, -250f, 250f, "0");
            _settings.attackMs = NekoStyles.SliderRow(NekoLoc.C("set.attackMs"), _settings.attackMs, 0f, 200f, "0");
            _settings.releaseMs = NekoStyles.SliderRow(NekoLoc.C("set.releaseMs"), _settings.releaseMs, 0f, 400f, "0");
            _settings.silenceThreshold = NekoStyles.SliderRow(NekoLoc.C("set.silence"), _settings.silenceThreshold, 0f, 0.5f);
            _settings.liveliness = NekoStyles.SliderRow(NekoLoc.C("set.liveliness"), _settings.liveliness, 0f, 1f);

            // FPS as a dropdown of sane animation rates.
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(NekoLoc.C("set.fps"), GUILayout.Width(190f));
            int fpsIndex = Array.IndexOf(NekoLipSyncSettings.FpsOptions, _settings.fps);
            if (fpsIndex < 0) fpsIndex = Array.IndexOf(NekoLipSyncSettings.FpsOptions, 30);
            var fpsNames = new string[NekoLipSyncSettings.FpsOptions.Length];
            for (int i = 0; i < fpsNames.Length; i++) fpsNames[i] = NekoLipSyncSettings.FpsOptions[i].ToString();
            int newFps = EditorGUILayout.Popup(fpsIndex, fpsNames);
            _settings.fps = NekoLipSyncSettings.FpsOptions[Mathf.Clamp(newFps, 0, fpsNames.Length - 1)];
            EditorGUILayout.EndHorizontal();

            _settings.quality = NekoStyles.IntSliderRow(NekoLoc.C("set.quality"), _settings.quality, 1, 10);
            _settings.cleanVocal = NekoStyles.ToggleRow(NekoLoc.C("set.cleanVocal"), _settings.cleanVocal);
            _settings.loopTime = NekoStyles.ToggleRow(NekoLoc.C("set.loop"), _settings.loopTime);
            _settings.keyReduction = NekoStyles.ToggleRow(NekoLoc.C("set.keyReduction"), _settings.keyReduction);
            if (_settings.keyReduction)
                _settings.keyTolerance = NekoStyles.SliderRow(NekoLoc.C("set.keyTolerance"), _settings.keyTolerance, 0.05f, 5f);
            _settings.writeSil = NekoStyles.ToggleRow(NekoLoc.C("set.writeSil"), _settings.writeSil);
            _settings.normalize = NekoStyles.ToggleRow(NekoLoc.C("set.normalize"), _settings.normalize);

            NekoStyles.Rule(4f);
            float len = _buffer != null ? _buffer.Length : (_audio != null ? _audio.length : 0f);
            if (len > 0f)
            {
                _settings.startSec = NekoStyles.SliderRow(NekoLoc.C("set.startSec"), _settings.startSec, 0f, len, "0.00");
                float endShown = _settings.endSec > 0f ? _settings.endSec : len;
                endShown = NekoStyles.SliderRow(NekoLoc.C("set.endSec"), endShown, 0f, len, "0.00");
                _settings.endSec = Mathf.Approximately(endShown, len) ? 0f : endShown;
                if (_settings.endSec > 0f && _settings.endSec <= _settings.startSec)
                    _settings.endSec = Mathf.Min(len, _settings.startSec + 0.1f);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(NekoLoc.T("common.reset"), GUILayout.Width(90f)))
            {
                _settings.ResetToDefaults();
                _presetIndex = 0;
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        void DrawTargetSection()
        {
            _foldTargets = EditorGUILayout.Foldout(_foldTargets, NekoLoc.T("section.targets"), true, NekoStyles.SectionHeader);
            if (!_foldTargets) return;

            EditorGUILayout.BeginVertical(NekoStyles.Card);

            var targetNames = new[]
            {
                NekoLoc.T("target.auto"), NekoLoc.T("target.visemes"),
                NekoLoc.T("target.jaw"), NekoLoc.T("target.single")
            };
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(NekoLoc.C("target.label"), GUILayout.Width(190f));
            _settings.target = (NekoLipSyncTarget)EditorGUILayout.Popup((int)_settings.target, targetNames);
            EditorGUILayout.EndHorizontal();

            if (_settings.target != NekoLipSyncTarget.SingleMouthOpen)
            {
                _settings.driveJaw = NekoStyles.ToggleRow(NekoLoc.C("target.alsoJaw"), _settings.driveJaw);
                if (_settings.driveJaw || _settings.target == NekoLipSyncTarget.JawBone)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(NekoLoc.C("target.jawBone"), GUILayout.Width(180f));
                    Transform newJaw = (Transform)EditorGUILayout.ObjectField(
                        _binding != null ? _binding.Jaw : null, typeof(Transform), true);
                    if (_binding != null && newJaw != _binding.Jaw)
                    {
                        _binding.Jaw = newJaw;
                        _binding.JawPath = newJaw != null && _avatar != null
                            ? AnimationUtility.CalculateTransformPath(newJaw, _avatar.transform)
                            : null;
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(NekoLoc.C("target.jawAxis"), GUILayout.Width(180f));
                    _settings.jawAxis = (NekoJawAxis)EditorGUILayout.EnumPopup(_settings.jawAxis);
                    EditorGUILayout.EndHorizontal();

                    _settings.jawMaxAngle = NekoStyles.SliderRow(NekoLoc.C("target.jawAngle"), _settings.jawMaxAngle, 0f, 60f, "0.#", 180f);
                    _settings.jawInvert = NekoStyles.ToggleRow(NekoLoc.C("target.jawInvert"), _settings.jawInvert, 180f);
                    EditorGUI.indentLevel--;
                }
            }

            if (_settings.target != NekoLipSyncTarget.JawBone)
            {
                _settings.driveSingleShape = NekoStyles.ToggleRow(NekoLoc.C("target.alsoSingle"), _settings.driveSingleShape);
                if ((_settings.driveSingleShape || _settings.target == NekoLipSyncTarget.SingleMouthOpen) && _binding != null)
                {
                    EditorGUI.indentLevel++;
                    DrawShapePicker();
                    EditorGUI.indentLevel--;
                }
            }

            NekoStyles.Rule(4f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(NekoLoc.C("out.folder"), GUILayout.Width(190f));
            EditorGUILayout.SelectableLabel(_outputFolder, EditorStyles.textField, GUILayout.Height(18f));
            if (GUILayout.Button("...", EditorStyles.miniButton, GUILayout.Width(28f)))
            {
                string abs = EditorUtility.SaveFolderPanel(NekoLoc.T("out.folder"), "Assets", "");
                if (!string.IsNullOrEmpty(abs))
                {
                    string rel = ToAssetPath(abs);
                    if (rel != null) _outputFolder = rel;
                    else _error = NekoLoc.T("err.folderOutsideProject");
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        void DrawShapePicker()
        {
            List<KeyValuePair<SkinnedMeshRenderer, string>> shapes = NekoAvatarBinder.AllBlendShapes(_avatar);
            if (shapes.Count == 0)
            {
                EditorGUILayout.HelpBox(NekoLoc.T("target.noShapes"), MessageType.Info);
                return;
            }

            var labels = new string[shapes.Count];
            int current = -1;
            for (int i = 0; i < shapes.Count; i++)
            {
                labels[i] = shapes[i].Key.name + " / " + shapes[i].Value;
                if (_binding.SingleRenderer == shapes[i].Key && _binding.SingleShapeName == shapes[i].Value)
                    current = i;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(NekoLoc.C("target.mouthShape"), GUILayout.Width(180f));
            int picked = EditorGUILayout.Popup(current, labels);
            EditorGUILayout.EndHorizontal();

            if (picked >= 0 && picked != current)
            {
                _binding.SingleRenderer = shapes[picked].Key;
                _binding.SingleShapeName = shapes[picked].Value;
                _binding.SingleRendererPath = AnimationUtility.CalculateTransformPath(
                    shapes[picked].Key.transform, _avatar.transform);
            }
        }

        void DrawBindingSection()
        {
            _foldBinding = EditorGUILayout.Foldout(_foldBinding, NekoLoc.T("section.binding"), true, NekoStyles.SectionHeader);
            if (!_foldBinding) return;

            EditorGUILayout.BeginVertical(NekoStyles.Card);
            if (_binding == null)
            {
                GUILayout.Label(NekoLoc.T("bind.noAvatar"), NekoStyles.WrapLabel);
                EditorGUILayout.EndVertical();
                return;
            }

            GUILayout.Label(NekoLoc.T("bind.source") + ": " + SourceLabel(_binding.VisemeSource), NekoStyles.SlotMeta);
            GUILayout.Label(NekoLoc.T("bind.mapped", _binding.MappedVisemeCount, NekoVisemes.Count), NekoStyles.SlotMeta);
            if (_binding.VisemeRenderer != null)
                GUILayout.Label(NekoLoc.T("bind.mesh") + ": " + _binding.VisemeRenderer.name, NekoStyles.SlotMeta);
            if (_binding.Jaw != null)
                GUILayout.Label(NekoLoc.T("bind.jaw") + ": " + _binding.Jaw.name, NekoStyles.SlotMeta);
            if (_binding.HasSingleShape)
                GUILayout.Label(NekoLoc.T("bind.single") + ": " + _binding.SingleShapeName, NekoStyles.SlotMeta);

            NekoStyles.Rule(3f);

            for (int v = 0; v < NekoVisemes.Count; v++)
            {
                EditorGUILayout.BeginHorizontal();
                bool mapped = !string.IsNullOrEmpty(_binding.ShapeNames[v]);
                var chip = new GUIStyle(NekoStyles.Chip);
                chip.normal.textColor = mapped ? NekoStyles.Good : NekoStyles.Bad;
                GUILayout.Label(NekoVisemes.Names[v], chip, GUILayout.Width(40f));
                string shown = mapped ? _binding.ShapeNames[v] : NekoLoc.T("bind.unmapped");
                GUILayout.Label(shown, NekoStyles.SlotMeta);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            for (int i = 0; i < _binding.Notes.Count; i++)
                EditorGUILayout.HelpBox(_binding.Notes[i], MessageType.None);

            if (GUILayout.Button(NekoLoc.T("bind.rescan"), EditorStyles.miniButton, GUILayout.Width(120f)))
                Rebind();

            EditorGUILayout.EndVertical();
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

        // ------------------------------------------------------------------ bake

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
