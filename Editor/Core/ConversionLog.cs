using System.Collections.Generic;
using UnityEditor;

namespace MotionConverter
{
    public enum LogLevel { Info, Warning, Error }

    /// <summary>Collects messages from a conversion so the window can show them in one place.</summary>
    public class ConversionLog
    {
        public readonly List<(LogLevel level, string text)> Entries = new List<(LogLevel, string)>();

        public void Info(string text) => Entries.Add((LogLevel.Info, text));
        public void Warn(string text) => Entries.Add((LogLevel.Warning, text));
        public void Error(string text) => Entries.Add((LogLevel.Error, text));
        public void Clear() => Entries.Clear();

        public static MessageType ToMessageType(LogLevel level) =>
            level == LogLevel.Error ? MessageType.Error :
            level == LogLevel.Warning ? MessageType.Warning : MessageType.None;
    }
}
