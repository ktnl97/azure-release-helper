using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace azure
{
    internal static class Program
    {
        private static async Task Main()
        {
            var connection = GetAzureConnection();
            Console.WriteLine("Established azure connection successfully. Starting to fetch and analyse commits...");
            var releaseReportGenerator = new CommitChecker(connection);
            await releaseReportGenerator.ComputeChangeSet();

            PrintReleaseBranchComparisonLinks();
        }

        private static void PrintReleaseBranchComparisonLinks()
        {
            if(string.IsNullOrWhiteSpace(Config.CurrentReleaseBranch))
                return;
            
            foreach (var (projectName, repoDetails) in Config.RepoConfigs)
            {
                foreach (var repoDetail in repoDetails)
                {
                    Console.WriteLine(
                        $"{repoDetail.Name} \n " +
                        $"https://occm.visualstudio.com/{projectName}/_git/{repoDetail.Name}/branchCompare?baseVersion=GB{repoDetail.BaseBranch}&targetVersion=GB{Config.CurrentReleaseBranch}&_a=commits" +
                        "\n");
                }
            }
        }

        private static VssConnection GetAzureConnection()
        {
            var orgUrl = new Uri(Config.AzureDevopsUrl);
            return new VssConnection(orgUrl, new VssBasicCredential(string.Empty, Config.Pat));
        }
    }
}