using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace NekoSune.LipSync.Editor
{
    /// <summary>
    /// Editor-only AudioClip preview used by Lip Sync and Animation Tools.
    /// UnityEditor.AudioUtil is internal, so every call is reflection with graceful fallbacks.
    /// </summary>
    internal static class NekoAudioPreview
    {
        static Type _audioUtil;
        static bool _resolved;

        static Type AudioUtil
        {
            get
            {
                if (_resolved) return _audioUtil;
                _resolved = true;
                Assembly asm = typeof(AudioImporter).Assembly;
                _audioUtil = asm.GetType("UnityEditor.AudioUtil");
                return _audioUtil;
            }
        }

        public static bool Supported { get { return AudioUtil != null; } }

        public static void Play(AudioClip clip)
        {
            if (clip == null || AudioUtil == null) return;
            if (Invoke("PlayPreviewClip", new object[] { clip, 0, false }, new[] { typeof(AudioClip), typeof(int), typeof(bool) })) return;
            if (Invoke("PlayClip", new object[] { clip, 0, false }, new[] { typeof(AudioClip), typeof(int), typeof(bool) })) return;
            Invoke("PlayClip", new object[] { clip }, new[] { typeof(AudioClip) });
        }

        public static void PlayAt(AudioClip clip, float seconds)
        {
            if (clip == null) return;
            Stop();
            Play(clip);
            Seek(clip, seconds);
        }

        public static void Seek(AudioClip clip, float seconds)
        {
            if (clip == null || AudioUtil == null) return;
            int sample = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp(seconds, 0f, clip.length) * clip.frequency), 0, Mathf.Max(0, clip.samples - 1));
            if (Invoke("SetPreviewClipSamplePosition", new object[] { clip, sample }, new[] { typeof(AudioClip), typeof(int) })) return;
            Invoke("SetClipSamplePosition", new object[] { clip, sample }, new[] { typeof(AudioClip), typeof(int) });
        }

        public static void Stop()
        {
            if (AudioUtil == null) return;
            if (Invoke("StopAllPreviewClips", null, Type.EmptyTypes)) return;
            Invoke("StopAllClips", null, Type.EmptyTypes);
        }

        public static bool IsPlaying(AudioClip clip)
        {
            if (clip == null || AudioUtil == null) return false;
            object r = Call("IsPreviewClipPlaying", null, Type.EmptyTypes);
            if (r is bool) return (bool)r;
            r = Call("IsClipPlaying", new object[] { clip }, new[] { typeof(AudioClip) });
            return r is bool && (bool)r;
        }

        static bool Invoke(string name, object[] args, Type[] sig)
        {
            bool ok;
            Call(name, args, sig, out ok);
            return ok;
        }

        static object Call(string name, object[] args, Type[] sig)
        {
            bool ok;
            return Call(name, args, sig, out ok);
        }

        static object Call(string name, object[] args, Type[] sig, out bool ok)
        {
            ok = false;
            if (AudioUtil == null) return null;
            MethodInfo m = AudioUtil.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, sig, null);
            if (m == null) return null;
            try
            {
                object result = m.Invoke(null, args);
                ok = true;
                return result;
            }
            catch { return null; }
        }
    }
}
