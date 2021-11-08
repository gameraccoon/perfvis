using System.Collections.Generic;

namespace perfvis.Model
{
    class TaskData
    {
        public TaskData() { }
        public TaskData(int threadId, long timeStart, long timeFinish, int nameIdx)
        {
            this.threadId = threadId;
            this.timeStart = timeStart;
            this.timeFinish = timeFinish;
            this.taskNameIdx = nameIdx;
        }

        public int threadId;
        public long timeStart;
        public long timeFinish;
        public int taskNameIdx;
    }

    class FrameData
    {
        public List<TaskData> tasks = new List<TaskData>();
    }

    class PerformanceData
    {
        public List<FrameData> frames = new List<FrameData>();
        public List<string> taskNames = new List<string>();
        public List<TaskData> nonFrameTasks = new List<TaskData>();
    }
}
