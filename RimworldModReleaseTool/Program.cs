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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using Microsoft.SqlServer.Server;
using Octokit;
using FileMode = System.IO.FileMode;
#pragma warning disable 1587

namespace RimworldModReleaseTool
{
    internal class Program
    {
        private static ReleaseSettings InitializeSettings(out ReleaseSettings settings)
        {
            var xml = new XmlDocument();
            xml.Load("config.xml");
            Console.WriteLine(xml.InnerXml);
            settings = null;
            using(TextReader sr = new StringReader(xml.InnerXml))
            {
                var serializer = new XmlSerializer(typeof(ReleaseSettings));
                settings =  (ReleaseSettings)serializer.Deserialize(sr);
                Console.WriteLine(settings.GithubProductHeaderValue);
            }
            
            if (settings == null)
            {
                Console.WriteLine("File config.xml is missing, unreadable, or inaccessible");
                throw new NullReferenceException();
            }
            return settings;
        }
        
        public static void Main(string[] args)
        {
            ReleaseSettings settings;
            InitializeSettings(out settings);
            
            string workspacePath = ResolvePathForWorkspace(args);
            string releasePath = ResolvePathForRelease(args);

            var curDirName = workspacePath.Split(Path.DirectorySeparatorChar).Last();

            
            //////////////////////////////
            /// Automating my Dev Process
            /////////////////////////////
            
            //////////////////////////////
            /// General
            //1. Make seperate directory for release.
                if (!DeleteExistingDirectoriesIfAny(releasePath, workspacePath)) goto Abort;
                CopyFilesAndDirectories(workspacePath, Regex.Replace(settings.FilteredWhenCopied, @"\s+", "").Split(','), releasePath);
            //2. Restart RimWorld for testing.
                RestartRimWorldRequest();
            
            if (!UserAccepts("\nPublish update for " + curDirName + "?"))
                goto Abort;
            ModUpdateInfo updateInfo = new ModUpdateInfo(settings, workspacePath);
            
            /////////////////////////////
            /// Publishing
            //1. Update GitHub
            GitHubCommitRequest(settings, updateInfo);
            GitHubReleaseRequest(settings, updateInfo);
            //2. Update Discord
            SendJSONToDiscordWebhook(settings, updateInfo);
            //3. Update Patreon
            OutputUpdateReport(settings, updateInfo);
            ZipFilesRequest(releasePath, updateInfo, Regex.Replace(settings.FilteredWhenZipped, @"\s+", "").Split(','));
            //4. Update Ludeon
            //5. Update Steam
            
            Abort:
            Console.WriteLine("\nFin."); // Any key to exit...");
        }

        private static string ResolvePathForWorkspace(string[] args)
        {
            string result = "";
            if (args != null && args.Length != 0)
            {
                //Case 1: One argument is passed. Suppose current directory is Workspace directory.
                if (args.Length == 1)
                    result = Directory.GetCurrentDirectory();
                //Case 2: Two arguments or more are passed. Assume the first argument is the Workspace directory.
                else
                {
                    result = args[0];
                }
            }
            if (result == "")
            {
                Console.WriteLine("Expected arguments passed. None received.\n" +
                                  "Unable to resolve path of mod workspace.\n" +
                                  "Please enter mod workspace directory path: ");
                result = Console.ReadLine();   
            }
            return result;
        }

        private static string ResolvePathForRelease(string[] args)
        {
            string result = "";
            if (args != null && args.Length != 0)
            {
                //Case 1: One argument is passed. Suppose argument is target directory.
                if (args.Length == 1)
                    result = Path.GetFullPath(args[0]);
                //Case 2: Two arguments or more are passed. Assume the second argument is the target directory.
                else
                    result = Path.GetFullPath(args[1]);
            }
            if (result == "")
            {
                Console.WriteLine("Expected arguments passed. None received.\n" +
                                  "Unable to resolve path of mod release directory.\n" +
                                  "Please enter mod release directory path: ");
                result = Console.ReadLine();   
            }
            return result;
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

        private static void OutputUpdateReport(ReleaseSettings settings, ModUpdateInfo updateInfo)
        {
            if (settings.ShowCopyableNotes)
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
                Process.Start(settings.CopyableNotesProgram, newFilePath);                
            }

        }


        private static void GitHubReleaseRequest(ReleaseSettings settings, ModUpdateInfo updateInfo)
        {
            if (settings.HandleGitHub && UserAccepts($"Make release on github? (Y/N) "))
            {
                var repo = GitHubUtility.GetGithubRepository(settings, updateInfo.Name);
                var task = Task.Run(async () => await GitHubUtility.CreateRelease(repo, version: updateInfo.Version, name: updateInfo.Title, body: updateInfo.Description));
                task.Wait();
                var result = task.Result;
                Console.WriteLine("Created release id {0}", result.Id);
            }
        }

        private static void GitHubCommitRequest(ReleaseSettings settings, ModUpdateInfo updateInfo)
        {
            if (settings.HandleGitHub && UserAccepts($"Push to github with commit? (Y/N) "))
            {
                var repo = GitHubUtility.GetGithubRepository(settings, updateInfo.Name);
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
#pragma warning disable 168
                catch (Exception e)
#pragma warning restore 168
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

        private static void SendJSONToDiscordWebhook(ReleaseSettings settings, ModUpdateInfo updateInfo)
        {
            if (settings.HandleDiscord && UserAccepts("Generate JSON message to Discord? (Y/N) : "))
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
}