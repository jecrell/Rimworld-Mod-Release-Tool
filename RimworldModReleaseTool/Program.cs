using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Schema;
using Microsoft.SqlServer.Server;
using Octokit;
using FileMode = System.IO.FileMode;
#pragma warning disable 1587

namespace RimworldModReleaseTool
{
    internal class Program
    {
        
        public static void Main(string[] args)
        {


            string currentPath = Directory.GetCurrentDirectory();
            string[] excludedPaths =
                {".git", "\\source", ".gitattributes", ".gitignore", ".idea", ".vs", ".exe", "Octokit"};
            string[] excludedZipFiles = {"PublishedFileId", "Deployer", "RimworldModReleaseTool", "updateinfo", "Octokit"};
            string path = Path.GetFullPath(args[0]);

            var curDirName = currentPath.Split(Path.DirectorySeparatorChar).Last();
            var task = Task.Run(async () => await GitHubUtility.GetRepoFromGitHub(curDirName));
            task.Wait();
            var repo = task.Result;
            
            //////////////////////////////
            /// Automating my Dev Process
            /////////////////////////////
            
            //////////////////////////////
            /// General
            //1. Make seperate directory for release.
                if (!DeleteExistingDirectoriesIfAny(path, currentPath)) goto Abort;
                CopyFilesAndDirectories(currentPath, excludedPaths, path);
            //2. Restart RimWorld for testing.
                RestartRimWorldRequest();
            
            if (!UserAccepts("\nPublish update for " + repo.Name + "?"))
                goto Abort;
            ModUpdateInfo updateInfo = new ModUpdateInfo(currentPath, repo);
            
            /////////////////////////////
            /// Publishing
            //1. Update GitHub
            GitHubCommitRequest(updateInfo);
            GitHubReleaseRequest(repo, updateInfo);
            //2. Update Discord
            GenerateJSONPost(updateInfo);
            //3. Update Patreon
            OutputUpdateReport(updateInfo);
            ZipFilesRequest(path, updateInfo, excludedZipFiles);
            //4. Update Ludeon
            //5. Update Steam
            
            Abort:
            Console.WriteLine("\nFin."); // Any key to exit...");
        }

        private static void HttpWebRequestWithJSON(string json, string token)
        {
            var httpWebRequest = (HttpWebRequest)
                WebRequest.Create(
                    token);
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "POST";

            using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
            {
                streamWriter.Write(json);
                Console.WriteLine(json);
                streamWriter.Flush();
                streamWriter.Close();
            }

            var result = "";
            var httpResponse = (HttpWebResponse) httpWebRequest.GetResponse();
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                result = streamReader.ReadToEnd();
            }
            //Console.WriteLine(result);
        }

        private static void OutputUpdateReport(ModUpdateInfo updateInfo)
        {
            StringBuilder s = new StringBuilder();
            
            s.AppendLine("==============================");
            s.AppendLine("==========Steam Format========");
            s.AppendLine("==============================");
            s.AppendLine();
            s.AppendLine(updateInfo.Name + " Update");
            s.AppendLine("====================");
            s.AppendLine("Version: " + updateInfo.Version);
            s.AppendLine("Updated: " + updateInfo.PublishDateString);
            s.AppendLine("Description: " + updateInfo.Description);
            s.AppendLine("====================");
            s.AppendLine("Greetings fellow RimWorlder,");
            s.AppendLine();
            s.AppendLine("Text goes here");
            s.AppendLine();
            s.AppendLine("Yours");
            s.AppendLine("-Jec");
            s.AppendLine();
            s.AppendLine("Download now on...");
            s.AppendLine("- Patreon: " + updateInfo.PatreonURL);
            s.AppendLine("- GitHub: " + updateInfo.URL);
            s.AppendLine("- Steam: " + updateInfo.SteamURL);
            s.AppendLine("Discuss the mod on...");
            s.AppendLine("- Discord: " + updateInfo.DiscordURL);
            s.AppendLine("- Ludeon forums: " + updateInfo.LudeonURL);
            s.AppendLine();
            
            s.AppendLine("==============================");
            s.AppendLine("===========BBS Format=========");
            s.AppendLine("==============================");
            s.AppendLine();
            s.AppendLine("[center][b][glow=red,2,300][size=18pt]" + updateInfo.Name + "[/size][/glow][/b]");
            s.AppendLine("[img width=260]" + updateInfo.ImageUrl + "[/img]");
            s.AppendLine("[hr]");
            s.AppendLine("[b]" + updateInfo.Name);
            s.AppendLine("Version: " + updateInfo.Version);
            s.AppendLine("Updated: " + updateInfo.PublishDateString);
            s.AppendLine("Description: [color=orange]" + updateInfo.Description + "[/color]");
            s.AppendLine("[hr][b]Notes from Jec:[/b][/center]");
            s.AppendLine();
            s.AppendLine("[center][tt]Greetings fellow RimWorlder,");
            s.AppendLine();
            s.AppendLine("Text goes here");
            s.AppendLine();
            s.AppendLine("[tt]Yours[/tt]");
            s.AppendLine("[list][li][tt]Jec[/tt][/li][/list]");
            s.AppendLine("[hr]");
            s.AppendLine("[b]Download now on...[/b]");
            s.AppendLine("[url=" + updateInfo.PatreonURL + "]Patreon[/url]");
            s.AppendLine("[url=" + updateInfo.URL + "]GitHub[/url]");
            s.AppendLine("[url=" + updateInfo.SteamURL + "]Steam[/url]");
            s.AppendLine("[b]Discuss the mod on...[/b]");
            s.AppendLine("[url=" + updateInfo.DiscordURL + "]Discord[/url]");
            var newFilePath = updateInfo.Path + @"\updateinfo";
            if (File.Exists(newFilePath))
            {
                File.Delete(newFilePath);
            }
            System.IO.File.WriteAllText(newFilePath, s.ToString());
            Process.Start("notepad.exe", newFilePath);
        }

        private static void GitHubReleaseRequest(Repository repo, ModUpdateInfo updateInfo)
        {
            if (UserAccepts($"Make release on github? (Y/N) "))
            {                           
                var task = Task.Run(async () => await GitHubUtility.CreateRelease(repo, version: updateInfo.Version, name: updateInfo.Title, body: updateInfo.Description));
                task.Wait();
                var result = task.Result;
                Console.WriteLine("Created release id {0}", result.Id);
            }
        }

        private static void GitHubCommitRequest(ModUpdateInfo updateInfo)
        {
            if (UserAccepts($"Push to github with commit? (Y/N) "))
            {
                GitHubUtility.RunGitProcessWithArgs(@"add -A");
                GitHubUtility.RunGitProcessWithArgs(@"commit " + updateInfo.Description);
                GitHubUtility.RunGitProcessWithArgs(@"push origin master");
            }
        }

        private static void RestartRimWorldRequest()
        {
            Process[] pname = Process.GetProcessesByName("RimWorldWin64");
            if (pname.Length != 0)
            {
                Console.WriteLine("\nActive RimWorld Detected.");
                if (UserAccepts($"Restart RimWorld process? (Y/N) "))
                {
                    var process = Process.GetProcessesByName("RimWorldWin64")[0];
                    process.Kill();
                    var processPath = process.MainModule.FileName;
                    Process.Start(processPath);
                }
            }
        }

        private static void ZipFilesRequest(string targetPath, ModUpdateInfo info, string[] excludedZipFiles)
        {
            if (UserAccepts($"\nCopying complete. Would you like to zip the files? (Y/N) "))
            {
                var name = info.Path.Substring(info.Path.LastIndexOf("\\", StringComparison.Ordinal) + 1);

                string note = "";
                Console.WriteLine("Add a note for the ZIP file or press ENTER to continue");
                note = Console.ReadLine();

                var path = targetPath + $"\\..\\{name}-{note}({info.Version})({info.PublishDateString}).zip";
                Console.WriteLine(path);
                string dest = Path.GetFullPath(path);

                if (File.Exists(dest))
                {
                    File.Delete(dest);
                }

                ZipFile.CreateFromDirectory(targetPath, dest);

                //Remove unwanted files from the zip
                using (FileStream zipToOpen = new FileStream(dest, FileMode.Open))
                {
                    using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Update))
                    {
                        foreach (var exclusion in excludedZipFiles)
                        {
                            var entry = archive.Entries.FirstOrDefault(x => x.Name.Contains(exclusion));
                            if (entry != null)
                            {
                                Console.WriteLine("Exclude Zip File: " + entry.Name);
                                entry.Delete();
                            }
                        }

                        //Push all files down into a subdirectory with the mod name.
                        List<string> fileList = archive.Entries.Select(fileEntry => fileEntry.FullName).ToList();
                        foreach (var file in fileList)
                        {
                            var oldName = file;
                            var newName = name + "\\" + file;

                            ZipArchiveEntry oldEntry = archive.GetEntry(oldName),
                                newEntry = archive.CreateEntry(newName);

                            using (var oldStream = oldEntry.Open())
                            using (var newStream = newEntry.Open())
                            {
                                oldStream.CopyTo(newStream);
                            }
                            oldEntry.Delete();
                        }
                    }
                }


                Console.WriteLine("Zipped and placed at " + dest);
            }
        }

        private static void CopyFilesAndDirectories(string currentPath, string[] excludedPaths, string path)
        {
            List<string> ToCopy = new List<string>();

            foreach (string dirPath in Directory.GetDirectories(currentPath, "*",
                SearchOption.AllDirectories))
            {
                if (excludedPaths.Any(exlusion =>
                    dirPath.ToLowerInvariant().Contains(exlusion) && !dirPath.ToLowerInvariant().Contains("resource")))
                {
                    Console.WriteLine("Exclude folder: " + dirPath);
                    continue;
                }

                Directory.CreateDirectory(dirPath.Replace(currentPath, path));
            }

            foreach (string file in Directory.GetFiles(currentPath, "*.*", SearchOption.AllDirectories))
            {
                if (excludedPaths.Any(exlusion => file.ToLowerInvariant().Contains(exlusion)))
                {
                    Console.WriteLine("Exclude File: " + file);
                    continue;
                }

                Console.WriteLine(file + " => " + file.Replace(currentPath, path));
                File.Copy(file, file.Replace(currentPath, path));
            }
            Console.WriteLine();
        }

        private static bool DeleteExistingDirectoriesIfAny(string path, string currentPath)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                Console.WriteLine("Create Directory: " + path);
            }

            if (!UserAccepts($"Copying from\n{currentPath}\nTo\n{path}\nIs this correct? (Y/N) "))
            {
                return false;
            }

            if (Directory.EnumerateFileSystemEntries(path).Any(file => file != null))
            {
                if (!UserAccepts($"Target path is not empty. Ok to delete files? (Y/N) "))
                {
                    return false;
                }

                try
                {
                    if (Directory.Exists(path))
                    {
                        Directory.Delete(path, true);
                    }
                }
                catch (Exception exception)
                {
                    Console.WriteLine("Failed to delete. Directory. Forcing delete and relaunching.");   
                    var process = Process.Start("C:\\Program Files\\LockHunter\\LockHunter.exe",
                        "-sm -d " + "\"" + path + "\"");
                    while (process != null && !process.HasExited)
                    {
                        process?.WaitForExit(Timeout.Infinite);                    
                    }
                    //Process.Start(Assembly.GetExecutingAssembly().Location, "\"" + path + "\"");
                    //Environment.Exit(0);
                }
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
                Directory.CreateDirectory(path);

                Console.WriteLine("-------------------------------------\n" +
                                  "Purged target directory of all files.\n" +
                                  "-------------------------------------");
            }

            Console.WriteLine();
            return true;
        }

        private static void GenerateJSONPost(ModUpdateInfo updateInfo)
        {
            if (UserAccepts("Generate JSON message to Discord? (Y/N) : "))
            {
                var updateInfoName = updateInfo.Name;
                var updateInfoVersion = updateInfo.Version;
                var updateInfoPublishDateString = updateInfo.PublishDateString;
                var updateInfoDescription = updateInfo.Description;
                var updateInfoPatreonUrl = updateInfo.PatreonURL;
                var updateInfoSteamUrl = updateInfo.SteamURL;
                var updateInfoUrl = updateInfo.URL;
                var updateInfoImageUrl = updateInfo.ImageUrl;
                var imgJson =
                    "{" +
                    "\"embeds\":[{\"image\":{\"url\":\"" + updateInfoImageUrl + "\"}}]" +          
                    "}";
                var json = "{" +
                           "\"content\":\": \\n\\n..-==========================-.\\n" +
                           "   __**" + updateInfoName + " Updated**__\\n" +
                           "    **Version:** ***" + updateInfoVersion + "***\\n" +
                           "    **Updated:** ***" + updateInfoPublishDateString + "***\\n" +
                           "    **Description:** \\n```" +
                           //"\"embeds\":[" +
                           //"{" +
                           updateInfoDescription + "```\\n" +
                           "  Download now on...\\n" +
                           "  * [Patreon](" + updateInfoPatreonUrl + ")\\n" +
                           "  * [Steam](" + updateInfoSteamUrl + ")\\n" +
                           "  * [GitHub](" + updateInfoUrl + ")\\n" +
                           "'-==========================-'\"" +
                           "}";
                HttpWebRequestWithJSON(imgJson, updateInfo.DiscordWebhookToken);
                HttpWebRequestWithJSON(json, updateInfo.DiscordWebhookToken);
            }
        }

        public static bool UserAccepts(string question)
        {
            char input;
            Console.Write(question);
            input = ' ';
            while (input != 'y' && input != 'n')
            {
                input = char.ToLower(Console.ReadKey().KeyChar);
            }

            Console.WriteLine();

            if (input == 'y')
            {
                Console.WriteLine("\n");
                return true;
            }
            return false;
        }


    }

    internal class ModUpdateInfo
    {
        public static readonly string RimWorldVer = "B19"; 
        
        private string path;
        private string team;
        private string name;
        private string title;
        private DateTime publishDate = DateTime.Now;
        private string publishDateString = null;
        private string version;
        private string description;
        private string url;
        private string imageURL;
        private string patreonURL;
        private string steamURL;
        private string discordURL;
        private string ludeonURL;
        private string webhookToken;
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

        public ModUpdateInfo(string newPath, Repository repo)
        {
            ///// Get the name
            path = newPath;
            name = repo.Name; //path.Substring(path.LastIndexOf("\\", StringComparison.Ordinal) + 1);
            team = repo.Owner.ToString();
            
            ///// Get the date
            publishDate = DateTime.Now;
            publishDateString = $"{publishDate:MM-dd-yyyy}";

            ///// Autoset a version number
            var daysSinceStarted = (DateTime.Now - new DateTime(2016, 12, 11)).Days;
            version = RimWorldVer + '.' + daysSinceStarted;
            
            ///// Get the repo's preview image
            url = repo.HtmlUrl;
            imageURL = url + "/master/About/Preview.png";
            imageURL =
                imageURL.Replace("https://github.com/", "https://raw.githubusercontent.com/");
            
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
            string steamPublishID = File.ReadLines(path + @"\About\PublishedFileId.txt").First();
            steamURL = @"https://steamcommunity.com/sharedfiles/filedetails/?id=" + steamPublishID;
            
            ///// Get the Patreon URL
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
            
            ///// Get the Discord URL
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
            
            ///// Get the Ludeon URL
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

            ///// Get the Discord Webhook
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

            ///// Get GitHub credentials
            

        }
    }
}