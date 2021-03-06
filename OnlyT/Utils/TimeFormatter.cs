﻿using System;

namespace OnlyT.Utils
{
    /// <summary>
    /// Formats time values
    /// </summary>
    public static class TimeFormatter
    {
        private static readonly int _secsPerMinute = 60;

        /// <summary>
        /// Gets a timer string
        /// </summary>
        /// <param name="totalSeconds">Seconds remaining or used (can be negative)</param>
        /// <returns>Formatted time (mins and secs)</returns>
        public static string FormatTimerDisplayString(int totalSeconds)
        {
            int mins = Math.Abs(totalSeconds) / _secsPerMinute;
            int secs = Math.Abs(totalSeconds) % _secsPerMinute;

            return $"{mins:D2}:{secs:D2}";
        }
    }
}
