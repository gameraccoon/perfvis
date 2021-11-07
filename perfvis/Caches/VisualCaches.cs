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

            double tempAverageFrameDuration = 0.0f;
            int systemsProcessed = 0;

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

                    // avoid overflows
                    tempAverageFrameDuration += ((taskData.timeFinish - taskData.timeStart) - tempAverageFrameDuration) * (1 / (systemsProcessed + 1.0f));
                    systemsProcessed++;
                }
            }

            totalDuration = maxTime - minTime;
            averageFrameDuration = Convert.ToInt64(tempAverageFrameDuration);
        }

        public List<int> threads = new List<int>();
        public long minTime = 0;
        public long maxTime = 0;
        public long totalDuration = 0;
        public long averageFrameDuration = 0;
    }
}
