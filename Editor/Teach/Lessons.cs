using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NekoSune.WorldUiAssistant
{
    /// <summary>What a lesson step can look at: the panel being edited (may be null) and the scene.</summary>
    internal class LessonContext
    {
        public Canvas panel;
        public AssistantWindow window;
    }

    /// <summary>
    /// One step: an instruction, optionally a "Show me" helper, and either a check against the
    /// user's own work or a quiz question. Steps without either just need "Next".
    /// </summary>
    internal class LessonStep
    {
        public string text;
        public string highlight;                       // Build card to outline, e.g. "Outline", "Add", "Edit"
        public string showMeLabel;
        public Action<LessonContext> showMe;
        public Func<LessonContext, bool> check;
        public string checkHint;                       // shown when the check fails
        public string[] quiz;                          // answer options
        public int answer;                             // index of the right option
        public string quizWhy;                         // shown after answering
    }

    internal class Lesson
    {
        public string id;
        public string title;
        public string summary;
        public string category;                        // "Basics", "Systems", "Templates"
        public int minutes = 5;
        public string badge;                           // badge earned when finished
        public string templateId;                      // template this lesson is about (optional)
        public string videoUrl;                        // optional tutorial video (Phase 5)
        public LessonStep[] steps;
    }

    /// <summary>All lessons, progress (EditorPrefs, per machine) and badges.</summary>
    internal static class LessonBook
    {
        private const string Prefix = "NekoSune.WorldUiAssistant.Lesson.";
        private static List<Lesson> all;

        public static List<Lesson> All
        {
            get
            {
                if (all == null)
                {
                    all = new List<Lesson>();
                    all.AddRange(CoreLessons.Build());
                    all.AddRange(SystemLessons.Build());
                    ApplyVideoLinks(all);
                }
                return all;
            }
        }

        public const string VideoLinksPackage = "Packages/com.nekosune.world-ui-assistant/Editor/Teach/lesson-videos.json";
        public const string VideoLinksProject = "Assets/NekoSune/UIAssistant/lesson-videos.json";

        [System.Serializable] private class VideoLink { public string lesson; public string url; }
        [System.Serializable] private class VideoLinks { public VideoLink[] videos; }

        /// <summary>Tutorial videos per lesson from lesson-videos.json (the package's, then your own in Assets to add or override).</summary>
        private static void ApplyVideoLinks(List<Lesson> lessons)
        {
            foreach (var path in new[] { VideoLinksPackage, VideoLinksProject })
            {
                if (!System.IO.File.Exists(path)) continue;
                try
                {
                    var data = JsonUtility.FromJson<VideoLinks>(System.IO.File.ReadAllText(path));
                    if (data?.videos == null) continue;
                    foreach (var v in data.videos)
                    {
                        var l = lessons.FirstOrDefault(x => x.id == v.lesson);
                        if (l != null && !string.IsNullOrEmpty(v.url)) l.videoUrl = v.url;
                    }
                }
                catch (System.Exception e) { Debug.LogWarning("[NekoUI Assistant] Couldn't read " + path + ": " + e.Message); }
            }
        }

        public static Lesson Get(string id) => All.FirstOrDefault(l => l.id == id);

        public static int StepOf(Lesson l) => Mathf.Clamp(EditorPrefs.GetInt(Prefix + l.id + ".Step", 0), 0, l.steps.Length - 1);
        public static void SetStep(Lesson l, int step) => EditorPrefs.SetInt(Prefix + l.id + ".Step", step);
        public static bool IsDone(Lesson l) => EditorPrefs.GetBool(Prefix + l.id + ".Done", false);

        public static void Finish(Lesson l)
        {
            EditorPrefs.SetBool(Prefix + l.id + ".Done", true);
            SetStep(l, 0);
            Debug.Log("[NekoUI Assistant] Lesson finished: " + l.title + (string.IsNullOrEmpty(l.badge) ? "" : " – badge earned: " + l.badge));
        }

        public static void Reset(Lesson l)
        {
            EditorPrefs.DeleteKey(Prefix + l.id + ".Done");
            EditorPrefs.DeleteKey(Prefix + l.id + ".Step");
        }

        public static int DoneCount(string category = null) => All.Count(l => (category == null || l.category == category) && IsDone(l));
        public static int Count(string category = null) => All.Count(l => category == null || l.category == category);

        /// <summary>The next lesson to suggest: the first unfinished one, basics first.</summary>
        public static Lesson Next => All.FirstOrDefault(l => !IsDone(l));

        public static IEnumerable<string> Badges => All.Where(IsDone).Select(l => l.badge).Where(b => !string.IsNullOrEmpty(b));

        /// <summary>Suggests a lower hint level as the user finishes lessons.</summary>
        public static HintLevel SuggestedLevel
        {
            get
            {
                int done = DoneCount("Basics");
                return done >= 8 ? HintLevel.Expert : done >= 4 ? HintLevel.SomeHelp : HintLevel.FullHelp;
            }
        }

        // ------------------------------------------------------------------ shared check helpers

        public static IEnumerable<T> InPanel<T>(LessonContext c) where T : Component =>
            c.panel ? c.panel.GetComponentsInChildren<T>(true) : Enumerable.Empty<T>();

        public static IEnumerable<Canvas> WorldCanvases() =>
            Resources.FindObjectsOfTypeAll<Canvas>().Where(x => x && !EditorUtility.IsPersistent(x) && x.gameObject.scene.IsValid()
                                                               && x.isRootCanvas && x.renderMode == RenderMode.WorldSpace);

        public static bool Selected<T>() where T : Component => Selection.activeGameObject && Selection.activeGameObject.GetComponent<T>();

        public static void SelectFirst<T>(LessonContext c) where T : Component
        {
            var x = InPanel<T>(c).FirstOrDefault();
            if (x) { Selection.activeGameObject = x.gameObject; EditorGUIUtility.PingObject(x.gameObject); }
        }

        public static void OpenInspector() => EditorApplication.ExecuteMenuItem("Window/General/Inspector");
    }
}
