using System.Linq;
using UnityEditor;
using UnityEngine;
using S = NekoSune.WorldUiAssistant.AssistantStyles;
using G = NekoSune.WorldUiAssistant.AssistantGUI;

namespace NekoSune.WorldUiAssistant
{
    internal partial class AssistantWindow
    {
        internal enum MainTab { Build, Learn, Doctor, Templates, Glossary }

        private static readonly (MainTab tab, string icon, string label)[] MainTabs =
        {
            (MainTab.Build, "Canvas Icon", "Build"),
            (MainTab.Learn, "d_UnityEditor.InspectorWindow", "Learn"),
            (MainTab.Doctor, "d_console.warnicon.sml", "Doctor"),
            (MainTab.Templates, "Prefab Icon", "Templates"),
            (MainTab.Glossary, "TextAsset Icon", "Glossary"),
        };

        [SerializeField] private MainTab mainTab;
        [SerializeField] private string activeLesson;
        [SerializeField] private int quizPick = -1;
        [SerializeField] private string glossarySearch = "";
        [SerializeField] private bool madeOpen;
        private string lessonMessage;
        private bool lessonMessageOk;

        /// <summary>Panels created by the Assistant remember their theme; hand-made ones don't.</summary>
        public static bool IsAssistantPanel(Canvas c) => c && EditorPrefs.HasKey(ThemePrefPrefix + GlobalObjectId.GetGlobalObjectIdSlow(c));

        private LessonContext Context => new LessonContext { panel = panel, window = this };

        // ------------------------------------------------------------------ tab bar + help settings

        private void DrawMainTabs()
        {
            var bar = GUILayoutUtility.GetRect(1, 28, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(bar, S.HeaderBg);
            EditorGUI.DrawRect(new Rect(bar.x, bar.yMax - 1, bar.width, 1), S.CardBorder);
            bool labels = bar.width >= 470f;
            float w = (bar.width - 16f) / MainTabs.Length;
            for (int i = 0; i < MainTabs.Length; i++)
            {
                var t = MainTabs[i];
                var r = new Rect(bar.x + 8 + w * i, bar.y, w, bar.height - 1);
                bool active = mainTab == t.tab;
                var content = new GUIContent(labels ? " " + t.label : "", EditorGUIUtility.IconContent(t.icon).image, t.label);
                if (GUI.Button(r, content, active ? S.TabActive : S.Tab) && !active)
                {
                    mainTab = t.tab;
                    scroll = Vector2.zero;
                    GUI.FocusControl(null);
                }
                EditorGUIUtility.AddCursorRect(r, MouseCursor.Link);
                if (active) EditorGUI.DrawRect(new Rect(r.x + 6, r.yMax - 3, r.width - 12, 2), S.Accent);
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(new GUIContent("Help", "How much the Assistant explains."), EditorStyles.miniLabel, GUILayout.Width(30));
                int level = EditorGUILayout.Popup((int)Help.Level, Help.LevelNames.Select((n, i) => n).ToArray(), EditorStyles.toolbarPopup, GUILayout.Width(90));
                if (level != (int)Help.Level) Help.Level = (HintLevel)level;
                Help.Explain = GUILayout.Toggle(Help.Explain, new GUIContent("Explain mode", "Show which Unity component and property each setting changes."), EditorStyles.toolbarButton);
                GUILayout.FlexibleSpace();
                var next = LessonBook.Next;
                GUILayout.Label(new GUIContent(LessonBook.DoneCount() + "/" + LessonBook.Count() + " lessons", "Lessons finished on this computer."), EditorStyles.miniLabel);
            }
        }

        // ------------------------------------------------------------------ lesson bar (on top of Build)

        private Lesson ActiveLesson => string.IsNullOrEmpty(activeLesson) ? null : LessonBook.Get(activeLesson);

        private void DrawLessonBar()
        {
            var lesson = ActiveLesson;
            G.Highlight = null;
            if (lesson == null) return;
            int stepIndex = LessonBook.StepOf(lesson);
            var step = lesson.steps[stepIndex];
            G.Highlight = step.highlight;

            G.BeginCard("d_UnityEditor.InspectorWindow", lesson.title + "  ·  step " + (stepIndex + 1) + " of " + lesson.steps.Length);
            var progress = GUILayoutUtility.GetRect(1, 4, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(progress, S.TrackBg);
            EditorGUI.DrawRect(new Rect(progress.x, progress.y, progress.width * stepIndex / lesson.steps.Length, progress.height), S.Accent);
            EditorGUILayout.Space(4);
            GUILayout.Label(step.text, LessonText);

            if (step.quiz != null)
            {
                EditorGUILayout.Space(4);
                for (int i = 0; i < step.quiz.Length; i++)
                    if (GUILayout.Toggle(quizPick == i, " " + step.quiz[i], EditorStyles.radioButton)) quizPick = i;
            }

            if (!string.IsNullOrEmpty(lessonMessage)) G.Report(lessonMessage, lessonMessageOk);
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (step.showMe != null && GUILayout.Button(step.showMeLabel ?? "Show me", GUILayout.Height(24))) step.showMe(Context);
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(stepIndex == 0))
                    if (GUILayout.Button("Back", GUILayout.Height(24), GUILayout.Width(56))) GoToStep(lesson, stepIndex - 1);
                string label = step.check != null ? "Check my work" : step.quiz != null ? "Answer" : stepIndex == lesson.steps.Length - 1 ? "Finish" : "Next";
                var old = GUI.backgroundColor;
                GUI.backgroundColor = S.Accent;
                using (new EditorGUI.DisabledScope(step.quiz != null && quizPick < 0))
                    if (GUILayout.Button(label, GUILayout.Height(24), GUILayout.Width(110))) Advance(lesson, stepIndex, step);
                GUI.backgroundColor = old;
                if (GUILayout.Button(new GUIContent("✕", "Stop the lesson (your place is kept)."), GUILayout.Height(24), GUILayout.Width(26)))
                {
                    activeLesson = null;
                    lessonMessage = null;
                    G.Highlight = null;
                }
            }
            G.EndCard();
        }

        private void Advance(Lesson lesson, int stepIndex, LessonStep step)
        {
            if (step.check != null)
            {
                bool ok;
                try { ok = step.check(Context); }
                catch { ok = false; }
                if (!ok)
                {
                    lessonMessage = "Not yet: " + (step.checkHint ?? "try again.");
                    lessonMessageOk = false;
                    return;
                }
            }
            if (step.quiz != null && quizPick != step.answer)
            {
                lessonMessage = "Not quite. Have another look and try again.";
                lessonMessageOk = false;
                return;
            }
            string why = step.quiz != null ? step.quizWhy : null;
            if (stepIndex + 1 >= lesson.steps.Length)
            {
                LessonBook.Finish(lesson);
                activeLesson = null;
                mainTab = MainTab.Learn;
                lessonMessage = "Lesson finished" + (string.IsNullOrEmpty(lesson.badge) ? "!" : " – badge earned: " + lesson.badge + "!");
                lessonMessageOk = true;
                var suggested = LessonBook.SuggestedLevel;
                if (suggested > Help.Level)
                    lessonMessage += " You're getting good – try the \"" + Help.LevelNames[(int)suggested] + "\" help level.";
                return;
            }
            GoToStep(lesson, stepIndex + 1);
            lessonMessage = string.IsNullOrEmpty(why) ? (step.check != null ? "Well done!" : null) : "Right! " + why;
            lessonMessageOk = true;
        }

        private void GoToStep(Lesson lesson, int step)
        {
            LessonBook.SetStep(lesson, step);
            quizPick = -1;
            lessonMessage = null;
        }

        private void StartLesson(Lesson lesson)
        {
            activeLesson = lesson.id;
            quizPick = -1;
            lessonMessage = null;
            mainTab = MainTab.Build;
            scroll = Vector2.zero;
            if (!string.IsNullOrEmpty(lesson.templateId) && (!panel || showWizard)) mainTab = MainTab.Templates;
        }

        private static GUIStyle lessonText;
        private static GUIStyle LessonText => lessonText ?? (lessonText = new GUIStyle(EditorStyles.wordWrappedLabel) { richText = true, fontSize = 12 });

        // ------------------------------------------------------------------ Learn tab

        private void DrawLearn()
        {
            if (!string.IsNullOrEmpty(lessonMessage) && ActiveLesson == null)
            {
                G.BeginCard(null, null);
                G.Report(lessonMessage, lessonMessageOk);
                G.EndCard();
            }

            G.BeginCard("d_UnityEditor.InspectorWindow", "Your progress");
            int done = LessonBook.DoneCount(), total = LessonBook.Count();
            var bar = GUILayoutUtility.GetRect(1, 8, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(bar, S.TrackBg);
            EditorGUI.DrawRect(new Rect(bar.x, bar.y, bar.width * done / Mathf.Max(1, total), bar.height), S.Success);
            EditorGUILayout.Space(4);
            GUILayout.Label(done + " of " + total + " lessons finished on this computer.", EditorStyles.miniLabel);
            var badges = LessonBook.Badges.ToList();
            if (badges.Count > 0)
            {
                G.Group("Badges");
                Flow(badges, b => G.Chip("★ " + b, S.Warning));
            }
            var next = LessonBook.Next;
            if (next != null)
            {
                EditorGUILayout.Space(6);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("Next: <b>" + next.title + "</b>", LessonText);
                    if (GUILayout.Button(LessonBook.StepOf(next) > 0 ? "Continue" : "Start", GUILayout.Width(80))) StartLesson(next);
                }
            }
            else G.Report("You finished every lesson. Amazing!", true);
            G.EndCard();

            foreach (var category in LessonBook.All.Select(l => l.category).Distinct())
            {
                G.BeginCard(category == "Basics" ? "d_UnityEditor.InspectorWindow" : "Prefab Icon",
                    category + "  (" + LessonBook.DoneCount(category) + "/" + LessonBook.Count(category) + ")",
                    category == "Basics" ? "Start here. Each lesson uses your own panel and ends with something you built." : "Each one goes with a template from the Templates tab and ends with you rebuilding a simpler version by hand.");
                foreach (var lesson in LessonBook.All.Where(l => l.category == category))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        bool isDone = LessonBook.IsDone(lesson);
                        GUILayout.Label(EditorGUIUtility.IconContent(isDone ? "TestPassed" : "TestNormal"), GUILayout.Width(18), GUILayout.Height(18));
                        GUILayout.Label(new GUIContent(lesson.title, lesson.summary), GUILayout.MinWidth(80));
                        GUILayout.FlexibleSpace();
                        GUILayout.Label(lesson.minutes + " min", EditorStyles.miniLabel, GUILayout.Width(44));
                        if (!string.IsNullOrEmpty(lesson.videoUrl) && GUILayout.Button(new GUIContent("▶", "Watch the video for this lesson"), EditorStyles.miniButton, GUILayout.Width(24)))
                            Application.OpenURL(lesson.videoUrl);
                        string label = isDone ? "Again" : LessonBook.StepOf(lesson) > 0 ? "Continue" : "Start";
                        if (GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Width(64)))
                        {
                            if (isDone) LessonBook.Reset(lesson);
                            StartLesson(lesson);
                        }
                    }
                    if (Help.ShowHints) GUILayout.Label(lesson.summary, S.Hint);
                }
                G.EndCard();
            }
        }

        private void Flow(System.Collections.Generic.List<string> items, System.Action<string> draw)
        {
            int perRow = Mathf.Max(1, Mathf.FloorToInt((position.width - 40) / 140));
            for (int i = 0; i < items.Count; i += perRow)
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int j = i; j < Mathf.Min(i + perRow, items.Count); j++) draw(items[j]);
                    GUILayout.FlexibleSpace();
                }
        }

        // ------------------------------------------------------------------ Glossary tab

        private void DrawGlossary()
        {
            G.BeginCard("TextAsset Icon", "Glossary", "Every word the Assistant uses, in plain English.");
            glossarySearch = EditorGUILayout.TextField(glossarySearch, EditorStyles.toolbarSearchField);
            EditorGUILayout.Space(4);
            foreach (var (term, meaning) in Glossary.Terms.OrderBy(t => t.term))
            {
                if (!string.IsNullOrEmpty(glossarySearch) &&
                    term.IndexOf(glossarySearch, System.StringComparison.OrdinalIgnoreCase) < 0 &&
                    meaning.IndexOf(glossarySearch, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                GUILayout.Label(term, EditorStyles.boldLabel);
                GUILayout.Label(meaning, EditorStyles.wordWrappedLabel);
                EditorGUILayout.Space(4);
            }
            G.EndCard();
        }

        // ------------------------------------------------------------------ "What did it make?"

        private void DrawMadeCard()
        {
            G.BeginCard(null, null, key: "Made");
            madeOpen = EditorGUILayout.Foldout(madeOpen, new GUIContent(" What did it make?", EditorGUIUtility.IconContent("d_UnityEditor.InspectorWindow").image), true, G.FoldoutTitle);
            if (madeOpen)
            {
                S.Line();
                EditorGUILayout.Space(4);
                G.Hint("Every Unity component in this panel. Nothing here is hidden or special – you can open any of it in the Inspector.");
                var groups = panel.GetComponentsInChildren<Component>(true)
                    .Where(c => c && (c.hideFlags & HideFlags.HideInInspector) == 0)
                    .GroupBy(c => c.GetType().Name)
                    .OrderByDescending(g => g.Count());
                foreach (var g in groups)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label(new GUIContent(" " + ObjectNames.NicifyVariableName(g.Key), EditorGUIUtility.ObjectContent(null, g.First().GetType()).image), GUILayout.Width(Mathf.Min(220, position.width * 0.42f)), GUILayout.Height(18));
                        G.Chip("× " + g.Count());
                        GUILayout.Label(ComponentGuide.Explain(g.Key), S.Hint, GUILayout.MinWidth(40));
                        if (GUILayout.Button(new GUIContent("Select", "Select every object with this component."), EditorStyles.miniButton, GUILayout.Width(50)))
                            Selection.objects = g.Select(c => (Object)c.gameObject).Distinct().ToArray();
                    }
                }
            }
            G.EndCard();
        }
    }
}
