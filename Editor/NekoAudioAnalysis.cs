#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NekoSuneVR.BeatKeyframing
{
    /// <summary>
    /// One frequency lane shown in the timeline.
    /// </summary>
    public struct BandDefinition
    {
        public string Name;
        public string Tooltip;
        public float LowHz;
        public float HighHz;
        public Color Color;

        public BandDefinition(string name, float lowHz, float highHz, Color color, string tooltip)
        {
            Name = name;
            LowHz = lowHz;
            HighHz = highHz;
            Color = color;
            Tooltip = tooltip;
        }
    }

    /// <summary>
    /// Everything the window needs about a clip, computed once when the clip is assigned.
    /// </summary>
    public sealed class NekoAudioAnalysis
    {
        public string ClipPath;
        public string ClipName;
        public float Duration;
        public int Channels;
        public int Frequency;
        public int Columns;
        public float SecondsPerColumn;

        /// <summary>Per-column min/max sample value, for a proper peak envelope.</summary>
        public float[] PeakMin;
        public float[] PeakMax;
        public float[] Rms;

        /// <summary>[band][column], normalised 0..1.</summary>
        public float[][] Bands;

        /// <summary>Detected bass drops, in seconds, strongest first.</summary>
        public List<DropMarker> Drops = new List<DropMarker>();

        public float EstimatedBpm;
        public float BeatOffset;

        public struct DropMarker
        {
            public float Time;
            public float Strength;
        }

        public static readonly BandDefinition[] BandSet =
        {
            new BandDefinition("TRIBAL", 20f, 60f, new Color(0.96f, 0.30f, 0.35f),
                "Sub bass, 20-60 Hz. The floor thump you feel more than hear - kick drums and 808 tails."),
            new BandDefinition("BASS", 60f, 250f, new Color(0.99f, 0.58f, 0.18f),
                "Bass, 60-250 Hz. Bassline and kick body. Drops show up loudest here."),
            new BandDefinition("LOW MID", 250f, 800f, new Color(0.95f, 0.87f, 0.25f),
                "Low mids, 250-800 Hz. Snares, toms, vocal warmth."),
            new BandDefinition("MID", 800f, 4000f, new Color(0.38f, 0.86f, 0.47f),
                "Mids, 800 Hz - 4 kHz. Vocals, leads, most melodic content."),
            new BandDefinition("HIGH", 4000f, 16000f, new Color(0.38f, 0.72f, 0.99f),
                "Highs, 4-16 kHz. Hats, cymbals, air. Good for fast flicker keyframes."),
        };

        private const int FftSize = 2048;
        private const float TargetColumnSeconds = 0.02f;
        private const int MaxColumns = 20000;

        // ------------------------------------------------------------------
        // Entry point
        // ------------------------------------------------------------------

        public static NekoAudioAnalysis Analyse(AudioClip clip, out string error)
        {
            error = null;
            if (clip == null)
            {
                error = "No clip assigned.";
                return null;
            }

            string path = AssetDatabase.GetAssetPath(clip);

            float[] samples;
            int channels;
            int frequency;
            float duration;

            try
            {
                EditorUtility.DisplayProgressBar("Analysing Audio", "Decoding samples...", 0.05f);

                if (!TryLoadSamples(clip, out samples, out channels, out frequency, out duration, out error))
                    return null;

                if (samples == null || samples.Length == 0)
                {
                    error = "The clip decoded to zero samples.";
                    return null;
                }

                EditorUtility.DisplayProgressBar("Analysing Audio", "Mixing down...", 0.2f);
                float[] mono = MixToMono(samples, channels);

                var analysis = new NekoAudioAnalysis
                {
                    ClipPath = path,
                    ClipName = clip.name,
                    Channels = channels,
                    Frequency = frequency,
                    Duration = duration
                };

                int hop = Mathf.Max(1, Mathf.RoundToInt(frequency * TargetColumnSeconds));
                int columns = Mathf.Max(1, mono.Length / hop);
                if (columns > MaxColumns)
                {
                    hop = Mathf.Max(1, mono.Length / MaxColumns);
                    columns = Mathf.Max(1, mono.Length / hop);
                }

                analysis.Columns = columns;
                analysis.SecondsPerColumn = hop / (float)frequency;

                EditorUtility.DisplayProgressBar("Analysing Audio", "Building waveform...", 0.35f);
                analysis.BuildEnvelope(mono, hop);

                EditorUtility.DisplayProgressBar("Analysing Audio", "Running frequency analysis...", 0.5f);
                float[] onsetEnvelope;
                analysis.BuildSpectrum(mono, hop, out onsetEnvelope);

                EditorUtility.DisplayProgressBar("Analysing Audio", "Finding drops...", 0.9f);
                analysis.DetectDrops();
                analysis.EstimateTempo(onsetEnvelope);

                return analysis;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Debug.LogException(ex);
                return null;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // ------------------------------------------------------------------
        // Sample loading
        // ------------------------------------------------------------------

        /// <summary>
        /// Reads raw samples out of any imported clip - mp3, ogg, wav, aiff, anything Unity accepts.
        ///
        /// This is the fix for "some audio shows no waveform". AudioClip.GetData only returns real
        /// data when the clip is decompressed in memory and preloaded. An mp3/ogg imported with the
        /// default Compressed In Memory (or Streaming) load type hands back a buffer of silence and
        /// reports success, so the waveform draws as a flat line with no error anywhere.
        ///
        /// So: flip the importer to Decompress On Load + preload, reimport, read, then put the
        /// original import settings back exactly as they were.
        /// </summary>
        public static bool TryLoadSamples(
            AudioClip clip,
            out float[] samples,
            out int channels,
            out int frequency,
            out float duration,
            out string error)
        {
            samples = null;
            error = null;
            channels = clip.channels;
            frequency = clip.frequency;
            duration = clip.length;

            string path = AssetDatabase.GetAssetPath(clip);
            var importer = string.IsNullOrEmpty(path) ? null : AssetImporter.GetAtPath(path) as AudioImporter;

            // Procedural or in-memory clip: nothing to reimport, just read it.
            if (importer == null)
            {
                samples = ReadData(clip);
                if (samples == null)
                {
                    error = "Unity refused to read samples from this clip and it has no importer to adjust.";
                    return false;
                }
                return true;
            }

            bool alreadyUsable = clip.loadType == AudioClipLoadType.DecompressOnLoad
                                 && clip.preloadAudioData
                                 && clip.loadState == AudioDataLoadState.Loaded;

            if (alreadyUsable)
            {
                samples = ReadData(clip);
                if (samples != null && !IsSilent(samples))
                    return true;
            }

            var original = importer.defaultSampleSettings;
            bool originalPreload = ReadAudioFlag(importer, original, "preloadAudioData", true);
            bool originalBackground = ReadAudioFlag(importer, original, "loadInBackground", false);
            bool changed = false;

            try
            {
                var coerced = original;
                coerced.loadType = AudioClipLoadType.DecompressOnLoad;
                coerced = WriteAudioFlag(importer, coerced, "preloadAudioData", true);
                coerced = WriteAudioFlag(importer, coerced, "loadInBackground", false);

                importer.defaultSampleSettings = coerced;
                changed = true;

                importer.SaveAndReimport();
                AssetDatabase.ImportAsset(path,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

                // The reimport usually replaces the AudioClip instance, so the old reference goes
                // stale - but not always, and LoadAssetAtPath can come back empty right after a
                // reimport. Try each route before giving up, ending with the original reference,
                // which Unity's overloaded null check reports as null only if it really was
                // destroyed.
                var reloaded = AssetDatabase.LoadAssetAtPath<AudioClip>(path);

                if (reloaded == null)
                {
                    reloaded = AssetDatabase.LoadAllAssetsAtPath(path)
                        .OfType<AudioClip>()
                        .FirstOrDefault();
                }

                if (reloaded == null && clip != null)
                    reloaded = clip;

                if (reloaded == null)
                {
                    error = "The clip could not be reloaded after reimport:\n" + path;
                    return false;
                }

                channels = reloaded.channels;
                frequency = reloaded.frequency;
                duration = reloaded.length;

                if (reloaded.loadState != AudioDataLoadState.Loaded)
                    reloaded.LoadAudioData();

                samples = ReadData(reloaded);

                if (samples == null)
                {
                    error = "Unity could not decode this clip even after forcing Decompress On Load.";
                    return false;
                }

                if (IsSilent(samples))
                {
                    error = "This clip decoded to complete silence. It may be a corrupt or empty file.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                if (changed)
                {
                    // Always put the user's import settings back - leaving a 5 minute track
                    // decompressed in memory would quietly cost hundreds of MB at runtime.
                    try
                    {
                        var restored = original;
                        restored = WriteAudioFlag(importer, restored, "preloadAudioData", originalPreload);
                        restored = WriteAudioFlag(importer, restored, "loadInBackground", originalBackground);

                        importer.defaultSampleSettings = restored;
                        importer.SaveAndReimport();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning(
                            "[NekoBeat] Could not restore the original import settings on " + path + ": " + ex.Message);
                    }
                }
            }
        }

        private const System.Reflection.BindingFlags MemberFlags =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public;

        /// <summary>
        /// Reads an import flag that Unity has moved between releases.
        ///
        /// preloadAudioData and loadInBackground used to live on AudioImporter; from 2022.2 they
        /// are per-platform fields on AudioImporterSampleSettings and the old properties are
        /// obsolete-as-error. Going through reflection means this compiles and runs on both,
        /// rather than pinning the tool to one Unity version.
        /// </summary>
        private static bool ReadAudioFlag(
            AudioImporter importer,
            AudioImporterSampleSettings settings,
            string name,
            bool fallback)
        {
            var field = typeof(AudioImporterSampleSettings).GetField(name, MemberFlags);
            if (field != null && field.FieldType == typeof(bool))
                return (bool)field.GetValue(settings);

            var property = typeof(AudioImporter).GetProperty(name, MemberFlags);
            if (property != null && property.PropertyType == typeof(bool) && property.CanRead)
                return (bool)property.GetValue(importer, null);

            return fallback;
        }

        /// <summary>
        /// Writes one of those flags to whichever location this Unity version uses. Returns the
        /// settings struct, which the caller must keep - on newer versions the value lives inside
        /// it, and structs are copied by value.
        /// </summary>
        private static AudioImporterSampleSettings WriteAudioFlag(
            AudioImporter importer,
            AudioImporterSampleSettings settings,
            string name,
            bool value)
        {
            var field = typeof(AudioImporterSampleSettings).GetField(name, MemberFlags);
            if (field != null && field.FieldType == typeof(bool))
            {
                object boxed = settings;
                field.SetValue(boxed, value);
                return (AudioImporterSampleSettings)boxed;
            }

            var property = typeof(AudioImporter).GetProperty(name, MemberFlags);
            if (property != null && property.PropertyType == typeof(bool) && property.CanWrite)
                property.SetValue(importer, value, null);

            return settings;
        }

        private static float[] ReadData(AudioClip clip)
        {
            long total = (long)clip.samples * clip.channels;
            if (total <= 0 || total > int.MaxValue)
                return null;

            var buffer = new float[total];
            return clip.GetData(buffer, 0) ? buffer : null;
        }

        private static bool IsSilent(float[] samples)
        {
            // Sample sparsely - a real track hits something non-trivial almost immediately.
            int step = Mathf.Max(1, samples.Length / 20000);
            for (int i = 0; i < samples.Length; i += step)
            {
                if (Mathf.Abs(samples[i]) > 0.0005f)
                    return false;
            }
            return true;
        }

        private static float[] MixToMono(float[] interleaved, int channels)
        {
            if (channels <= 1)
                return interleaved;

            int frames = interleaved.Length / channels;
            var mono = new float[frames];
            float scale = 1f / channels;

            for (int i = 0; i < frames; i++)
            {
                float sum = 0f;
                int baseIndex = i * channels;
                for (int c = 0; c < channels; c++)
                    sum += interleaved[baseIndex + c];
                mono[i] = sum * scale;
            }

            return mono;
        }

        // ------------------------------------------------------------------
        // Envelope
        // ------------------------------------------------------------------

        private void BuildEnvelope(float[] mono, int hop)
        {
            PeakMin = new float[Columns];
            PeakMax = new float[Columns];
            Rms = new float[Columns];

            for (int column = 0; column < Columns; column++)
            {
                int start = column * hop;
                int end = Mathf.Min(start + hop, mono.Length);

                float min = 0f;
                float max = 0f;
                float sumSquares = 0f;

                for (int i = start; i < end; i++)
                {
                    float value = mono[i];
                    if (value < min) min = value;
                    if (value > max) max = value;
                    sumSquares += value * value;
                }

                int count = Mathf.Max(1, end - start);
                PeakMin[column] = min;
                PeakMax[column] = max;
                Rms[column] = Mathf.Sqrt(sumSquares / count);
            }
        }

        // ------------------------------------------------------------------
        // Spectrum
        // ------------------------------------------------------------------

        private void BuildSpectrum(float[] mono, int hop, out float[] onsetEnvelope)
        {
            int bandCount = BandSet.Length;
            Bands = new float[bandCount][];
            for (int b = 0; b < bandCount; b++)
                Bands[b] = new float[Columns];

            onsetEnvelope = new float[Columns];

            var window = BuildHannWindow(FftSize);
            var real = new float[FftSize];
            var imaginary = new float[FftSize];
            int half = FftSize / 2;

            float binHz = Frequency / (float)FftSize;

            // Precompute each band's bin range once.
            var binStart = new int[bandCount];
            var binEnd = new int[bandCount];
            for (int b = 0; b < bandCount; b++)
            {
                binStart[b] = Mathf.Clamp(Mathf.FloorToInt(BandSet[b].LowHz / binHz), 1, half - 1);
                binEnd[b] = Mathf.Clamp(Mathf.CeilToInt(BandSet[b].HighHz / binHz), binStart[b] + 1, half);
            }

            var previousMagnitude = new float[half];
            int progressStep = Mathf.Max(1, Columns / 20);

            for (int column = 0; column < Columns; column++)
            {
                if (column % progressStep == 0)
                {
                    EditorUtility.DisplayProgressBar(
                        "Analysing Audio",
                        "Frequency analysis " + (column * 100 / Columns) + "%",
                        0.5f + 0.4f * (column / (float)Columns));
                }

                int centre = column * hop;
                int start = centre - FftSize / 2;

                for (int i = 0; i < FftSize; i++)
                {
                    int index = start + i;
                    float value = (index >= 0 && index < mono.Length) ? mono[index] : 0f;
                    real[i] = value * window[i];
                    imaginary[i] = 0f;
                }

                Fft(real, imaginary);

                float flux = 0f;
                for (int bin = 0; bin < half; bin++)
                {
                    float magnitude = Mathf.Sqrt(real[bin] * real[bin] + imaginary[bin] * imaginary[bin]);

                    // Positive spectral flux only - we care about energy arriving, not leaving.
                    float delta = magnitude - previousMagnitude[bin];
                    if (delta > 0f) flux += delta;

                    previousMagnitude[bin] = magnitude;
                    real[bin] = magnitude;
                }

                onsetEnvelope[column] = flux;

                for (int b = 0; b < bandCount; b++)
                {
                    float sum = 0f;
                    for (int bin = binStart[b]; bin < binEnd[b]; bin++)
                        sum += real[bin];

                    Bands[b][column] = sum / (binEnd[b] - binStart[b]);
                }
            }

            NormaliseBands();
        }

        /// <summary>
        /// Scale each band by its own 98th percentile rather than its maximum, so a single
        /// transient spike cannot flatten the whole lane into invisibility.
        /// </summary>
        private void NormaliseBands()
        {
            foreach (var band in Bands)
            {
                float reference = Percentile(band, 0.98f);
                if (reference <= 1e-6f)
                    reference = 1e-6f;

                for (int i = 0; i < band.Length; i++)
                    band[i] = Mathf.Clamp01(band[i] / reference);
            }
        }

        private static float Percentile(float[] values, float percentile)
        {
            if (values.Length == 0) return 0f;

            var copy = new float[values.Length];
            Array.Copy(values, copy, values.Length);
            Array.Sort(copy);

            int index = Mathf.Clamp(Mathf.RoundToInt((copy.Length - 1) * percentile), 0, copy.Length - 1);
            return copy[index];
        }

        private static float[] BuildHannWindow(int size)
        {
            var window = new float[size];
            for (int i = 0; i < size; i++)
                window[i] = 0.5f * (1f - Mathf.Cos(2f * Mathf.PI * i / (size - 1)));
            return window;
        }

        /// <summary>In-place iterative radix-2 Cooley-Tukey. Length must be a power of two.</summary>
        private static void Fft(float[] real, float[] imaginary)
        {
            int n = real.Length;

            // Bit-reversal permutation.
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1)
                    j ^= bit;
                j ^= bit;

                if (i < j)
                {
                    float tr = real[i]; real[i] = real[j]; real[j] = tr;
                    float ti = imaginary[i]; imaginary[i] = imaginary[j]; imaginary[j] = ti;
                }
            }

            for (int length = 2; length <= n; length <<= 1)
            {
                float angle = -2f * Mathf.PI / length;
                float wReal = Mathf.Cos(angle);
                float wImaginary = Mathf.Sin(angle);

                for (int i = 0; i < n; i += length)
                {
                    float curReal = 1f;
                    float curImaginary = 0f;

                    for (int j = 0; j < length / 2; j++)
                    {
                        int even = i + j;
                        int odd = i + j + length / 2;

                        float oddReal = real[odd] * curReal - imaginary[odd] * curImaginary;
                        float oddImaginary = real[odd] * curImaginary + imaginary[odd] * curReal;

                        real[odd] = real[even] - oddReal;
                        imaginary[odd] = imaginary[even] - oddImaginary;
                        real[even] += oddReal;
                        imaginary[even] += oddImaginary;

                        float nextReal = curReal * wReal - curImaginary * wImaginary;
                        curImaginary = curReal * wImaginary + curImaginary * wReal;
                        curReal = nextReal;
                    }
                }
            }
        }

        // ------------------------------------------------------------------
        // Drops
        // ------------------------------------------------------------------

        /// <summary>
        /// A drop is low-end energy jumping hard after a lull. Comparing a short window against
        /// a long trailing average catches exactly that shape, and ignores music that is simply
        /// loud all the way through.
        /// </summary>
        private void DetectDrops()
        {
            Drops.Clear();
            if (Bands == null || Columns < 8) return;

            var lowEnd = new float[Columns];
            for (int i = 0; i < Columns; i++)
                lowEnd[i] = (Bands[0][i] + Bands[1][i]) * 0.5f;

            int shortWindow = Mathf.Max(1, Mathf.RoundToInt(0.12f / SecondsPerColumn));

            // The trailing window has to span a whole section, not a couple of bars. At 2 seconds
            // every ordinary bass gap in a trap beat reads as a drop - measured on a real track
            // that produced 64 "drops" in five minutes. Six seconds, clamped to a quarter of the
            // clip so short loops still work, brings that down to the handful that are real.
            int longWindow = Mathf.Max(
                shortWindow * 4,
                Mathf.Min(Mathf.RoundToInt(6.0f / SecondsPerColumn), Columns / 4));

            int minSpacing = Mathf.Max(1, Mathf.RoundToInt(1.5f / SecondsPerColumn));

            var candidates = new List<DropMarker>();

            for (int i = longWindow; i < Columns - shortWindow; i++)
            {
                float after = Mean(lowEnd, i, i + shortWindow);
                float before = Mean(lowEnd, i - longWindow, i);

                // Must be genuinely loud, not just relatively louder than near-silence.
                if (after < 0.38f)
                    continue;

                // And there has to have been a real lull first. Without this, bass that is simply
                // loud throughout keeps re-triggering.
                if (before > 0.6f * after)
                    continue;

                float ratio = after / (before + 0.05f);
                if (ratio < 2.5f)
                    continue;

                candidates.Add(new DropMarker
                {
                    Time = i * SecondsPerColumn,
                    Strength = ratio * after
                });
            }

            // Non-maximum suppression: keep the strongest candidate in each cluster.
            candidates.Sort((a, b) => b.Strength.CompareTo(a.Strength));
            float spacingSeconds = minSpacing * SecondsPerColumn;

            foreach (var candidate in candidates)
            {
                bool tooClose = false;
                foreach (var accepted in Drops)
                {
                    if (Mathf.Abs(accepted.Time - candidate.Time) < spacingSeconds)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                    Drops.Add(candidate);

                if (Drops.Count >= 24)
                    break;
            }

            // Finally, judge each drop against the biggest one in this track rather than against
            // a fixed number. A song's own dynamic range is the only sane reference - the same
            // absolute threshold that suits a loud master would flood a quiet one with markers.
            if (Drops.Count > 0)
            {
                float strongest = Drops.Max(d => d.Strength);
                Drops.RemoveAll(d => d.Strength < 0.3f * strongest);
            }

            Drops.Sort((a, b) => a.Time.CompareTo(b.Time));
        }

        private static float Mean(float[] values, int start, int end)
        {
            start = Mathf.Max(0, start);
            end = Mathf.Min(values.Length, end);
            if (end <= start) return 0f;

            float sum = 0f;
            for (int i = start; i < end; i++)
                sum += values[i];
            return sum / (end - start);
        }

        // ------------------------------------------------------------------
        // Tempo
        // ------------------------------------------------------------------

        /// <summary>
        /// Autocorrelation of the onset envelope. Good enough for a beat grid on steady
        /// electronic music; it is reported as an estimate and can be overridden in the UI.
        /// </summary>
        private void EstimateTempo(float[] onsetEnvelope)
        {
            EstimatedBpm = 0f;
            BeatOffset = 0f;

            if (onsetEnvelope == null || onsetEnvelope.Length < 32)
                return;

            // Flatten the envelope so loud sections do not dominate the correlation.
            var normalised = new float[onsetEnvelope.Length];
            float mean = Mean(onsetEnvelope, 0, onsetEnvelope.Length);
            for (int i = 0; i < onsetEnvelope.Length; i++)
                normalised[i] = Mathf.Max(0f, onsetEnvelope[i] - mean);

            int minLag = Mathf.Max(1, Mathf.RoundToInt(60f / 200f / SecondsPerColumn)); // 200 BPM
            int maxLag = Mathf.RoundToInt(60f / 60f / SecondsPerColumn);                // 60 BPM
            maxLag = Mathf.Min(maxLag, normalised.Length / 2);

            if (maxLag <= minLag)
                return;

            float bestScore = 0f;
            int bestLag = 0;

            for (int lag = minLag; lag <= maxLag; lag++)
            {
                float score = 0f;
                for (int i = 0; i + lag < normalised.Length; i++)
                    score += normalised[i] * normalised[i + lag];

                score /= (normalised.Length - lag);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestLag = lag;
                }
            }

            if (bestLag <= 0)
                return;

            EstimatedBpm = 60f / (bestLag * SecondsPerColumn);

            // Anchor the grid on the strongest onset inside the first beat period.
            float strongest = 0f;
            for (int i = 0; i < Mathf.Min(bestLag, normalised.Length); i++)
            {
                if (normalised[i] > strongest)
                {
                    strongest = normalised[i];
                    BeatOffset = i * SecondsPerColumn;
                }
            }
        }

        /// <summary>Band value at a time, 0..1. Used for the cursor readout.</summary>
        public float SampleBand(int band, float time)
        {
            if (Bands == null || band < 0 || band >= Bands.Length || Columns == 0)
                return 0f;

            int column = Mathf.Clamp(Mathf.RoundToInt(time / SecondsPerColumn), 0, Columns - 1);
            return Bands[band][column];
        }
    }
}
#endif
