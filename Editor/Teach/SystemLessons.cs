using System.Collections.Generic;

namespace NekoSune.WorldUiAssistant
{
    /// <summary>Lessons for templates and ready-made systems (filled in TemplateLessons.cs).</summary>
    internal static partial class SystemLessons
    {
        public static List<Lesson> Build()
        {
            var list = new List<Lesson>();
            AddTemplateLessons(list);
            return list;
        }

        static partial void AddTemplateLessons(List<Lesson> list);
    }
}
