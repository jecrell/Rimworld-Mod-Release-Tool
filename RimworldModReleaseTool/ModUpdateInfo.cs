using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Octokit;

namespace RimworldModReleaseTool
{
    internal class ModUpdateInfo
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

        public ModUpdateInfo(ReleaseSettings settings, string newPath)
        {
            path = newPath;
            
            ///// Get the update title
            Console.Write("\nPlease enter a title or Press ENTER to continue : ");
            title = "";
            title = Console.ReadLine();
            Console.WriteLine();
            
            ///// Get the update description
            Console.Write("\nPlease enter a description or Press ENTER to continue : ");
            description = "";
            description = Console.ReadLine();
            Console.WriteLine();
            
            ///// Get the Steam URL
            var steamPublishIDPath = path + @"\About\PublishedFileId.txt";
            if (!File.Exists(steamPublishIDPath))
            {
                Console.WriteLine("\nSteam Publish ID not detected.");
                if (Program.UserAccepts("Publish mod on Steam now?"))
                {
                    SteamUtility.PublishMod();
                }                
            }
            else
            {
                string steamPublishID = File.ReadLines(steamPublishIDPath).First();
                steamURL = @"https://steamcommunity.com/sharedfiles/filedetails/?id=" + steamPublishID;
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
            string modName = ParseAboutXMLFor("name", newPath);
            string modAuthor = ParseAboutXMLFor("author", newPath);
            
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
                ///// Get the repo's preview image
                var repo = GitHubUtility.GetGithubRepository(settings, modName);
                
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