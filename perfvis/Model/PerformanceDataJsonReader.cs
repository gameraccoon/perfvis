using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace perfvis.Model
{
    class PerformanceDataJsonReader
    {
        public static PerformanceData ReadFromJson(string path)
        {
            using (StreamReader r = new StreamReader(path))
            {
                string json = r.ReadToEnd();
                PerformanceData data = JsonConvert.DeserializeObject<PerformanceData>(json);
                fixRawData(data);
                return data;
            }
        }

        private static void fixRawData(PerformanceData data)
        {
            // fix empty scope records
            foreach (ScopeThreadRecords scopeThreadRecords in data.scopeRecords)
            {
                if (scopeThreadRecords.records == null)
                {
                    scopeThreadRecords.records = new List<ScopeRecord>();
                }
            }
        }
    }
}
