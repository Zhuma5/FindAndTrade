using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MGAutoSell
{
    internal class PerformanceTracker : Stopwatch
    {
        private List<(string name, long ms)> milestones = new();

        internal new static PerformanceTracker StartNew()
        {
            var tracker = new PerformanceTracker();
            tracker.Start();
            return tracker;
        }

        internal void Checkpoint(string name)
        {
            if (!IsRunning)
                throw new Exception("Performance tracker wasn't running");

            Stop();
            milestones.Add((name, ElapsedMilliseconds));
            Restart();
        }

        internal string Flush()
        {
            Stop();
            var values = string.Join("\n", milestones.Select(x => $"{x.name}: {x.ms}ms"));
            milestones.Clear();
            return values;
        }
    }
}
