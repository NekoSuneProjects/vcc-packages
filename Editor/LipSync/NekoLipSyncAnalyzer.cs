using System;
using UnityEngine;

namespace NekoSune.LipSync.Editor
{
    /// <summary>Result of a bake: per output frame, a weight per viseme plus a mouth-openness scalar.</summary>
    internal class NekoLipSyncTrack
    {
        public int Fps;
        public int FrameCount;
        public float Duration;
        /// <summary>[frame][viseme] in 0..1.</summary>
        public float[][] Weights;
        /// <summary>[frame] in 0..1, derived from the viseme mix.</summary>
        public float[] Openness;
    }

    /// <summary>
    /// Turns a waveform into viseme weights.
    ///
    /// Pass 1 walks the audio at a fixed 100 Hz analysis rate and extracts per-frame spectral
    /// features (band energies, spectral flatness, centroid, flux, and two estimated formants).
    /// Pass 2 turns those features into viseme weights, adapting the vowel prototypes to the
    /// singer's vocal tract so the same settings work on a deep male voice and a high female one.
    /// The result is then enveloped, resampled to the requested frame rate, and normalized.
    /// </summary>
    internal static class NekoLipSyncAnalyzer
    {
        const int AnalysisHz = 100;
        const float AnalysisDt = 1f / AnalysisHz;

        struct Feature
        {
            public float Rms;
            public float Flux;
            public float Centroid;
            public float F1;
            public float F2;
            public float ELow;    // 80 - 350
            public float EMid1;   // 350 - 900
            public float EMid2;   // 900 - 2500
            public float EHigh;   // 2500 - 5500
            public float EVHigh;  // 5500 - 11000
            public float Flatness;
        }

        public static NekoLipSyncTrack Analyze(NekoAudioBuffer audio, NekoLipSyncSettings s,
                                               Func<float, string, bool> progress)
        {
            if (audio == null || audio.Mono == null || audio.Mono.Length == 0) return null;

            int sr = audio.SampleRate;
            float[] pcm = audio.Mono;

            int startSample = Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(0f, s.startSec) * sr), 0, pcm.Length);
            int endSample = s.endSec > 0f
                ? Mathf.Clamp(Mathf.RoundToInt(s.endSec * sr), startSample, pcm.Length)
                : pcm.Length;
            int usable = endSample - startSample;
            if (usable < sr / 20) return null;

            int fftSize = FftSizeFor(s.quality, sr);
            var fft = new NekoFFT(fftSize);
            int bins = fft.Bins;
            float binHz = (float)sr / fftSize;

            int hop = Mathf.Max(1, sr / AnalysisHz);
            int aFrames = Mathf.Max(1, usable / hop);

            var feats = new Feature[aFrames];

            var window = new float[fftSize];
            var re = new float[fftSize];
            var im = new float[fftSize];
            var mag = new float[bins];
            var prevMag = new float[bins];
            var floorMag = new float[bins];
            var smooth = new float[bins];
            bool suppressMusic = !s.cleanVocal;
            int smoothRadius = Mathf.Max(1, Mathf.RoundToInt(160f / binHz)); // ~160 Hz -> kills harmonic ripple

            for (int f = 0; f < aFrames; f++)
            {
                if (progress != null && (f & 63) == 0)
                {
                    if (progress((float)f / aFrames * 0.6f, NekoLoc.T("progress.analyzing")))
                        return null;
                }

                int center = startSample + f * hop;
                int begin = center - fftSize / 2;

                double sq = 0.0;
                for (int i = 0; i < fftSize; i++)
                {
                    int idx = begin + i;
                    float v = (idx >= 0 && idx < pcm.Length) ? pcm[idx] : 0f;
                    window[i] = v;
                    sq += v * v;
                }

                Feature ft = default(Feature);
                ft.Rms = (float)Math.Sqrt(sq / fftSize);

                fft.MagnitudeSpectrum(window, re, im, mag);

                if (suppressMusic)
                {
                    for (int b = 0; b < bins; b++)
                    {
                        float m = mag[b];
                        if (m < floorMag[b]) floorMag[b] = floorMag[b] * 0.85f + m * 0.15f;
                        else                 floorMag[b] = floorMag[b] * 1.0015f + 1e-7f;
                        mag[b] = Mathf.Max(0f, m - floorMag[b] * 1.6f);
                    }
                }

                // Spectral flux (onset strength).
                float flux = 0f;
                for (int b = 0; b < bins; b++)
                {
                    float d = mag[b] - prevMag[b];
                    if (d > 0f) flux += d;
                    prevMag[b] = mag[b];
                }
                ft.Flux = flux;

                ft.ELow   = BandEnergy(mag, binHz,   80f,   350f);
                ft.EMid1  = BandEnergy(mag, binHz,  350f,   900f);
                ft.EMid2  = BandEnergy(mag, binHz,  900f,  2500f);
                ft.EHigh  = BandEnergy(mag, binHz, 2500f,  5500f);
                ft.EVHigh = BandEnergy(mag, binHz, 5500f, 11000f);

                ft.Centroid = Centroid(mag, binHz, 300f, 11000f);
                ft.Flatness = Flatness(mag, binHz, 150f, 4000f);

                SmoothSpectrum(mag, smooth, smoothRadius);
                ft.F1 = PeakFrequency(smooth, binHz, 220f, 1150f);
                ft.F2 = PeakFrequency(smooth, binHz, 950f, 3100f);

                feats[f] = ft;
            }

            // ---- Clip-level statistics ---------------------------------------------------
            float loudRef = Percentile(feats, 0.95f);
            if (loudRef < 1e-5f) loudRef = 1e-5f;
            float fluxRef = PercentileFlux(feats, 0.93f);
            if (fluxRef < 1e-6f) fluxRef = 1e-6f;
            float tractScale = EstimateTractScale(feats, loudRef);

            // ---- Pass 2: classify --------------------------------------------------------
            var raw = new float[aFrames][];
            var openRaw = new float[aFrames];
            float prevRmsNorm = 0f;

            for (int f = 0; f < aFrames; f++)
            {
                if (progress != null && (f & 127) == 0)
                {
                    if (progress(0.6f + (float)f / aFrames * 0.25f, NekoLoc.T("progress.mapping")))
                        return null;
                }

                Feature ft = feats[f];
                float rmsNorm = Mathf.Clamp01(ft.Rms / loudRef);
                var w = new float[NekoVisemes.Count];

                if (rmsNorm < s.silenceThreshold)
                {
                    w[NekoVisemes.Sil] = 1f;
                    raw[f] = w;
                    openRaw[f] = 0f;
                    prevRmsNorm = rmsNorm;
                    continue;
                }

                float total = ft.ELow + ft.EMid1 + ft.EMid2 + ft.EHigh + ft.EVHigh + 1e-9f;
                float rLow   = ft.ELow / total;
                float rMid1  = ft.EMid1 / total;
                float rMid2  = ft.EMid2 / total;
                float rHigh  = ft.EHigh / total;
                float rVHigh = ft.EVHigh / total;

                float voiced = Mathf.Clamp01(1f - Mathf.InverseLerp(0.10f, 0.55f, ft.Flatness));
                float unvoiced = 1f - voiced;

                // --- consonant scores ---
                float sibilance = rVHigh + rHigh * 0.55f;
                float sScore = Mathf.InverseLerp(0.22f, 0.55f, sibilance) * Mathf.Lerp(0.35f, 1f, unvoiced);

                float fricative = Mathf.InverseLerp(0.10f, 0.30f, rHigh) * unvoiced * (1f - sScore);

                float fluxNorm = Mathf.Clamp01(ft.Flux / fluxRef);
                float plosive = Mathf.Clamp01(fluxNorm - 0.45f) / 0.55f * (1f - Mathf.Clamp01(prevRmsNorm * 2.2f));

                float nasal = voiced
                            * Mathf.InverseLerp(0.45f, 0.85f, rLow)
                            * (1f - Mathf.InverseLerp(0.06f, 0.22f, rMid2 + rHigh));

                float rhotic = voiced
                             * Mathf.InverseLerp(0.30f, 0.10f, rHigh + rVHigh)
                             * Gauss(ft.F2, 1450f * tractScale, 260f * tractScale)
                             * 0.55f;

                float consonantness = Mathf.Clamp01(Mathf.Max(Mathf.Max(sScore, fricative),
                                                    Mathf.Max(plosive, Mathf.Max(nasal, rhotic))));

                var cons = new float[NekoVisemes.Count];
                if (consonantness > 0.001f)
                {
                    // sibilant: SS (s/z) is brighter than CH (sh/ch/j)
                    float shSplit = Mathf.InverseLerp(4200f, 7000f, ft.Centroid);
                    cons[NekoVisemes.SS] += sScore * shSplit;
                    cons[NekoVisemes.CH] += sScore * (1f - shSplit);

                    // non-sibilant fricative: FF (f/v) low-ish, TH brighter and weaker
                    float thSplit = Mathf.InverseLerp(3800f, 6000f, ft.Centroid);
                    cons[NekoVisemes.FF] += fricative * (1f - thSplit);
                    cons[NekoVisemes.TH] += fricative * thSplit;

                    // plosive burst placement by burst brightness
                    float pp = 1f - Mathf.InverseLerp(700f, 1800f, ft.Centroid);
                    float kk = Mathf.InverseLerp(2600f, 4200f, ft.Centroid);
                    float dd = Mathf.Clamp01(1f - pp - kk);
                    cons[NekoVisemes.PP] += plosive * pp;
                    cons[NekoVisemes.DD] += plosive * dd;
                    cons[NekoVisemes.KK] += plosive * kk;

                    cons[NekoVisemes.NN] += nasal;
                    cons[NekoVisemes.RR] += rhotic;
                    Normalize(cons);
                }

                // --- vowel scores from the two formants ---
                var vow = new float[NekoVisemes.Count];
                VowelWeights(ft.F1, ft.F2, tractScale, vow);
                // Very low F2 energy usually means a rounded vowel; nudge oh/ou.
                if (rMid2 < 0.12f)
                {
                    vow[NekoVisemes.OU] += 0.15f;
                    vow[NekoVisemes.OH] += 0.10f;
                }
                Normalize(vow);

                for (int v = 0; v < NekoVisemes.Count; v++)
                    w[v] = Mathf.Lerp(vow[v], cons[v], consonantness);

                // --- clarity: push the mix toward the dominant viseme ---
                if (s.clarity > 1.001f)
                {
                    for (int v = 0; v < NekoVisemes.Count; v++)
                        w[v] = Mathf.Pow(w[v], s.clarity);
                }
                Normalize(w);

                // --- how far the mouth actually opens this frame ---
                float loudGain = Mathf.Lerp(1f, Mathf.Pow(rmsNorm, 0.6f), s.volumeToMouth);
                float open = loudGain * (1f - s.consonantClose * consonantness);
                open = Mathf.Clamp01(open * s.strength);

                if (s.liveliness > 0f)
                    open = Mathf.Clamp01(open * (1f + (Hash01(f) - 0.5f) * 0.25f * s.liveliness));

                for (int v = 1; v < NekoVisemes.Count; v++) w[v] *= open;
                w[NekoVisemes.Sil] = Mathf.Clamp01(1f - Sum(w, 1));

                raw[f] = w;
                openRaw[f] = MouthOpenness(w);
                prevRmsNorm = rmsNorm;
            }

            // ---- Envelope (asymmetric attack / release) ----------------------------------
            float attack = Mathf.Max(0.001f, s.attackMs / 1000f);
            float release = Mathf.Max(0.001f, s.releaseMs / 1000f);
            float aCoef = 1f - Mathf.Exp(-AnalysisDt / attack);
            float rCoef = 1f - Mathf.Exp(-AnalysisDt / release);

            var state = new float[NekoVisemes.Count];
            float openState = 0f;
            for (int f = 0; f < aFrames; f++)
            {
                float[] w = raw[f];
                for (int v = 0; v < NekoVisemes.Count; v++)
                {
                    float target = w[v];
                    float c = target > state[v] ? aCoef : rCoef;
                    state[v] += (target - state[v]) * c;
                    w[v] = state[v];
                }
                float oc = openRaw[f] > openState ? aCoef : rCoef;
                openState += (openRaw[f] - openState) * oc;
                openRaw[f] = openState;
            }

            // ---- Resample to the output frame rate ---------------------------------------
            int fps = Mathf.Clamp(s.fps, 5, 240);
            float duration = (float)usable / sr;
            int outFrames = Mathf.Max(2, Mathf.CeilToInt(duration * fps) + 1);
            float offsetSec = s.offsetMs / 1000f;

            var track = new NekoLipSyncTrack
            {
                Fps = fps,
                FrameCount = outFrames,
                Duration = duration,
                Weights = new float[outFrames][],
                Openness = new float[outFrames]
            };

            for (int o = 0; o < outFrames; o++)
            {
                if (progress != null && (o & 255) == 0)
                {
                    if (progress(0.85f + (float)o / outFrames * 0.1f, NekoLoc.T("progress.resampling")))
                        return null;
                }

                float t = o / (float)fps + offsetSec;
                float pos = t * AnalysisHz;
                int i0 = Mathf.Clamp(Mathf.FloorToInt(pos), 0, aFrames - 1);
                int i1 = Mathf.Clamp(i0 + 1, 0, aFrames - 1);
                float frac = Mathf.Clamp01(pos - i0);

                var outW = new float[NekoVisemes.Count];
                bool inside = pos >= -0.5f && pos <= aFrames - 0.5f;
                if (!inside)
                {
                    outW[NekoVisemes.Sil] = 1f;
                    track.Weights[o] = outW;
                    track.Openness[o] = 0f;
                    continue;
                }

                for (int v = 0; v < NekoVisemes.Count; v++)
                    outW[v] = Mathf.Lerp(raw[i0][v], raw[i1][v], frac);

                if (s.normalize)
                {
                    float sum = Sum(outW, 0);
                    if (sum > 1f)
                        for (int v = 0; v < NekoVisemes.Count; v++) outW[v] /= sum;
                }

                track.Weights[o] = outW;
                track.Openness[o] = Mathf.Lerp(openRaw[i0], openRaw[i1], frac);
            }

            return track;
        }

        // -------------------------------------------------------------------------------------

        static int FftSizeFor(int quality, int sampleRate)
        {
            int size;
            if (quality <= 2) size = 512;
            else if (quality <= 4) size = 1024;
            else if (quality <= 6) size = 2048;
            else if (quality <= 8) size = 4096;
            else size = 8192;

            // Keep the window under ~90 ms so fast consonants are not smeared.
            int maxSize = 1;
            while (maxSize * 2 <= sampleRate * 0.09f) maxSize *= 2;
            return Mathf.Clamp(size, 512, Mathf.Max(512, maxSize));
        }

        static float BandEnergy(float[] mag, float binHz, float lo, float hi)
        {
            int b0 = Mathf.Max(1, Mathf.FloorToInt(lo / binHz));
            int b1 = Mathf.Min(mag.Length - 1, Mathf.CeilToInt(hi / binHz));
            float sum = 0f;
            for (int b = b0; b <= b1; b++) sum += mag[b] * mag[b];
            return sum;
        }

        static float Centroid(float[] mag, float binHz, float lo, float hi)
        {
            int b0 = Mathf.Max(1, Mathf.FloorToInt(lo / binHz));
            int b1 = Mathf.Min(mag.Length - 1, Mathf.CeilToInt(hi / binHz));
            float num = 0f, den = 0f;
            for (int b = b0; b <= b1; b++)
            {
                float m = mag[b];
                num += m * b * binHz;
                den += m;
            }
            return den > 1e-9f ? num / den : 0f;
        }

        /// <summary>Spectral flatness: near 1 for noise (unvoiced), near 0 for harmonic (voiced).</summary>
        static float Flatness(float[] mag, float binHz, float lo, float hi)
        {
            int b0 = Mathf.Max(1, Mathf.FloorToInt(lo / binHz));
            int b1 = Mathf.Min(mag.Length - 1, Mathf.CeilToInt(hi / binHz));
            if (b1 <= b0) return 1f;
            double logSum = 0.0, sum = 0.0;
            int n = 0;
            for (int b = b0; b <= b1; b++)
            {
                double m = mag[b] + 1e-9;
                logSum += Math.Log(m);
                sum += m;
                n++;
            }
            double geo = Math.Exp(logSum / n);
            double ari = sum / n;
            return ari > 1e-12 ? Mathf.Clamp01((float)(geo / ari)) : 1f;
        }

        static void SmoothSpectrum(float[] src, float[] dst, int radius)
        {
            int n = src.Length;
            float inv = 1f / (radius * 2 + 1);
            float run = 0f;
            for (int i = -radius; i <= radius; i++) run += src[Mathf.Clamp(i, 0, n - 1)];
            for (int i = 0; i < n; i++)
            {
                dst[i] = run * inv;
                run -= src[Mathf.Clamp(i - radius, 0, n - 1)];
                run += src[Mathf.Clamp(i + radius + 1, 0, n - 1)];
            }
        }

        static float PeakFrequency(float[] smoothed, float binHz, float lo, float hi)
        {
            int b0 = Mathf.Max(1, Mathf.FloorToInt(lo / binHz));
            int b1 = Mathf.Min(smoothed.Length - 2, Mathf.CeilToInt(hi / binHz));
            if (b1 <= b0) return (lo + hi) * 0.5f;

            int best = b0;
            float bestVal = -1f;
            for (int b = b0; b <= b1; b++)
            {
                if (smoothed[b] > bestVal) { bestVal = smoothed[b]; best = b; }
            }

            // Parabolic interpolation around the peak for sub-bin accuracy.
            float y0 = smoothed[best - 1], y1 = smoothed[best], y2 = smoothed[best + 1];
            float denom = y0 - 2f * y1 + y2;
            float delta = Mathf.Abs(denom) > 1e-9f ? 0.5f * (y0 - y2) / denom : 0f;
            delta = Mathf.Clamp(delta, -0.5f, 0.5f);
            return (best + delta) * binHz;
        }

        static float Percentile(Feature[] feats, float p)
        {
            var vals = new float[feats.Length];
            for (int i = 0; i < feats.Length; i++) vals[i] = feats[i].Rms;
            Array.Sort(vals);
            return vals[Mathf.Clamp(Mathf.RoundToInt((vals.Length - 1) * p), 0, vals.Length - 1)];
        }

        static float PercentileFlux(Feature[] feats, float p)
        {
            var vals = new float[feats.Length];
            for (int i = 0; i < feats.Length; i++) vals[i] = feats[i].Flux;
            Array.Sort(vals);
            return vals[Mathf.Clamp(Mathf.RoundToInt((vals.Length - 1) * p), 0, vals.Length - 1)];
        }

        /// <summary>
        /// Vowel formants scale roughly with vocal-tract length. Comparing the clip's median F1
        /// on loud voiced frames against a 560 Hz reference lets one preset fit any voice.
        /// </summary>
        static float EstimateTractScale(Feature[] feats, float loudRef)
        {
            var vals = new System.Collections.Generic.List<float>(feats.Length / 4 + 1);
            for (int i = 0; i < feats.Length; i++)
            {
                Feature f = feats[i];
                if (f.Rms < loudRef * 0.35f) continue;
                if (f.Flatness > 0.30f) continue;   // want voiced frames only
                if (f.F1 < 200f || f.F1 > 1100f) continue;
                vals.Add(f.F1);
            }
            if (vals.Count < 12) return 1f;
            vals.Sort();
            float median = vals[vals.Count / 2];
            return Mathf.Clamp(median / 560f, 0.72f, 1.45f);
        }

        struct VowelProto
        {
            public int Index;
            public float F1;
            public float F2;
        }

        static readonly VowelProto[] Prototypes =
        {
            new VowelProto { Index = NekoVisemes.AA, F1 = 730f, F2 = 1150f },
            new VowelProto { Index = NekoVisemes.E,  F1 = 530f, F2 = 1840f },
            new VowelProto { Index = NekoVisemes.IH, F1 = 380f, F2 = 2100f },
            new VowelProto { Index = NekoVisemes.OH, F1 = 570f, F2 =  870f },
            new VowelProto { Index = NekoVisemes.OU, F1 = 320f, F2 =  790f },
        };

        static void VowelWeights(float f1, float f2, float scale, float[] outWeights)
        {
            // Distances are taken in log-frequency space, which matches how vowels are perceived.
            float lf1 = Mathf.Log(Mathf.Max(80f, f1));
            float lf2 = Mathf.Log(Mathf.Max(200f, f2));

            float sum = 0f;
            var scores = new float[Prototypes.Length];
            for (int i = 0; i < Prototypes.Length; i++)
            {
                VowelProto p = Prototypes[i];
                float d1 = lf1 - Mathf.Log(p.F1 * scale);
                float d2 = lf2 - Mathf.Log(p.F2 * scale);
                // F2 carries most of the vowel identity, so weight it a little heavier.
                float d = d1 * d1 * 1.0f + d2 * d2 * 1.35f;
                float score = Mathf.Exp(-d / (2f * 0.055f));
                scores[i] = score;
                sum += score;
            }

            if (sum < 1e-6f)
            {
                outWeights[NekoVisemes.AA] = 1f;
                return;
            }

            for (int i = 0; i < Prototypes.Length; i++)
                outWeights[Prototypes[i].Index] += scores[i] / sum;
        }

        static float Gauss(float x, float mu, float sigma)
        {
            float d = (x - mu) / Mathf.Max(1e-3f, sigma);
            return Mathf.Exp(-0.5f * d * d);
        }

        static void Normalize(float[] w)
        {
            float sum = 0f;
            for (int i = 0; i < w.Length; i++) sum += w[i];
            if (sum < 1e-6f) return;
            for (int i = 0; i < w.Length; i++) w[i] /= sum;
        }

        static float Sum(float[] w, int from)
        {
            float s = 0f;
            for (int i = from; i < w.Length; i++) s += w[i];
            return s;
        }

        static float MouthOpenness(float[] w)
        {
            float o = 0f;
            for (int v = 0; v < NekoVisemes.Count; v++) o += w[v] * NekoVisemes.Openness[v];
            return Mathf.Clamp01(o);
        }

        /// <summary>Deterministic 0..1 noise so repeated bakes of the same clip stay identical.</summary>
        static float Hash01(int i)
        {
            uint x = (uint)i * 2654435761u;
            x ^= x >> 15;
            x *= 2246822519u;
            x ^= x >> 13;
            return (x & 0xFFFFFF) / (float)0xFFFFFF;
        }
    }
}
