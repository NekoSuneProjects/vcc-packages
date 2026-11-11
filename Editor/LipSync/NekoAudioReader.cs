using System;
using UnityEditor;
using UnityEngine;

namespace NekoSune.LipSync.Editor
{
    internal class NekoAudioBuffer
    {
        public float[] Mono;
        public int SampleRate;
        public float Length { get { return SampleRate > 0 ? (float)Mono.Length / SampleRate : 0f; } }
    }

    /// <summary>
    /// Pulls raw PCM out of an AudioClip. Compressed clips cannot be read directly, so the
    /// importer is flipped to Decompress On Load just long enough to grab the samples and is
    /// then restored exactly as the user had it.
    /// </summary>
    internal static class NekoAudioReader
    {
        public static NekoAudioBuffer Read(AudioClip clip, out string error)
        {
            error = null;
            if (clip == null) { error = NekoLoc.T("err.noAudio"); return null; }

            string path = AssetDatabase.GetAssetPath(clip);
            var importer = string.IsNullOrEmpty(path) ? null : AssetImporter.GetAtPath(path) as AudioImporter;

            bool changed = false;
            AudioImporterSampleSettings original = default(AudioImporterSampleSettings);

            try
            {
                if (importer != null)
                {
                    original = importer.defaultSampleSettings;
                    if (original.loadType != AudioClipLoadType.DecompressOnLoad)
                    {
                        AudioImporterSampleSettings temp = original;
                        temp.loadType = AudioClipLoadType.DecompressOnLoad;
                        importer.defaultSampleSettings = temp;
                        importer.SaveAndReimport();
                        changed = true;
                        clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path) ?? clip;
                    }
                }

                if (clip.loadState != AudioDataLoadState.Loaded)
                    clip.LoadAudioData();

                int channels = Mathf.Max(1, clip.channels);
                int frames = clip.samples;
                if (frames <= 0) { error = NekoLoc.T("err.emptyAudio"); return null; }

                var interleaved = new float[frames * channels];
                if (!clip.GetData(interleaved, 0))
                {
                    error = NekoLoc.T("err.readFailed");
                    return null;
                }

                var mono = new float[frames];
                if (channels == 1)
                {
                    Array.Copy(interleaved, mono, frames);
                }
                else
                {
                    float inv = 1f / channels;
                    for (int i = 0; i < frames; i++)
                    {
                        float sum = 0f;
                        int b = i * channels;
                        for (int c = 0; c < channels; c++) sum += interleaved[b + c];
                        mono[i] = sum * inv;
                    }
                }

                RemoveDcOffset(mono);

                return new NekoAudioBuffer { Mono = mono, SampleRate = clip.frequency };
            }
            catch (Exception e)
            {
                error = e.Message;
                return null;
            }
            finally
            {
                if (changed && importer != null)
                {
                    importer.defaultSampleSettings = original;
                    importer.SaveAndReimport();
                }
            }
        }

        static void RemoveDcOffset(float[] data)
        {
            if (data.Length == 0) return;
            double sum = 0.0;
            for (int i = 0; i < data.Length; i++) sum += data[i];
            float mean = (float)(sum / data.Length);
            if (Mathf.Abs(mean) < 1e-6f) return;
            for (int i = 0; i < data.Length; i++) data[i] -= mean;
        }

        /// <summary>Down-samples the waveform into min/max pairs for a compact preview strip.</summary>
        public static void BuildPeaks(NekoAudioBuffer buffer, int columns, out float[] mins, out float[] maxs)
        {
            columns = Mathf.Max(1, columns);
            mins = new float[columns];
            maxs = new float[columns];
            if (buffer == null || buffer.Mono == null || buffer.Mono.Length == 0) return;

            float[] d = buffer.Mono;
            double per = (double)d.Length / columns;
            for (int c = 0; c < columns; c++)
            {
                int start = (int)(c * per);
                int end = Mathf.Min(d.Length, (int)((c + 1) * per));
                if (end <= start) end = Mathf.Min(d.Length, start + 1);
                float lo = 1f, hi = -1f;
                for (int i = start; i < end; i++)
                {
                    float v = d[i];
                    if (v < lo) lo = v;
                    if (v > hi) hi = v;
                }
                if (lo > hi) { lo = 0f; hi = 0f; }
                mins[c] = lo;
                maxs[c] = hi;
            }
        }
    }
}
