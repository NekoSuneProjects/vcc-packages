#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase.Editor.Api;

namespace NekoSune.Tools
{
    /// <summary>The VRChat platforms an avatar can be uploaded for, and the Unity build target for each.</summary>
    internal static class PublishPlatforms
    {
        public const string Windows = "windows";
        public const string Android = "android";
        public const string IOS = "ios";

        public static readonly string[] All = { Windows, Android, IOS };

        public static string Label(string p) => p == Android ? "Android (Quest)" : p == IOS ? "iOS" : "Windows (PC)";

        public static BuildTarget Target(string p) =>
            p == Android ? BuildTarget.Android : p == IOS ? BuildTarget.iOS : BuildTarget.StandaloneWindows64;

        public static BuildTargetGroup Group(string p) =>
            p == Android ? BuildTargetGroup.Android : p == IOS ? BuildTargetGroup.iOS : BuildTargetGroup.Standalone;

        /// <summary>Unity has the build support module for this platform installed.</summary>
        public static bool Installed(string p) => BuildPipeline.IsBuildTargetSupported(Group(p), Target(p));

        public static string Current
        {
            get
            {
                switch (EditorUserBuildSettings.activeBuildTarget)
                {
                    case BuildTarget.Android: return Android;
                    case BuildTarget.iOS: return IOS;
                    case BuildTarget.StandaloneWindows:
                    case BuildTarget.StandaloneWindows64: return Windows;
                    default: return null;
                }
            }
        }

        public static bool IsCurrent(string p) => Current == p;

        /// <summary>Starts the platform switch. Unity reloads scripts afterwards.</summary>
        public static bool SwitchTo(string p) => EditorUserBuildSettings.SwitchActiveBuildTarget(Group(p), Target(p));
    }

    /// <summary>Content info lists matching the SDK builder panel.</summary>
    internal static class ContentInfo
    {
        public static readonly string[] WarningTags = { "content_sex", "content_adult", "content_violence", "content_gore", "content_horror" };

        public static string WarningLabel(string tag)
        {
            switch (tag)
            {
                case "content_sex": return "Sexually Suggestive";
                case "content_adult": return "Adult Language and Themes";
                case "content_violence": return "Graphic Violence";
                case "content_gore": return "Excessive Gore";
                case "content_horror": return "Extreme Horror";
                default: return tag;
            }
        }

        private static List<VRCAvatarStyle> styles;
        private static Task loading;

        /// <summary>Avatar styles from VRChat ("Anime", "Furry", ...). Empty until loaded (needs an SDK login).</summary>
        public static IReadOnlyList<VRCAvatarStyle> Styles => styles ?? (IReadOnlyList<VRCAvatarStyle>)Array.Empty<VRCAvatarStyle>();

        public static bool StylesLoaded => styles != null && styles.Count > 0;

        public static Task LoadStyles(bool force = false)
        {
            if (!force && (StylesLoaded || loading != null)) return loading ?? Task.CompletedTask;
            return loading = LoadStylesAsync();
        }

        private static async Task LoadStylesAsync()
        {
            try
            {
                var list = await VRCApi.GetAvatarStyles();
                if (list != null && list.Count > 0) styles = list.Where(s => !string.IsNullOrEmpty(s.StyleName)).ToList();
            }
            catch (Exception)
            {
                // Not logged in / offline. The style menus just show "Not Specified" until retried.
            }
            finally
            {
                loading = null;
            }
        }

        /// <summary>Style name or ID → ID (what the upload API expects). Null when unknown/empty.</summary>
        public static string StyleId(string nameOrId)
        {
            if (string.IsNullOrWhiteSpace(nameOrId) || styles == null) return null;
            foreach (var s in styles)
                if (string.Equals(s.StyleName, nameOrId, StringComparison.OrdinalIgnoreCase) || s.ID == nameOrId)
                    return s.ID;
            return null;
        }

        /// <summary>
        /// Renders the last active Scene view at 1200x900 (4:3, VRChat thumbnail shape) and saves a PNG.
        /// Returns the file path, or null when there is no Scene view.
        /// </summary>
        public static string CaptureSceneView(string fileName)
        {
            var view = SceneView.lastActiveSceneView;
            if (view == null || view.camera == null) return null;

            const int w = 1200, h = 900;
            var cam = view.camera;
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            var oldTarget = cam.targetTexture;
            var oldActive = RenderTexture.active;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            try
            {
                cam.aspect = (float)w / h;
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();

                Directory.CreateDirectory(PublisherData.ThumbnailFolder);
                var path = Path.Combine(PublisherData.ThumbnailFolder, fileName + ".png");
                File.WriteAllBytes(path, tex.EncodeToPNG());
                return path;
            }
            finally
            {
                cam.targetTexture = oldTarget;
                cam.ResetAspect();
                RenderTexture.active = oldActive;
                RenderTexture.ReleaseTemporary(rt);
                UnityEngine.Object.DestroyImmediate(tex);
                view.Repaint();
            }
        }
    }
}
#endif
