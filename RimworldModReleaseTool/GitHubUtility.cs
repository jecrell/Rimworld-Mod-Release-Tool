using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Octokit;

namespace RimworldModReleaseTool
{
    public static class GitHubUtility
    {
        private static readonly string ClientToken = "jecrell.rimworldmodreleasetool";

        public static void RunGitProcessWithArgs(string gitAddArgument, bool writeConsole = true)
        {
            if (writeConsole) Console.WriteLine("git " + gitAddArgument);
            var gitAdd = new Process
            {
                StartInfo = new ProcessStartInfo
                {
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
        
        public static async Task<Octokit.Repository> GetRepoFromGitHub(string newRepoName)
        {
            //var owner = "jecrell";
            var reponame = newRepoName;
            var client = new GitHubClient(new Octokit.ProductHeaderValue(ClientToken));
            //var repository = await client.Repository.Get( owner, reponame);

            var request = new SearchRepositoriesRequest(reponame)
            {
            };

            var searchResult = await client.Search.SearchRepo(request);

            var repository = searchResult.Items.First();
            
            return repository;
        }        
        public static async Task<Octokit.Release> CreateRelease(Repository repo, string version, string name, string body)
        {
            var client = new GitHubClient(new Octokit.ProductHeaderValue(ClientToken));

            //Get list of commits
            var latestCommits = Task.Run(async () => await client.Repository.Commit.GetAll(repo.Owner.Login, repo.Name));
            latestCommits.Wait();
            var latestCommit = latestCommits.Result.First().Sha;
            
            //Set a tag for our release
            var tag = new NewTag {
                Message = name + " - " + body,
                Tag = version,
                Object = latestCommit, // short SHA
                Type = TaggedType.Commit, // TODO: what are the defaults when nothing specified?
                Tagger = new Committer("jecrell from Release Tool", "matt.walls31@gmail.com", DateTimeOffset.UtcNow)
            };
            Console.Write("Auth: ");
            var auth = Console.ReadLine();
            Console.Write("Key: ");
            var key = Console.ReadLine();
            var basicAuth = new Credentials(auth, key); // NOTE: not real credentials
            client.Credentials = basicAuth;
            
            var newTagProc = Task.Run(async () => await client.Git.Tag.Create(repo.Owner.Login, repo.Name, tag));
            newTagProc.Wait();

            var newTag = newTagProc.Result;
            Console.WriteLine("Created a tag for {0} at {1}", newTag.Tag, newTag.Sha);
            
            var newRelease = new NewRelease(newTag.Tag);
            newRelease.Name = repo.Name + " " + name;
            newRelease.Body = body;
            newRelease.Draft = false;
            newRelease.Prerelease = false;
            
            var result = await client.Repository.Release.Create(repo.Owner.Login, repo.Name, newRelease);
            return result;
        }

    }
}