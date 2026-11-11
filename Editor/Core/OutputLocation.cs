using System.IO;
using UnityEditor;
using UnityEngine;

namespace MotionConverter
{
    /// <summary>
    /// Works out where a converted clip goes and saves it there, creating folders as needed.
    /// Every module uses the same layout, so all converted motions end up organised by artist and title.
    /// </summary>
    public static class OutputLocation
    {
        public const string RootFolder = "Assets/[Motions Converted]";

        /// <summary>
        /// Artist and/or title set: Assets/[Motions Converted]/Artist/Title/Artist - Title{suffix}.anim
        /// Neither set: <paramref name="fallbackPath"/>.
        /// </summary>
        public static string PathFor(string artist, string title, string fallbackPath, string suffix = "")
        {
            artist = Clean(artist);
            title = Clean(title);
            if (artist.Length == 0 && title.Length == 0) return fallbackPath;

            string folder = RootFolder + (artist.Length > 0 ? "/" + artist : "") + (title.Length > 0 ? "/" + title : "");
            string file = artist.Length > 0 && title.Length > 0 ? $"{artist} - {title}" : artist + title;
            return $"{folder}/{file}{suffix}.anim";
        }

        /// <summary>Clip pipeline output. Without artist/title: next to the source, as SourceName_Humanoid.anim.</summary>
        public static string PathFor(ConversionSettings settings)
        {
            string source = settings.sourceClip != null ? AssetDatabase.GetAssetPath(settings.sourceClip) : "";
            if (string.IsNullOrEmpty(source)) source = settings.sourceAssetPath ?? "";
            string name = settings.sourceClip != null ? settings.sourceClip.name : "Motion";
            return PathFor(settings.artist, settings.title, $"{FolderOf(source)}/{name}_Humanoid.anim");
        }

        /// <summary>Folder of a project asset path, or "Assets" when there is none.</summary>
        public static string FolderOf(string assetPath) =>
            string.IsNullOrEmpty(assetPath) ? "Assets" : Path.GetDirectoryName(assetPath).Replace('\\', '/');

        /// <summary>Save `clip` at `path`. An existing clip is overwritten in place so references to it keep working.</summary>
        public static AnimationClip Save(AnimationClip clip, string path)
        {
            EnsureFolder(path.Substring(0, path.LastIndexOf('/')));

            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(clip, existing);
                existing.name = Path.GetFileNameWithoutExtension(path);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(clip);
                clip = existing;
            }
            else
            {
                clip.name = Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(clip, path);
            }

            AssetDatabase.SaveAssets();
            return clip;
        }

        public static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            int slash = folder.LastIndexOf('/');
            EnsureFolder(folder.Substring(0, slash));
            AssetDatabase.CreateFolder(folder.Substring(0, slash), folder.Substring(slash + 1));
        }

        private static string Clean(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c.ToString(), "");
            return name.Trim().TrimEnd('.');
        }
    }
}
