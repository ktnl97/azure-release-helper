using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace azure
{
    public static class ReportGenerator
    {
        public static void GenerateUnrelatedCommitsReport(IEnumerable<CommitDetail> unrelatedCommits)
        {
            const string filePath = "../../../unrelated-changes.txt";
            using var fileStream = File.Open(filePath, FileMode.OpenOrCreate);
            using var writeText = new StreamWriter(fileStream);
            var content = string.Empty;
            foreach (var commitsByRepo in unrelatedCommits.GroupBy(c => c.RepoName))
            {
                content += $"\n >>>> Commits of Repo: {commitsByRepo.Key} \n \n";
                foreach (var commitsByAuthor in commitsByRepo.GroupBy(c => c.Author))
                {
                    content += $"\n ** Authored By: {commitsByAuthor.Key} **\n \n";
                    content += CommitDetail.AsFormattedString(commitsByAuthor);
                }
            }

            writeText.WriteLine("List of commits without the given release tags or work Item associated \n \n" +
                                content);
            Console.WriteLine(
                "Unrelated commits report generated successfully.");
        }
    }
}