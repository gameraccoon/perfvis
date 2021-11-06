using perfvis.Model;
using System;
using System.Collections.Generic;

namespace perfvis.Caches
{
    class PerformanceVisualizationCaches
    {
        public void updateFromData(PerformanceData data)
        {
            threads.Clear();

            minTime = long.MaxValue;
            maxTime = long.MinValue;

            foreach (FrameData frame in data.frames)
            {
                foreach (TaskData taskData in frame.tasks)
                {
                    if (!threads.Contains(taskData.threadId))
                    {
                        threads.Add(taskData.threadId);
                    }

                    minTime = Math.Min(minTime, taskData.timeStart);
                    maxTime = Math.Max(maxTime, taskData.timeFinish);
                }
            }

            totalDuration = maxTime - minTime;
        }

        public List<int> threads = new List<int>();
        public long minTime = 0;
        public long maxTime = 0;
        public long totalDuration = 0;
    }
}
