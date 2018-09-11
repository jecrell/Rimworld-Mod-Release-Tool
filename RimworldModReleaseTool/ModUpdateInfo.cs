using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Octokit;

namespace RimworldModReleaseTool
{
    public class ModUpdateInfo
    {
        private static readonly string RimWorldVer = "B19";
        private static readonly DateTime FirstPublishDate = new DateTime(2016, 12, 11);

        private readonly string path;
        private readonly string team;
        private readonly string name;
        private readonly string title;
        private readonly DateTime publishDate = DateTime.Now;
        private readonly string publishDateString = null;
        private readonly string version;
        private readonly string description;
        private readonly string url;
        private readonly string imageURL;
        private readonly string patreonURL;
        private readonly string steamURL;
        private readonly string discordURL;
        private readonly string ludeonURL;
        private readonly string webhookToken;
        private readonly string gitRepoName;
        private readonly string gitRepoAuthor;
        private GitHubClient client;
        private string gitHubAuthor;
        private string gitHubEmail;

        public string GitHubEmail => gitHubEmail;
        public string GitHubAuthor => gitHubAuthor;
        public GitHubClient Client => client;
        public string GitRepoName => gitRepoName;
        public string GitRepoAuthor => gitRepoAuthor;
        public string Path => path;
        public string Name => name;
        public string Title => Name + " - " + title;
        public string Team => team;
        public DateTime PublishDate => publishDate;
        public string PublishDateString => publishDateString;
        public string Version => version;
        public string Description => description;
        public string URL => url;
        public string ImageUrl => imageURL;
        public string PatreonURL => patreonURL;
        public string SteamURL => steamURL;
        public string DiscordURL => discordURL;
        public string DiscordWebhookToken => webhookToken;
        public string LudeonURL => ludeonURL;

        public ModUpdateInfo(ReleaseSettings settings, string workspacePath)
        {
            path = workspacePath;

            ///// Get the update title
            Console.Write("\nPlease enter a title for your update/release or Press ENTER to continue : ");
            title = "";
            title = Console.ReadLine();
            ///// Get the update description
            Console.Write("\nPlease enter a description for your update/release or Press ENTER to continue : ");
            description = "";
            description = Console.ReadLine();
            Console.WriteLine();

            ///// Get the Steam URL
            if (settings.HandleSteam)
            {
                var steamPublishIDPath = path + @"\About\PublishedFileId.txt";
                if (File.Exists(steamPublishIDPath))
                {
                    string steamPublishID = File.ReadLines(steamPublishIDPath).First();
                    steamURL = @"https://steamcommunity.com/sharedfiles/filedetails/?id=" + steamPublishID;
                }
            }

            ///// Get the Patreon URL
            if (settings.HandlePatreon)
            {
                var patreonPath = path + @"\About\PatreonURL.txt";
                if (!File.Exists(patreonPath))
                {
                    if (Program.UserAccepts("Patreon URL file not detected. Create new one? (Y/N): "))
                    {
                        Console.Write("\nPlease enter the Patreon URL or Press ENTER to continue : ");
                        patreonURL = "";
                        patreonURL = Console.ReadLine();
                        Console.WriteLine();
                        System.IO.File.WriteAllText(patreonPath, patreonURL + "\n");
                    }
                }
                else
                    patreonURL = File.ReadLines(patreonPath).First();
            }


            ///// Get the Discord URL
            if (settings.HandleDiscord)
            {
                var discordPath = path + @"\About\DiscordURL.txt";
                if (!File.Exists(discordPath))
                {
                    if (Program.UserAccepts("Discord URL file not detected. Create new one? (Y/N): "))
                    {
                        Console.Write("\nPlease enter the Discord invite URL or Press ENTER to continue : ");
                        discordURL = "";
                        discordURL = Console.ReadLine();
                        Console.WriteLine();
                        System.IO.File.WriteAllText(discordPath, discordURL + "\n");
                    }
                }
                else
                    discordURL = File.ReadLines(discordPath).First();
            }


            ///// Get the Ludeon URL
            if (settings.HandleLudeon)
            {
                var ludeonPath = path + @"\About\LudeonURL.txt";
                if (!File.Exists(ludeonPath))
                {
                    if (Program.UserAccepts("Ludeon URL file not detected. Create new one? (Y/N): "))
                    {
                        Console.Write("\nPlease enter the Ludeon thread URL or Press ENTER to continue : ");
                        ludeonURL = "";
                        ludeonURL = Console.ReadLine();
                        Console.WriteLine();
                        System.IO.File.WriteAllText(ludeonPath, ludeonURL + "\n");
                    }
                }
                else
                    ludeonURL = File.ReadLines(ludeonPath).First();
            }


            ///// Get the Discord Webhook
            if (settings.HandleDiscordWebhook)
            {
                var webhookPath = path + @"\Source\DiscordWebhookToken.txt";
                if (!File.Exists(webhookPath))
                {
                    if (Program.UserAccepts("Discord Webhook Token not detected. Create new one? (Y/N): "))
                    {
                        Console.Write("\nPlease enter the Discord Webhook link or Press ENTER to continue : ");
                        webhookToken = "";
                        webhookToken = Console.ReadLine();
                        Console.WriteLine();
                        System.IO.File.WriteAllText(webhookPath, webhookToken + "\n");
                    }
                }
                else
                {
                    webhookToken = File.ReadLines(webhookPath).First().Trim();
                    //Console.WriteLine(webhookToken);
                }
            }

            ///// Get the name
            string modName = ParseAboutXMLFor("name", workspacePath);
            string modAuthor = ParseAboutXMLFor("author", workspacePath);
            //Console.WriteLine(modName);
            //Console.WriteLine(modAuthor);

            name = modName; //path.Substring(path.LastIndexOf("\\", StringComparison.Ordinal) + 1);
            team = modAuthor;

            ///// Get the date
            publishDate = DateTime.Now;
            publishDateString = $"{publishDate:MM-dd-yyyy}";

            ///// Autoset a version number
            var daysSinceStarted = (DateTime.Now - FirstPublishDate).Days;
            version = RimWorldVer + '.' + daysSinceStarted;

            if (settings.HandleGitHub)
            {
                var gitConfigPath = workspacePath + @"\.git\config";
                if (!File.Exists(gitConfigPath))
                {
                    Console.WriteLine("Warning - No .git folder detected.");
                }
                else
                {
                    string[] lines = File.ReadAllLines(gitConfigPath);
                    string urlLine = lines.FirstOrDefault(x => x.Contains("url ="));
                    urlLine = urlLine.ClearWhiteSpace();
                    //https://github.com/jecrell/Call-of-Cthulhu---Cosmic-Horrors.git
                    urlLine = urlLine.Replace("url=", "").Replace("https://github.com/", "").Replace(".git", "");
                    lines = urlLine.Split('/');
                    gitRepoAuthor = lines[0];
                    gitRepoName = lines[1];
                }

                Console.WriteLine(".git Config Detected.");
                Console.Write("Repository: " + gitRepoName + " Author: " + gitRepoAuthor);

                client = new GitHubClient(new Octokit.ProductHeaderValue("RimworldModReleaseTool"));
                var auth = gitRepoAuthor;
                if (Program.UserAccepts("\nLogin to GitHub? (Y/N): "))
                {    
                    Console.WriteLine("\nConnecting to GitHub requires a login.\nPlease enter your credentials to proceed.");
                    Console.WriteLine("Username: ");
                    auth = Console.ReadLine();
                    Console.WriteLine("Password: ");
                    var key = Console.ReadLine();
                    client.Credentials = new Credentials(auth, key);                    
                    //Get the user
                    gitHubAuthor = client.User.Get(auth).Result.Name;
                    gitHubEmail = client.User.Get(auth).Result.Email;
                }
                ///// Get the repo's preview image
                var repo = GitHubUtility.GetGithubRepository(this, settings, gitRepoName, gitRepoAuthor);

                url = repo.HtmlUrl;
                imageURL = url + "/master/About/Preview.png";
                imageURL =
                    imageURL.Replace("https://github.com/", "https://raw.githubusercontent.com/");
            }
        }

        private static string ParseAboutXMLFor(string element, string newPath)
        {
            var text = newPath + @"\About\About.xml";
            var xml = new XmlDocument();
            xml.Load(text);
            return XElement.Parse(xml.InnerXml).Element(element)?.Value ?? "NULL";
        }
    }
}