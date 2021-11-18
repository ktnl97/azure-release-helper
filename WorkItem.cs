using System.Collections.Generic;
using System.Linq;

namespace azure
{
    public class WorkItem
    {
        public int? Id { get; set; }
        public string Url { get; set; }
        
        public static string AsFormattedString(IEnumerable<WorkItem> workItems)
        {
            return workItems != null
                ? string.Join("\n", workItems.Select(w => $"\tUrl: {w.Url} \n"))
                : "";
        }
    }
}