#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace NekoSuneVR.BeatKeyframing
{
    /// <summary>
    /// Clip preview playback through UnityEditor.AudioUtil.
    ///
    /// Every method here is internal Unity API reached by reflection, and the names have
    /// changed across versions (PlayClip became PlayPreviewClip in 2020). Each call tries the
    /// known aliases and caches whichever one resolved, so the tool keeps working across
    /// versions instead of throwing a NullReferenceException on a missing MethodInfo.
    /// </summary>
    public static class NekoAudioPreview
    {
        private static Type audioUtilType;
        private static MethodInfo playMethod;
        private static MethodInfo stopMethod;
        private static MethodInfo setPositionMethod;
        private static MethodInfo isPlayingMethod;
        private static bool resolved;

        private static Type AudioUtilType
        {
            get
            {
                if (audioUtilType == null)
                    audioUtilType = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
                return audioUtilType;
            }
        }

        private static void Resolve()
        {
            if (resolved) return;
            resolved = true;

            var type = AudioUtilType;
            if (type == null)
            {
                Debug.LogWarning("[NekoBeat] UnityEditor.AudioUtil was not found; preview playback is unavailable.");
                return;
            }

            const BindingFlags Flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            playMethod =
                type.GetMethod("PlayPreviewClip", Flags, null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null)
                ?? type.GetMethod("PlayClip", Flags, null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null)
                ?? type.GetMethod("PlayPreviewClip", Flags, null, new[] { typeof(AudioClip) }, null)
                ?? type.GetMethod("PlayClip", Flags, null, new[] { typeof(AudioClip) }, null);

            stopMethod =
                type.GetMethod("StopAllPreviewClips", Flags, null, Type.EmptyTypes, null)
                ?? type.GetMethod("StopAllClips", Flags, null, Type.EmptyTypes, null);

            setPositionMethod =
                type.GetMethod("SetPreviewClipSamplePosition", Flags, null, new[] { typeof(AudioClip), typeof(int) }, null)
                ?? type.GetMethod("SetClipSamplePosition", Flags, null, new[] { typeof(AudioClip), typeof(int) }, null);

            isPlayingMethod =
                type.GetMethod("IsPreviewClipPlaying", Flags, null, Type.EmptyTypes, null)
                ?? type.GetMethod("IsClipPlaying", Flags, null, new[] { typeof(AudioClip) }, null);
        }

        public static void Play(AudioClip clip)
        {
            if (clip == null) return;
            Resolve();
            if (playMethod == null) return;

            try
            {
                object[] arguments = playMethod.GetParameters().Length == 1
                    ? new object[] { clip }
                    : new object[] { clip, 0, false };

                playMethod.Invoke(null, arguments);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[NekoBeat] Preview playback failed: " + ex.Message);
            }
        }

        public static void StopAll()
        {
            Resolve();
            if (stopMethod == null) return;

            try { stopMethod.Invoke(null, null); }
            catch (Exception ex) { Debug.LogWarning("[NekoBeat] Could not stop preview: " + ex.Message); }
        }

        public static void SetSamplePosition(AudioClip clip, int samplePosition)
        {
            if (clip == null) return;
            Resolve();
            if (setPositionMethod == null) return;

            try
            {
                setPositionMethod.Invoke(
                    null,
                    new object[] { clip, Mathf.Clamp(samplePosition, 0, Mathf.Max(0, clip.samples - 1)) });
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[NekoBeat] Could not seek preview: " + ex.Message);
            }
        }

        public static void SeekToTime(AudioClip clip, float time)
        {
            if (clip == null || clip.length <= 0f) return;
            SetSamplePosition(clip, Mathf.CeilToInt(clip.samples * Mathf.Clamp01(time / clip.length)));
        }

        public static bool IsPlaying(AudioClip clip)
        {
            Resolve();
            if (isPlayingMethod == null) return false;

            try
            {
                object[] arguments = isPlayingMethod.GetParameters().Length == 0
                    ? null
                    : new object[] { clip };

                return (bool)isPlayingMethod.Invoke(null, arguments);
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Read-only-ish view onto Unity's Animation window, so the waveform can line up with the
    /// dope sheet and scrub it.
    ///
    /// All of this is internal API. Every accessor degrades to a sensible default rather than
    /// throwing, so the window still works standalone when no Animation window is open.
    /// </summary>
    public static class NekoAnimationWindowLink
    {
        private static Assembly EditorAssembly => typeof(Editor).Assembly;

        private static Type stateType;
        private static Type animEditorType;
        private static Type dopeSheetType;

        private static Type StateType =>
            stateType ?? (stateType = EditorAssembly.GetType("UnityEditorInternal.AnimationWindowState"));

        private static Type AnimEditorType =>
            animEditorType ?? (animEditorType = EditorAssembly.GetType("UnityEditor.AnimEditor"));

        private static Type DopeSheetType =>
            dopeSheetType ?? (dopeSheetType = EditorAssembly.GetType("UnityEditorInternal.DopeSheetEditor"));

        private const BindingFlags InstanceFlags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static bool IsAvailable
        {
            get
            {
                var state = FirstState();
                return state != null;
            }
        }

        private static UnityEngine.Object FirstState()
        {
            if (StateType == null) return null;
            var instances = Resources.FindObjectsOfTypeAll(StateType);
            return instances.Length > 0 ? instances[0] : null;
        }

        private static object FirstDopeSheet()
        {
            if (AnimEditorType == null) return null;

            var getter = AnimEditorType.GetMethod("get_dopeSheetEditor", InstanceFlags);
            if (getter == null) return null;

            var instances = Resources.FindObjectsOfTypeAll(AnimEditorType);
            if (instances.Length == 0) return null;

            try { return getter.Invoke(instances[0], null); }
            catch { return null; }
        }

        private static T ReadStateProperty<T>(string name, T fallback)
        {
            var state = FirstState();
            if (state == null || StateType == null) return fallback;

            var property = StateType.GetProperty(name, InstanceFlags);
            if (property == null) return fallback;

            try
            {
                object value = property.GetValue(state, null);
                return value is T typed ? typed : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private static T ReadDopeSheetProperty<T>(string name, T fallback)
        {
            object dopeSheet = FirstDopeSheet();
            if (dopeSheet == null || DopeSheetType == null) return fallback;

            var property = DopeSheetType.GetProperty(name, InstanceFlags);
            if (property == null) return fallback;

            try
            {
                object value = property.GetValue(dopeSheet, null);
                return value is T typed ? typed : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        public static float CurrentTime => ReadStateProperty("currentTime", 0f);

        public static bool IsPlaying => ReadStateProperty("playing", false);

        /// <summary>Scrubs the Animation window. Returns false if the property is not writable here.</summary>
        public static bool TrySetCurrentTime(float time)
        {
            var state = FirstState();
            if (state == null || StateType == null) return false;

            var property = StateType.GetProperty("currentTime", InstanceFlags);
            if (property == null || !property.CanWrite) return false;

            try
            {
                property.SetValue(state, Mathf.Max(0f, time), null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>The time range currently visible in the dope sheet.</summary>
        public static Rect ShownArea => ReadDopeSheetProperty("shownArea", new Rect(0, 0, 1, 1));

        public static Rect DopeSheetRect => ReadDopeSheetProperty("rect", new Rect());

        public static Vector2 Translation => ReadDopeSheetProperty("translation", Vector2.zero);

        public static float HierarchyWidth
        {
            get
            {
                if (AnimEditorType == null) return 0f;

                var getter = AnimEditorType.GetMethod("get_hierarchyWidth", InstanceFlags);
                if (getter == null) return 0f;

                var instances = Resources.FindObjectsOfTypeAll(AnimEditorType);
                if (instances.Length == 0) return 0f;

                try { return (float)getter.Invoke(instances[0], null); }
                catch { return 0f; }
            }
        }
    }
}
#endif
