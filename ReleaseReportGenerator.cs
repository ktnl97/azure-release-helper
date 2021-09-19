using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Newtonsoft.Json;

namespace azure
{
    public class ReleaseReport
    {
        private readonly ProjectHttpClient _projectHttpClient;
        private readonly GitHttpClient _gitHttpClient;
        private readonly VssConnection _connection;
        private string _reportContent = "";
        private static string _workItemTagsIdentifier;

        public ReleaseReport(VssConnection connection)
        {
            _connection = connection;
            _projectHttpClient = connection.GetClient<ProjectHttpClient>();
            _gitHttpClient = connection.GetClient<GitHttpClient>();
        }

        public async Task ComputeChangeSet()
        {
            foreach (var project in await GetProjects())
            {
                _reportContent += $"\n \n Analysing project: {project.Name} \n \n";
                foreach (var repo in await GetRelatedRepos(project.Id.ToString(), project.Name))
                {
                    _reportContent += $"\n *Analysing commit diff for repo: {repo.Name}* \n";
                    var diffCommits = await GetDiffCommitsByComparingBranches(repo.BaseBranch,  repo.Id);
                    await ProcessDiffCommits(diffCommits);
                }
            }
            WriteContentToReport();
        }

        private void WriteContentToReport()
        {
            if (!File.Exists(Config.ReportPath))
            {
                File.CreateText(Config.ReportPath);
            }

            using var writeText = new StreamWriter(Config.ReportPath);
            writeText.WriteLine(_reportContent);
        }

        private async Task<IEnumerable<RepositoryDetails>> GetRelatedRepos(string projectId, string projectName)
        {
            var availableRepos = await _gitHttpClient.GetRepositoriesAsync(projectId);
            var requiredRepos = new List<RepositoryDetails>();
            Config.RepoConfigs[projectName].ForEach(repo =>
            {
                var matchingRepo = availableRepos.SingleOrDefault(ar => ar.Name == repo.Name);
                if (matchingRepo != null)
                {
                    repo.Id = matchingRepo.Id.ToString();
                    requiredRepos.Add(repo);
                }
            });
            return requiredRepos;
        }

        private async Task<IEnumerable<TeamProjectReference>> GetProjects()
        {
            return (await _projectHttpClient.GetProjects())
                .Where(pro => Config.RepoConfigs.Keys.Contains(pro.Name));
        }

        private async Task ProcessDiffCommits(List<GitCommitRef> diffCommits)
        {
            foreach (var diffCommit in diffCommits)
            {
                var workItemIds = diffCommit.WorkItems?.Select(x => int.Parse(x.Id)).ToList();
                if (workItemIds == null || !workItemIds.Any())
                {
                    _reportContent += 
                        "\n No work items associated with commit \n" +
                        $"{diffCommit.CommitId} => " +
                        $"{GetCommitLink(diffCommit.Url)} \n";
                }
                else
                {
                    await CheckRelatedWorkItemTags(_connection, Config.ReleaseTag, workItemIds, diffCommit.CommitId);
                }
            }
        }

        private static string GetCommitLink(string commitApiUrl)
        {
            return commitApiUrl
                .Replace("/_apis", "")
                .Replace("/repositories", "")
                .Replace("/git/", "/_git/")
                .Replace("/commits/", "/commit/");
        }

        private async Task CheckRelatedWorkItemTags(VssConnection connection, string releaseTag, IEnumerable<int> workItemIds, string commitId)
        {
            WorkItemTrackingHttpClient witClient = connection.GetClient<WorkItemTrackingHttpClient>();
            connection.GetClient<GitCompatHttpClientBase>();
            _workItemTagsIdentifier = "System.Tags";
            var workItems = await witClient.GetWorkItemsAsync(workItemIds, new[] {_workItemTagsIdentifier});
            var workItemsTags = workItems.SelectMany(GetWorkItemTags);
            if (!workItemsTags.Contains(releaseTag))
            {
                _reportContent += 
                    $"\n Commit {commitId} related to work item(s) \n" +
                    $"{JsonConvert.SerializeObject(workItems.Select(w => $"{w.Id} => {GetWorkItemLink(w)}"))} \n" +
                    $"found without {releaseTag} tag \n";
            }
        }

        private static string GetWorkItemLink(WorkItem w)
        {
            return w.Url.Replace("_apis/wit/workItems/", "_workitems/edit/");
        }

        private static string[] GetWorkItemTags(WorkItem w)
        {
            if (w.Fields.ContainsKey(_workItemTagsIdentifier))
            {
                return w.Fields[_workItemTagsIdentifier].ToString()?.Split("; ");
            }

            return Array.Empty<string>();
        }

        private async Task<List<GitCommitRef>> GetDiffCommits(VssConnection connection, string baseBranch, string targetBranch, string repoId)
        {
            GitHttpClient gitClient = connection.GetClient<GitHttpClient>();
            var diffCommits = await gitClient.GetCommitDiffsAsync(new Guid(repoId),
                baseVersionDescriptor: new GitBaseVersionDescriptor
                {
                    VersionType = GitVersionType.Branch,
                    Version = baseBranch,  
                },
                targetVersionDescriptor: new GitTargetVersionDescriptor
                {
                    Version = targetBranch,
                    VersionType = GitVersionType.Branch,
                });
            var diffCommitIds = diffCommits.Changes.Select(d => d.Item.CommitId).ToList();

            var result = new List<GitCommitRef>();

            diffCommitIds.Batch(40).ForEach(async (batchedDiffCommits) =>
            {
                GitQueryCommitsCriteria commitsCriteria = new GitQueryCommitsCriteria()
                {
                    Ids = batchedDiffCommits.ToList(),
                    IncludeWorkItems = true
                };
            
                result.AddRange(await gitClient.GetCommitsAsync(repoId, commitsCriteria)); 
            });
            return result;
        }
        
        private async Task<List<GitCommitRef>> GetDiffCommitsByComparingBranches(string baseBranch, string repoId)
        {
            GitHttpClient gitClient = _connection.GetClient<GitHttpClient>();
            GitQueryCommitsCriteria baseBranchSearchCriteria = new GitQueryCommitsCriteria()
            {
                ItemVersion = new GitVersionDescriptor
                {
                    Version = baseBranch,
                    VersionType = GitVersionType.Branch
                },
                IncludeWorkItems = true
            };
            var baseBranchCommits = await gitClient.GetCommitsAsync(repoId, baseBranchSearchCriteria, top: Config.MaxDiffNumber);
            GitQueryCommitsCriteria targetBranchSearchCriteria = new GitQueryCommitsCriteria()
            {
                ItemVersion = new GitVersionDescriptor
                {
                    Version = Config.TargetBranch,
                    VersionType = GitVersionType.Branch
                },
                IncludeWorkItems = true
            };
            var targetBranchCommits = await gitClient.GetCommitsAsync(repoId, targetBranchSearchCriteria, top: Config.MaxDiffNumber);

            return targetBranchCommits.Where(commit =>
                !baseBranchCommits.Select(x => x.CommitId).ToList().Contains(commit.CommitId)).ToList();
        }
    }
}