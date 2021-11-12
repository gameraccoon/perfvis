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

    class ScopeRecord
    {
        public int stackDepth = 0;
        public long timeStart = 0;
        public long timeFinish = 0;
        public string scopeName = "";
    }

    class ScopeThreadRecords
    {
        public int threadId = 0;
        public List<ScopeRecord> records = new List<ScopeRecord>();
    }

    class PerformanceData
    {
        public List<FrameData> frames = new List<FrameData>();
        public List<string> taskNames = new List<string>();
        public List<TaskData> nonFrameTasks = new List<TaskData>();
        public List<ScopeThreadRecords> scopeRecords = new List<ScopeThreadRecords>();
    }
}
