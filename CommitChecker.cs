using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace azure
{
    public class CommitChecker
    {
        private readonly ProjectHttpClient _projectHttpClient;
        private readonly GitHttpClient _gitHttpClient;
        private readonly WorkItemTrackingHttpClient _workItemClient;
        private readonly VssConnection _connection;
        private readonly List<CommitDetail> _unrelatedCommits;
        private const string WorkItemTagsIdentifier = "System.Tags";

        public CommitChecker(VssConnection connection)
        {
            _connection = connection;
            _unrelatedCommits = new List<CommitDetail>();
            _projectHttpClient = connection.GetClient<ProjectHttpClient>();
            _gitHttpClient = connection.GetClient<GitHttpClient>();
            _workItemClient = connection.GetClient<WorkItemTrackingHttpClient>();
        }

        public async Task ComputeChangeSet()
        {
            foreach (var project in await GetProjects())
            {
                foreach (var repo in await GetRelatedRepos(project.Id.ToString(), project.Name))
                {
                    var diffCommits = await GetDiffCommitsByComparingAgainstBaseBranch(repo.BaseBranch,  repo.Id);
                    await ProcessDiffCommits(diffCommits, project.Name, repo.Name);
                }
            }
            ReportGenerator.GenerateUnrelatedCommitsReport(_unrelatedCommits);
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

        private async Task ProcessDiffCommits(List<GitCommitRef> diffCommits, string projectName, string repoName)
        {
            foreach (var diffCommit in diffCommits)
            {
                var workItemIds = diffCommit.WorkItems?.Select(x => int.Parse(x.Id)).ToList();
                if (workItemIds == null || !workItemIds.Any())
                {
                    _unrelatedCommits.Add(new CommitDetail(diffCommit.CommitId, diffCommit.Author?.Name,
                        GetCommitLink(diffCommit.Url),
                        repoName, projectName
                    ));
                }
                else
                {
                    await CheckRelatedWorkItemTags(diffCommit,  workItemIds, projectName, repoName);
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

        private async Task CheckRelatedWorkItemTags(GitCommitRef commit, IEnumerable<int> workItemIds, string projectName, string repoName)
        {
            var workItems = await _workItemClient.GetWorkItemsAsync(workItemIds, new[] {WorkItemTagsIdentifier});
            var workItemsTags = workItems.SelectMany(GetWorkItemTags);
            if (!workItemsTags.Any(Config.ReleaseTags.Contains))
            {
                _unrelatedCommits.Add(new CommitDetail(commit.CommitId, commit.Author?.Name, GetCommitLink(commit.Url),
                    repoName, projectName,
                    workItems.Select(w => new WorkItem
                    {
                        Id = w.Id,
                        Url = GetWorkItemLink(w),
                    })
                ));
            }
        }

        private static string GetWorkItemLink(WorkItemTrackingResourceReference w)
        {
            return w.Url.Replace("_apis/wit/workItems/", "_workitems/edit/");
        }

        private static string[] GetWorkItemTags(Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models.WorkItem w)
        {
            return w.Fields.ContainsKey(WorkItemTagsIdentifier)
                ? w.Fields[WorkItemTagsIdentifier].ToString()?.Split("; ")
                : Array.Empty<string>();
        }

        private async Task<List<GitCommitRef>> GetDiffCommitsByComparingAgainstBaseBranch(string baseBranch, string repoId)
        {
            var baseBranchCommits = await GetBranchCommits(baseBranch, repoId);
            var targetBranchCommits = await GetBranchCommits(Config.TargetBranch, repoId);
            return targetBranchCommits.Where(commit =>
                !baseBranchCommits.Any(baseCommit =>
                    baseCommit.CommitId == commit.CommitId || baseCommit.Comment == commit.Comment)).ToList();
        }

        private async Task<List<GitCommitRef>> GetBranchCommits(string branchName, string repoId)
        {
            return 
                await _gitHttpClient.GetCommitsAsync(repoId, new GitQueryCommitsCriteria
                {
                    ItemVersion = new GitVersionDescriptor
                    {
                        Version = branchName,
                        VersionType = GitVersionType.Branch
                    },
                    IncludeWorkItems = true
                }, top: Config.MaxDiffNumber);
        }
    }
}