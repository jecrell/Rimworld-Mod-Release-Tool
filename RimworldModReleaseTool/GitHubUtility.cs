using System;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;
using Microsoft.Win32;
using Octokit;

namespace RimworldModReleaseTool
{
    public static class GitHubUtility
    {
        public static readonly string ClientToken = "RimworldModReleaseTool";

        public static Repository GetGithubRepository(ModUpdateInfo info, ReleaseSettings settings, string repoName,
            string repoAuthor = "")
        {
            var task = Task.Run(async () => await GetRepoFromGitHub(info, repoName, repoAuthor));
            task.Wait(TimeSpan.FromSeconds(30));
            return task.Result;
        }


        public static string GetGitCMDPath()
        {
            var rk = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            var sk1 = rk.OpenSubKey(@"SOFTWARE\GitForWindows");
            if (sk1 == null)
                return null;
            try
            {
                return (string) sk1.GetValue("InstallPath") + @"\cmd\git.exe";
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to find GitForWindows' directory");
                return null;
            }
        }


        public static void RunGitProcessWithArgs(string workingDirectory, string info)
        {
            
            using (var powershell = PowerShell.Create())
            {
                // this changes from the user folder that PowerShell starts up with to your git repository
                powershell.AddScript(string.Format("cd \"{0}\"", workingDirectory));
                powershell.AddScript(@"git init");
                powershell.AddScript(@"git add *");
                powershell.AddScript(@"git commit -m 'git commit from PowerShell in C#" + info + "'");
                powershell.AddScript(@"git push origin master");
                var results = powershell.Invoke();
                foreach (var invoke in results)
                    Console.WriteLine(invoke.ToString());                    
            }
        }

        public static async Task<Repository> GetRepoFromGitHub(ModUpdateInfo info, string repoName,
            string repoAuthor = "")
        {
            if (repoName != "" && repoAuthor != "")
                return info.Client.Repository.Get(repoAuthor, repoName).Result;

            var request = new SearchRepositoriesRequest(repoName)
            {
                Language = Language.CSharp,
                Archived = false
            };

            var searchResult = await info.Client.Search.SearchRepo(request);

            Repository repository = null;
            if (searchResult?.Items?.Count > 0) repository = searchResult.Items.First();

            return repository;
        }

        public static async Task<Release> CreateRelease(ModUpdateInfo info, Repository repo, string version,
            string name, string body)
        {
            //Get list of commits
            var latestCommits = Task.Run(async () =>
                await info.Client.Repository.Commit.GetAll(repo.Owner.Login, repo.Name));
            latestCommits.Wait();
            var latestCommit = latestCommits.Result.First().Sha;

            //Check if release exists for the version #
            var curVersion = version;
            for (int newVersionAdditive = 0; newVersionAdditive < 999; newVersionAdditive++)
            {    
                Console.WriteLine("Checking if release tag for version number already exists...");
                var versCheck = Task.Run(async () => await info.Client.Repository.Release.GetAll(repo.Id));
                versCheck.Wait();
                if (versCheck.Result.Any(x => x.TagName == curVersion))
                {
                    curVersion = curVersion + newVersionAdditive;
                    Console.WriteLine("Release tag exists. Setting new version to " + curVersion);
                }
                else
                {
                    Console.WriteLine("Final release tag set as " + curVersion);
                    break;
                }
            }
            //Set a tag for our release
            var tag = new NewTag
            {
                Message = name + " - " + body,
                Tag = curVersion,
                Object = latestCommit, // short SHA
                Type = TaggedType.Commit, // TODO: what are the defaults when nothing specified?
                Tagger = new Committer(info.GitHubAuthor, info.GitHubEmail, DateTimeOffset.UtcNow)
            };
            var newTagProc = Task.Run(async () => await info.Client.Git.Tag.Create(repo.Owner.Login, repo.Name, tag));
            newTagProc.Wait();

            var newTag = newTagProc.Result;
            Console.WriteLine("Created a tag for {0} at {1}", newTag.Tag, newTag.Sha);

            var newRelease = new NewRelease(newTag.Tag)
            {
                Name = repo.Name + " " + name,
                Body = body,
                Draft = false,
                Prerelease = false
            };


            var result = await info.Client.Repository.Release.Create(repo.Owner.Login, repo.Name, newRelease);
            return result;
        }
    }
}