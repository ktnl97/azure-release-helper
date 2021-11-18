using System.Collections.Generic;

namespace azure
{
    public static class Config
    {
        public static IDictionary<string, IEnumerable<RepositoryDetails>> RepoConfigs =  new Dictionary<string, IEnumerable<RepositoryDetails>>
        {
            {
                "Project_Name_1", new List<RepositoryDetails>
                {
                    new()
                    {
                        Name = "Repository_Name_1",
                        BaseBranch = "Previous_release_to_compare"
                    },
                }
            },{
                "Project_Name_2", new List<RepositoryDetails>
                {
                    new()
                    {
                        Name = "Repository_Name_2",
                        BaseBranch = "Previous_release_to_compare"
                    },
                }
            }
        };

        public static readonly List<string> ReleaseTags = new()
        {
            "Release tags"
        };
        public const string TargetBranch = "Master_branch";
        public const string AzureDevopsUrl = "Azure_devops_url";
        public const string Pat = "Your_PAT";
        public const string CurrentReleaseBranch = "";
        public const int MaxDiffNumber = 200;
    }
}