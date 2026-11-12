using System;
using TMPro;
using UdonSharp;
using UnityEngine;

namespace NekoSune.WorldUiAssistant
{
    /// <summary>
    /// Clock (the player's own local time, or UTC) and/or a countdown to an event time in UTC
    /// ("Party starts in 2d 3h 15m"). Everyone counts down to the same moment wherever they live.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class NekoClock : UdonSharpBehaviour
    {
        public TextMeshProUGUI clockText;
        public bool useUtc;
        public bool showSeconds;

        [Header("Countdown (UTC)")]
        public TextMeshProUGUI countdownText;
        public string eventName = "Next event";
        public int year = 2026, month = 12, day = 31, hour = 20, minute = 0;
        public string startedMessage = "is happening now!";

        private float next;

        private void Update()
        {
            if (Time.time < next) return;
            next = Time.time + 1f;
            var now = useUtc ? DateTime.UtcNow : DateTime.Now;
            if (clockText != null) clockText.text = now.ToString(showSeconds ? "HH:mm:ss" : "HH:mm") + (useUtc ? " UTC" : "");
            if (countdownText != null)
            {
                var target = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc);
                var left = target - DateTime.UtcNow;
                if (left.TotalSeconds <= 0) countdownText.text = eventName + " " + startedMessage;
                else countdownText.text = eventName + " in " +
                                          (left.Days > 0 ? left.Days + "d " : "") + left.Hours + "h " + left.Minutes + "m" +
                                          (left.TotalHours < 1 ? " " + left.Seconds + "s" : "");
            }
        }
    }
}
