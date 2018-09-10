using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Octokit;

namespace RimworldModReleaseTool
{
    public static class GitHubUtility
    {
        public static readonly string ClientToken = "RimworldModReleaseTool";
        
        public static Repository GetGithubRepository(ModUpdateInfo info, ReleaseSettings settings, string repoName, string repoAuthor ="")
        {
            var task = Task.Run(async () => await GitHubUtility.GetRepoFromGitHub(info, repoName, repoAuthor));
            task.Wait(TimeSpan.FromSeconds(30));
            return task.Result;
        }
        
        public static void RunGitProcessWithArgs(string workingDirectory, string gitAddArgument, bool writeConsole = true)
        {
            if (writeConsole) Console.WriteLine("git " + gitAddArgument);
            var gitAdd = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    WorkingDirectory = workingDirectory,
                    FileName = "git",
                    Arguments = gitAddArgument,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            gitAdd.Start();
            while (!gitAdd.StandardOutput.EndOfStream)
            {
                if (writeConsole) Console.WriteLine(gitAdd.StandardOutput.ReadLine());
            }
            
        }
        
        public static async Task<Octokit.Repository> GetRepoFromGitHub(ModUpdateInfo info, string repoName, string repoAuthor = "")
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
            if (searchResult?.Items?.Count > 0)
            {
                repository = searchResult.Items.First();
            }
            
            return repository;

        }        
        public static async Task<Octokit.Release> CreateRelease(ModUpdateInfo info, Repository repo, string version, string name, string body)
        {

            //Get list of commits
            var latestCommits = Task.Run(async () => await info.Client.Repository.Commit.GetAll(repo.Owner.Login, repo.Name));
            latestCommits.Wait();
            var latestCommit = latestCommits.Result.First().Sha;
            
            //Set a tag for our release
            var tag = new NewTag {
                Message = name + " - " + body,
                Tag = version,
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