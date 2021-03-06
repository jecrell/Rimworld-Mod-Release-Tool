﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Configuration;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Octokit;

namespace RimworldModReleaseTool
{
    public class ModUpdateInfo
    {
        private static readonly DateTime FirstPublishDate = new DateTime(2016, 12, 11);

        private readonly string title;

        public static string RemoveSpecialCharacters(string str)
        {
            return Regex.Replace(str, "[^a-zA-Z0-9_.]+", "", RegexOptions.Compiled);
        }
        
        public ModUpdateInfo(ReleaseSettings settings, string workspacePath, string targetPath)
        {
            Path = workspacePath;

            ///// Get the update title
            Console.Write("\nPlease enter a title for your update/release or Press ENTER to continue : ");
            title = RemoveSpecialCharacters(Console.ReadLine());
            ///// Get the update description
            Console.Write("\nPlease enter a description for your update/release or Press ENTER to continue : ");
            Description = "";
            Description = Console.ReadLine();
            Console.WriteLine();

            ///// Get the Steam URL
            if (settings.HandleSteam)
            {
                var steamPublishIDPath = Path + @"\About\PublishedFileId.txt";
                if (File.Exists(steamPublishIDPath))
                {
                    var steamPublishID = File.ReadLines(steamPublishIDPath).First();
                    SteamURL = @"https://steamcommunity.com/sharedfiles/filedetails/?id=" + steamPublishID;
                }
            }

            ///// Get the Patreon URL, Patron info, and Patrons message
            List<string> patrons = new List<string>();
            if (settings.HandlePatreon)
            {
                var patreonPath = Path + @"\About\PatreonURL.txt";
                if (!File.Exists(patreonPath))
                {
                    if (Program.UserAccepts("Patreon URL file not detected. Create new one? (Y/N): "))
                    {
                        Console.Write("\nPlease enter the Patreon URL or Press ENTER to continue : ");
                        PatreonURL = "";
                        PatreonURL = Console.ReadLine();
                        Console.WriteLine();
                        File.WriteAllText(patreonPath, PatreonURL + "\n");
                    }
                }
                else
                {
                    PatreonURL = File.ReadLines(patreonPath).First();
                }
                
                if (File.Exists(settings.PatronsFilePath))
                {
                    var lines = File.ReadLines(settings.PatronsFilePath).Skip(1);
                    var activeOnly = lines.ToList().FindAll(x=>x.Split(',')[3] == "Active patron");
                    var sorted = activeOnly.Select(line =>
                        {
                            var s = line.Split(',')[5];
                            //Console.WriteLine(s);
                            var replace = s.Replace("$", "").Replace("\"", "");
                            //Console.WriteLine(replace);
                            return new
                            {
                                SortKey = float.Parse(replace),
                                Line = line
                            };
                        })
                        .OrderBy(x => x.SortKey)
                        .Select(x => x.Line);
                    
                    foreach (var line in sorted)
                    {
                        var newLine = line.Split(',').FirstOrDefault();
                        patrons.Add(newLine);
                    }
                    Console.WriteLine("Patreons: ");
                    Console.WriteLine(String.Join(", ", patrons) + "\n");
                }
                else
                {
                    Console.WriteLine("Patrons file not found.");
                }
            }


            ///// Get the Discord URL
            if (settings.HandleDiscord)
            {
                var discordPath = Path + @"\About\DiscordURL.txt";
                if (!File.Exists(discordPath))
                {
                    if (Program.UserAccepts("Discord URL file not detected. Create new one? (Y/N): "))
                    {
                        Console.Write("\nPlease enter the Discord invite URL or Press ENTER to continue : ");
                        DiscordURL = "";
                        DiscordURL = Console.ReadLine();
                        Console.WriteLine();
                        File.WriteAllText(discordPath, DiscordURL + "\n");
                    }
                }
                else
                {
                    DiscordURL = File.ReadLines(discordPath).First();
                }
            }


            ///// Get the Ludeon URL
            if (settings.HandleLudeon)
            {
                var ludeonPath = Path + @"\About\LudeonURL.txt";
                if (!File.Exists(ludeonPath))
                {
                    if (Program.UserAccepts("Ludeon URL file not detected. Create new one? (Y/N): "))
                    {
                        Console.Write("\nPlease enter the Ludeon thread URL or Press ENTER to continue : ");
                        LudeonURL = "";
                        LudeonURL = Console.ReadLine();
                        Console.WriteLine();
                        File.WriteAllText(ludeonPath, LudeonURL + "\n");
                    }
                }
                else
                {
                    LudeonURL = File.ReadLines(ludeonPath).First();
                }
            }


            ///// Get the Discord Webhook
            if (settings.HandleDiscordWebhook)
            {
                var sourcePath = Path + @"\Source";
                var webhookPath = sourcePath + @"\DiscordWebhookToken.txt";
                if (!File.Exists(webhookPath))
                {
                    if (Program.UserAccepts("Discord Webhook Tokens not detected. Create new ones? (Y/N): "))
                    {
                        Console.Write("\nPlease enter the Discord Webhook link or Press ENTER to continue : ");
                        DiscordWebhookTokens = new List<string>();
                        DiscordWebhookTokens.Add(Console.ReadLine().Trim());
                        Console.WriteLine();
                        if (!Directory.Exists(sourcePath))
                            Directory.CreateDirectory(sourcePath);
                        for (int i = 0; i < 999; i++)
                        {
                            if (!Program.UserAccepts("Add additional webhooks? (Y/N): "))
                                break;
                            Console.Write("\nPlease enter the Discord Webhook link or Press ENTER to continue : ");
                            var input = Console.ReadLine();
                            if (input == "") break;
                            DiscordWebhookTokens.Add(input.Trim());
                        }

                        File.WriteAllLines(webhookPath, DiscordWebhookTokens);
                    }
                }
                else
                {
                    DiscordWebhookTokens = new List<string>(File.ReadAllLines(webhookPath));
                    Console.WriteLine("\nFound " + DiscordWebhookTokens.Count + " Webhook token(s).");
                    //Console.WriteLine(webhookToken);
                }
            }

            ///// Get the name
            var modName = ParseAboutXMLFor("name", workspacePath);
            var modAuthor = ParseAboutXMLFor("author", workspacePath);
            //Console.WriteLine(modName);
            //Console.WriteLine(modAuthor);

            Name = modName; //path.Substring(path.LastIndexOf("\\", StringComparison.Ordinal) + 1);
            Team = modAuthor;

            ///// Get the date
            PublishDate = DateTime.Now;
            PublishDateString = $"{PublishDate:MM-dd-yyyy}";

            ///// Ask or autoset a version number
            var versionPath = workspacePath + @"\About\Version.txt";
            var versionPathTwo = targetPath + @"\About\Version.txt";
            
            
            var manifestXMLPath = workspacePath + @"\About\Manifest.xml";
            var manifestXMLPathTarget = targetPath + @"\About\Manifest.xml";
                
            if (settings.AskForVersionNum)
            {
                if (!File.Exists(versionPath))
                {
                    if (Program.UserAccepts("Version number file not detected. Create new one? (Y/N): "))
                    {
                        Console.Write(
                            "\nPlease enter the current version number of the mod or Press ENTER to continue : ");
                        Version = "";
                        while (true)
                        {
                            var input = Console.ReadLine();
                            if (input == "")
                            {
                                var daysSinceStarted = (DateTime.Now - FirstPublishDate).Days;
                                Version = settings.TargetVersion + '.' + daysSinceStarted;
                                break;
                            }

                            Version = input;
                            Console.WriteLine("Set version to: " + input);
                            break;
                        }

                        File.WriteAllText(versionPath, Version + "\n");
                        File.WriteAllText(versionPathTwo, Version + "\n");
                    }
                }
                else
                {
                    Version = File.ReadLines(versionPath).First();
                    Console.WriteLine("Current version detected: " + Version);
                    Console.WriteLine("Enter new version or press ENTER to generate one: ");
                    Version = Console.ReadLine();
                    if (Version == "")
                    {
                        var daysSinceStarted = (DateTime.Now - FirstPublishDate).Days;
                        Version = settings.TargetVersion + '.' + daysSinceStarted;
                    }

                    Console.WriteLine("Set version to: " + Version);
                    File.WriteAllText(versionPath, Version + "\n");
                    File.WriteAllText(versionPathTwo, Version + "\n");
                }
            }
            else
            {
                var daysSinceStarted = (DateTime.Now - FirstPublishDate).Days;
                Version = settings.TargetVersion + '.' + daysSinceStarted;
            }

            //Adjust the version number in Manifest.xml (if it exists)
            if (File.Exists(manifestXMLPath))
            {
                XmlDocument doc;
                using (XmlTextReader reader = new XmlTextReader(manifestXMLPath))
                {
                    doc = new XmlDocument();
                    doc.Load(reader);
                    doc.SelectSingleNode("Manifest/version").InnerText =
                        Version;
                }
                doc.Save(manifestXMLPath);
                File.Delete(manifestXMLPathTarget);
                File.Copy(manifestXMLPath, manifestXMLPathTarget);   
            }
            
            var changelogPath = workspacePath + @"\About\Changelog.txt";
            if (settings.autoGenerateChangelog)
            {
                ///// Generate a mod description
                var updateContents = new string[]{Version + " (" + PublishDateString + ")", "========================", Description, ""};
                if (!File.Exists(changelogPath))
                {
                    if (Program.UserAccepts("Changelog file not detected. Create new one? (Y/N): "))
                    {
                        File.WriteAllLines(changelogPath, updateContents);
                    }
                }
                else
                {
                    var lines = new List<string>(updateContents);
                    foreach (var line in File.ReadAllLines(changelogPath))
                        lines.Add(line);
                    File.WriteAllLines(changelogPath, lines.ToArray());
                }
            }


            ///// Generate a mod description
            var descriptionPath = workspacePath + @"\About\Description.txt";
            var descriptionPathTwo = targetPath + @"\About\Description.txt";
            if (settings.autoGenerateDescription)
            {
                if (!File.Exists(descriptionPath))
                {
                    if (Program.UserAccepts("Raw description file not detected. Create new one? (Y/N): "))
                    {
                        Console.WriteLine("Please input a description. Use \\n to input new lines.");
                        File.WriteAllText(descriptionPath, Console.ReadLine());
                        File.Copy(descriptionPath, descriptionPathTwo);
                    }
                    else goto generateDescriptionOut;
                }

                var aboutXMLPath = workspacePath + @"\About\About.xml";
                var aboutXMLPathTarget = targetPath + @"\About\About.xml";
                XmlDocument doc;
                var newDescription = Version + " (" + this.PublishDateString + ")" + "\n\n" + File.ReadAllText(descriptionPath);
                if (!String.IsNullOrEmpty(settings.PatronsFilePath))
                {
                    newDescription = newDescription + "\n\n" + settings.PatronsMessage + "\n";
                    newDescription = newDescription + "\nThese are the most excellent rim dwellers who support me: \n" + String.Join(", ", patrons);
                }
                if (settings.autoGenerateChangelog && File.Exists(changelogPath))
                    newDescription = newDescription + "\n\n========================\nChangelog\n========================\n" + File.ReadAllText(changelogPath);
                //Adjust the version number in the about.xml
                using (XmlTextReader reader = new XmlTextReader(aboutXMLPath))
                {
                    doc = new XmlDocument();
                    doc.Load(reader); //Assuming reader is your XmlReader
                    doc.SelectSingleNode("ModMetaData/description").InnerText =
                        newDescription;
                    doc.SelectSingleNode("ModMetaData/targetVersion").InnerText =
                        settings.TargetVersion;
                }
                doc.Save(aboutXMLPath);
                File.Delete(aboutXMLPathTarget);
                File.Copy(aboutXMLPath, aboutXMLPathTarget);
                Console.WriteLine("Autogenerated Description: ");
                Console.WriteLine(newDescription);
                Console.WriteLine("Description Generated.\n");
            }
            generateDescriptionOut:

            if (settings.HandleGitHub)
            {
                var gitConfigPath = workspacePath + @"\.git\config";
                if (!File.Exists(gitConfigPath))
                {
                    Console.WriteLine("Warning - No .git folder detected.");
                }
                else
                {
                    var lines = File.ReadAllLines(gitConfigPath);
                    var urlLine = lines.FirstOrDefault(x => x.Contains("url ="));
                    urlLine = urlLine.ClearWhiteSpace();
                    //https://github.com/jecrell/Call-of-Cthulhu---Cosmic-Horrors.git
                    urlLine = urlLine.Replace("url=", "").Replace("https://github.com/", "").Replace(".git", "");
                    lines = urlLine.Split('/');
                    GitRepoAuthor = lines[0];
                    GitRepoName = lines[1];
                }

                Console.WriteLine(".git Config Detected.");
                Console.Write("Repository: " + GitRepoName + " Author: " + GitRepoAuthor);

                Client = new GitHubClient(new ProductHeaderValue("RimworldModReleaseTool"));
                var auth = GitRepoAuthor;
                if (Program.UserAccepts("\nLogin to GitHub? (Y/N): "))
                {
                    Console.WriteLine(
                        "\nConnecting to GitHub requires a login.\nPlease enter your credentials to proceed.");
                    Console.WriteLine("Username: ");
                    auth = Console.ReadLine();
                    Console.WriteLine("Password: ");
                    var key = Console.ReadLine();
                    Client.Credentials = new Credentials(auth, key);
                    //Get the user
                    GitHubAuthor = Client.User.Get(auth).Result.Name;
                    GitHubEmail = Client.User.Get(auth).Result.Email;
                }

                ///// Get the repo's preview image
                var repo = GitHubUtility.GetGithubRepository(this, settings, GitRepoName, GitRepoAuthor);

                URL = repo.HtmlUrl;
                ImageUrl = URL + "/master/About/Preview.png";
                ImageUrl =
                    ImageUrl.Replace("https://github.com/", "https://raw.githubusercontent.com/");
            }
        }

        public static bool IsFileReady(string filename)
        {
            // If the file can be opened for exclusive access it means that the file
            // is no longer locked by another process.
            try
            {
                using (FileStream inputStream = File.Open(filename, System.IO.FileMode.Open, FileAccess.Read, FileShare.None))
                    return inputStream.Length > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }
        
        public string GitHubEmail { get; }

        public string GitHubAuthor { get; }

        public GitHubClient Client { get; }

        public string GitRepoName { get; }

        public string GitRepoAuthor { get; }

        public string Path { get; }

        public string Name { get; }

        public string Title => Name + " - " + title;

        public string ZipTitle => title;
        
        public string Team { get; }

        public DateTime PublishDate { get; } = DateTime.Now;

        public string PublishDateString { get; }

        public string Version { get; }

        public string Description { get; }

        public string URL { get; }

        public string ImageUrl { get; }

        public string PatreonURL { get; }

        public string SteamURL { get; }

        public string DiscordURL { get; }

        public List<string> DiscordWebhookTokens { get; }

        public string LudeonURL { get; }

        private static string ParseAboutXMLFor(string element, string newPath)
        {
            var text = newPath + @"\About\About.xml";
            var xml = new XmlDocument();
            xml.Load(text);
            return XElement.Parse(xml.InnerXml).Element(element)?.Value ?? "NULL";
        }
    }
}