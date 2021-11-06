using Newtonsoft.Json;
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
                return JsonConvert.DeserializeObject<PerformanceData>(json);
            }
        }
    }
}
