using System.Collections.Generic;
using System.Linq;

namespace azure
{
    public class CommitDetail
    {
        public string Id { get; set; }
        public string Author { get; set; }
        public string Url { get; set; }
        public string RepoName { get; set; }
        public string ProjectName { get; set; }
        public IEnumerable<WorkItem> WorkItems { get; set; }
        public CommitDetail(string id, string author, string url, string repoName, string projectName, IEnumerable<WorkItem> workItems = null)
        {
            Id = id;
            Author = author;
            Url = url;
            RepoName = repoName;
            ProjectName = projectName;
            WorkItems = workItems;
        }
        
        public static string AsFormattedString(IEnumerable<CommitDetail> commitDetails)
        {
            return string.Join("\n", commitDetails.Select(c =>
                $"CommitId: {c.Id} \n" +
                $"Url: {c.Url} \n" +
                $"Related WorkItem: \n {WorkItem.AsFormattedString(c.WorkItems)}"));
        }
    }
}