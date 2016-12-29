using System;
using System.Diagnostics;

namespace Microsoft.Extensions.Correlation
{
    public class DateTimeStopwatch
    {
        //last machine boot time if Stopwatch is HighResolution
        private static DateTime _stopwatchStartTime = DateTime.UtcNow.AddSeconds(-1 * Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency);

        public static DateTime GetTime(long ticks)
        {
            return _stopwatchStartTime.AddSeconds(ticks / (double)Stopwatch.Frequency);
        }

        public static DateTime GetTime()
        {
            return GetTime(Stopwatch.GetTimestamp());
        }
    }
}
