#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NekoSuneVR.BeatKeyframing
{
    /// <summary>
    /// Disk cache for completed analyses.
    ///
    /// Analysing a five minute track costs two asset reimports and a few thousand FFTs. Without a
    /// cache that work is thrown away every time Unity reloads the domain - which happens on every
    /// script edit, every package install and every play-mode entry - so the window would come
    /// back blank constantly.
    ///
    /// The cache lives in Library/ rather than Assets/ so Unity never imports it, and it is keyed
    /// on the clip's GUID plus the audio file's last-write time. Swap in a different clip, or edit
    /// the same file, and the key changes and the analysis is redone; nothing else invalidates it.
    ///
    /// Binary, not JSON: a long track holds well over a hundred thousand floats.
    /// </summary>
    public static class NekoAnalysisCache
    {
        private const int Format = 1;
        private const string Magic = "NEKOBEAT";

        private static string CacheDirectory
        {
            get
            {
                return Path.Combine(
                    Directory.GetParent(Application.dataPath).FullName,
                    Path.Combine("Library", "NekoBeatKeyframing"));
            }
        }

        /// <summary>Null when the clip has no asset on disk, which means it cannot be cached.</summary>
        private static string CacheFileFor(AudioClip clip)
        {
            if (clip == null) return null;

            string assetPath = AssetDatabase.GetAssetPath(clip);
            if (string.IsNullOrEmpty(assetPath)) return null;

            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid)) return null;

            long stamp = 0;
            try
            {
                string absolute = Path.Combine(
                    Directory.GetParent(Application.dataPath).FullName, assetPath);

                if (File.Exists(absolute))
                    stamp = File.GetLastWriteTimeUtc(absolute).Ticks;
            }
            catch
            {
                // A missing timestamp only weakens invalidation; it must not break caching.
            }

            return Path.Combine(CacheDirectory, string.Format("{0}_{1}_v{2}.bin", guid, stamp, Format));
        }

        public static bool TryLoad(AudioClip clip, out NekoAudioAnalysis analysis)
        {
            analysis = null;

            string file = CacheFileFor(clip);
            if (string.IsNullOrEmpty(file) || !File.Exists(file))
                return false;

            try
            {
                using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read))
                using (var reader = new BinaryReader(stream))
                {
                    if (reader.ReadString() != Magic || reader.ReadInt32() != Format)
                        return false;

                    var loaded = new NekoAudioAnalysis
                    {
                        ClipPath = reader.ReadString(),
                        ClipName = reader.ReadString(),
                        Duration = reader.ReadSingle(),
                        Channels = reader.ReadInt32(),
                        Frequency = reader.ReadInt32(),
                        Columns = reader.ReadInt32(),
                        SecondsPerColumn = reader.ReadSingle(),
                        EstimatedBpm = reader.ReadSingle(),
                        BeatOffset = reader.ReadSingle()
                    };

                    loaded.PeakMin = ReadFloats(reader);
                    loaded.PeakMax = ReadFloats(reader);
                    loaded.Rms = ReadFloats(reader);

                    int bandCount = reader.ReadInt32();
                    loaded.Bands = new float[bandCount][];
                    for (int i = 0; i < bandCount; i++)
                        loaded.Bands[i] = ReadFloats(reader);

                    int dropCount = reader.ReadInt32();
                    for (int i = 0; i < dropCount; i++)
                    {
                        loaded.Drops.Add(new NekoAudioAnalysis.DropMarker
                        {
                            Time = reader.ReadSingle(),
                            Strength = reader.ReadSingle()
                        });
                    }

                    // A band count that no longer matches the current lane set means the tool was
                    // updated; redo the analysis rather than index past the end of the array.
                    if (bandCount != NekoAudioAnalysis.BandSet.Length)
                        return false;

                    analysis = loaded;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[NekoBeat] Cached analysis could not be read, redoing it: " + ex.Message);
                return false;
            }
        }

        public static void Save(AudioClip clip, NekoAudioAnalysis analysis)
        {
            if (analysis == null) return;

            string file = CacheFileFor(clip);
            if (string.IsNullOrEmpty(file)) return;

            try
            {
                Directory.CreateDirectory(CacheDirectory);

                using (var stream = new FileStream(file, FileMode.Create, FileAccess.Write))
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(Magic);
                    writer.Write(Format);

                    writer.Write(analysis.ClipPath ?? string.Empty);
                    writer.Write(analysis.ClipName ?? string.Empty);
                    writer.Write(analysis.Duration);
                    writer.Write(analysis.Channels);
                    writer.Write(analysis.Frequency);
                    writer.Write(analysis.Columns);
                    writer.Write(analysis.SecondsPerColumn);
                    writer.Write(analysis.EstimatedBpm);
                    writer.Write(analysis.BeatOffset);

                    WriteFloats(writer, analysis.PeakMin);
                    WriteFloats(writer, analysis.PeakMax);
                    WriteFloats(writer, analysis.Rms);

                    int bandCount = analysis.Bands != null ? analysis.Bands.Length : 0;
                    writer.Write(bandCount);
                    for (int i = 0; i < bandCount; i++)
                        WriteFloats(writer, analysis.Bands[i]);

                    writer.Write(analysis.Drops.Count);
                    foreach (var drop in analysis.Drops)
                    {
                        writer.Write(drop.Time);
                        writer.Write(drop.Strength);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[NekoBeat] Could not cache the analysis: " + ex.Message);
            }
        }

        /// <summary>Drops every cached analysis. Only used by the Settings menu.</summary>
        public static void Clear()
        {
            try
            {
                if (Directory.Exists(CacheDirectory))
                    Directory.Delete(CacheDirectory, true);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[NekoBeat] Could not clear the cache: " + ex.Message);
            }
        }

        public static long CacheSizeBytes()
        {
            try
            {
                if (!Directory.Exists(CacheDirectory))
                    return 0;

                long total = 0;
                foreach (string file in Directory.GetFiles(CacheDirectory))
                    total += new FileInfo(file).Length;

                return total;
            }
            catch
            {
                return 0;
            }
        }

        private static void WriteFloats(BinaryWriter writer, float[] values)
        {
            if (values == null)
            {
                writer.Write(0);
                return;
            }

            writer.Write(values.Length);

            // One Buffer.BlockCopy beats a few hundred thousand individual Write calls.
            var bytes = new byte[values.Length * sizeof(float)];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            writer.Write(bytes);
        }

        private static float[] ReadFloats(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count <= 0) return new float[0];

            var bytes = reader.ReadBytes(count * sizeof(float));
            var values = new float[count];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
        }
    }
}
#endif
