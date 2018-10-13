using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management;
using System.Management.Automation;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

#pragma warning disable 1587

namespace RimworldModReleaseTool
{
    internal class Program
    {
        private static ReleaseSettings InitializeSettings(out ReleaseSettings settings)
        {
            var xml = new XmlDocument();
            xml.Load("config.xml");
            settings = null;
            using (TextReader sr = new StringReader(xml.InnerXml))
            {
                var serializer = new XmlSerializer(typeof(ReleaseSettings));
                settings = (ReleaseSettings) serializer.Deserialize(sr);
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
            InitializeProgram();
            ReleaseSettings settings;
            InitializeSettings(out settings);

            var workspacePath = ResolvePathForWorkspace(args);
            var releasePath = ResolvePathForRelease(args);

            var curDirName = workspacePath.Split(Path.DirectorySeparatorChar).Last();
            ////////////////////////////////////////
            /// Automating the RimWorld Dev Process
            ////////////////////////////////////////

            //////////////////////////////
            /// Testing or Releasing
            //1. Make seperate directory for release.
            if (!DeleteExistingDirectoriesIfAny(releasePath, workspacePath)) goto Abort;
            CopyFilesAndDirectories(workspacePath, settings.FilteredWhenCopied.ClearWhiteSpace().Split(','),
                releasePath);
            //2. Restart RimWorld for testing.
            RestartRimWorldRequest();

            if (!UserAccepts("\nPublish update for " + curDirName + "? (Y/N) :"))
                goto Abort;
            var updateInfo = new ModUpdateInfo(settings, workspacePath, releasePath);

            /////////////////////////////
            /// Publishing
            //1. Update GitHub
            GitHubCommitRequest(settings, updateInfo);
            GitHubReleaseRequest(settings, updateInfo);
            //2. Update Patreon
            //PatreonPostRequest(settings, updateInfo); //TODO
            ZipFilesRequest(releasePath, updateInfo, settings.FilteredWhenZipped.ClearWhiteSpace().Split(','));
            //3. Update Ludeon
            //LudeonPostRequest(settings, updateInfo); //TODO
            //4. Update Steam
            SteamUpdateRequest(settings, updateInfo, releasePath, workspacePath); //TODO
            //5. Update Discord
            SendJSONToDiscordWebhook(settings, updateInfo);
            //6. Print reports
            OutputUpdateReport(settings, updateInfo);

            Abort:
            Console.WriteLine("\nFin."); // Any key to exit...");
        }

        private static void InitializeProgram()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                var resourceName = "AssemblyLoadingAndReflection." +
                                   new AssemblyName(args.Name).Name + ".dll";
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                {
                    var assemblyData = new byte[stream.Length];
                    stream.Read(assemblyData, 0, assemblyData.Length);
                    return Assembly.Load(assemblyData);
                }
            };
        }

        private static void PatreonPostRequest(ReleaseSettings settings, ModUpdateInfo updateInfo)
        {
            if (UserAccepts("Update Patreon post now? (Y/N): ")) PatreonUtility.UpdatePost(settings, updateInfo);
        }

        private static void SteamUpdateRequest(ReleaseSettings settings, ModUpdateInfo updateInfo, string releasePath, string workspacePath)
        {
            if (UserAccepts("Upload to Steam workshop now? (Y/N): "))
                try
                {
                    var publishedIdPath = releasePath + @"\About\PublishedFileId.txt";
                    var publishedIdPathTwo = workspacePath + @"\About\PublishedFileId.txt";
                    if (!File.Exists(publishedIdPath))
                        if (UserAccepts("\nPublishedFileId.txt not detected. Input workshop ID now? (Y / N): "))
                        {
                            Console.WriteLine("\nPlease enter the workshop ID OR Press ENTER to continue: ");
                            var newPublishedId = Console.ReadLine();
                            using (var sw = File.CreateText(publishedIdPath))
                            {
                                sw.WriteLine(newPublishedId);
                            }
                            using (var sw = File.CreateText(publishedIdPathTwo))
                            {
                                sw.WriteLine(newPublishedId);
                            }
                        }

                    var mod = new Mod(releasePath);
                    SteamUtility.Init();
                    Console.WriteLine(mod.ToString());
                    if (SteamUtility.Upload(mod, UpdateReport(updateInfo))) Console.WriteLine("Upload done");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
                finally
                {
                    SteamUtility.Shutdown();
                }
        }

        private static void LudeonPostRequest(ReleaseSettings settings, ModUpdateInfo updateInfo)
        {
            if (settings.HandleLudeon && UserAccepts("Update Ludeon forum thread front page now? (Y/N): "))
                LudeonUtility.UpdatePost(settings, updateInfo);
        }

        private static string ResolvePathForWorkspace(string[] args)
        {
            var result = "";
            if (args?.Length > 0)
            {
                Console.WriteLine("Args check");
                //Case 1: One argument is passed. Suppose argument is Workspace directory.
                if (args.Length == 1)
                {
                    Console.WriteLine("Args length 1");
                    result = args[0]; //Directory.GetCurrentDirectory();
                }
                //Case 2: Two arguments or more are passed. Assume the first argument is the Workspace directory.
                else
                    result = args[0];
            }

            if (result == "")
            {
                WorkspacePath:
                Console.WriteLine("Please enter mod workspace directory path OR Press ENTER to exit: ");
                result = Console.ReadLine();
                if (result == "")
                {
                    Console.WriteLine("Exiting...");
                    Environment.Exit(0);
                }

                if (!Directory.Exists(result))
                {
                    Console.WriteLine("Invalid directory path.");
                    goto WorkspacePath;
                }
            }

            return result;
        }

        private static string ResolvePathForRelease(string[] args)
        {
            var result = "";
            if (args != null && args.Length != 0)
            {
                //Case 1: One argument is passed. Suppose argument is target directory.
                if (args.Length == 1)
                    result = Path.GetFullPath(args[0]).Replace("Workspace", "");
                //Case 2: Two arguments or more are passed. Assume the second argument is the target directory.
                else
                    result = Path.GetFullPath(args[1]);
            }

            if (result == "")
            {
                ReleasePath:
                Console.WriteLine("Please enter mod release directory path OR Press ENTER to exit: ");
                result = Console.ReadLine();
                if (result == "")
                {
                    Console.WriteLine("Exiting...");
                    Environment.Exit(0);
                }

                if (!Directory.Exists(result))
                {
                    if (UserAccepts("Target path does not exist. Create directory? (Y / N): "))
                    {
                        Directory.CreateDirectory(result);
                        return result;
                    }

                    Console.WriteLine("Invalid directory path.");
                    goto ReleasePath;
                }
            }

            return result;
        }

        private static void HttpWebRequestWithJSON(string json, List<string> tokens)
        {
            foreach (string token in tokens)
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
            }

            //Console.WriteLine(result);
        }

        private static void OutputUpdateReport(ReleaseSettings settings, ModUpdateInfo updateInfo)
        {
            if (settings.ShowCopyableNotes)
            {

                var s = new StringBuilder();
                s.AppendLine("==============================");
                s.AppendLine("==========Steam Format========");
                s.AppendLine("==============================");
                s.AppendLine();
                s.Append(UpdateReport(updateInfo));
                s.AppendLine();
                s.AppendLine("Download now on...");
                if (settings.HandlePatreon) s.AppendLine("- Patreon: " + updateInfo.PatreonURL);
                if (settings.HandleGitHub) s.AppendLine("- GitHub: " + updateInfo.URL);
                if (settings.HandleSteam) s.AppendLine("- Steam: " + updateInfo.SteamURL);
                s.AppendLine("Discuss the mod on...");
                if (settings.HandleDiscord) s.AppendLine("- Discord: " + updateInfo.DiscordURL);
                if (settings.HandleLudeon) s.AppendLine("- Ludeon forums: " + updateInfo.LudeonURL);
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
//                s.AppendLine("[hr][b]Notes from Jec:[/b][/center]");
//                s.AppendLine();
//                s.AppendLine("[center][tt]Greetings fellow RimWorlder,");
//                s.AppendLine();
//                s.AppendLine("Text goes here");
//                s.AppendLine();
//                s.AppendLine("[tt]Yours[/tt]");
//                s.AppendLine("[list][li][tt]Jec[/tt][/li][/list]");
                s.AppendLine("[hr]");
                s.AppendLine("[b]Download now on...[/b]");
                if (settings.HandlePatreon) s.AppendLine("[url=" + updateInfo.PatreonURL + "]Patreon[/url]");
                if (settings.HandleGitHub) s.AppendLine("[url=" + updateInfo.URL + "]GitHub[/url]");
                if (settings.HandleSteam) s.AppendLine("[url=" + updateInfo.SteamURL + "]Steam[/url]");
                s.AppendLine("[b]Discuss the mod on...[/b]");
                if (settings.HandleDiscord) s.AppendLine("[url=" + updateInfo.DiscordURL + "]Discord[/url]");
                if (settings.HandleLudeon) s.AppendLine("[url=" + updateInfo.LudeonURL + "]Ludeon[/url]");
                var newFilePath = updateInfo.Path + @"\updateinfo";
                if (File.Exists(newFilePath)) File.Delete(newFilePath);
                File.WriteAllText(newFilePath, s.ToString());
                Process.Start(settings.CopyableNotesProgram, newFilePath);
            }
        }

        private static string UpdateReport(ModUpdateInfo updateInfo)
        {
            var s = new StringBuilder();
            s.AppendLine(updateInfo.Name + " Update");
            s.AppendLine("====================");
            s.AppendLine("Version: " + updateInfo.Version);
            s.AppendLine("Updated: " + updateInfo.PublishDateString);
            s.AppendLine("Description: " + updateInfo.Description);
            s.AppendLine("====================");
            return s.ToString();
        }

        private static void GitHubReleaseRequest(ReleaseSettings settings, ModUpdateInfo updateInfo)
        {
            if (settings.HandleGitHub && UserAccepts("\nMake release on github? (Y/N) "))
            {
                Console.WriteLine("Acquiring repository...");
                var repo = GitHubUtility.GetGithubRepository(updateInfo, settings, updateInfo.GitRepoName,
                    updateInfo.GitRepoAuthor);
                Console.WriteLine("Acquired repository: " + repo.Name + " by " + repo.Owner.Login);
                var task = Task.Run(async () => await GitHubUtility.CreateRelease(updateInfo, repo, updateInfo.Version,
                    updateInfo.Title, updateInfo.Description));
                task.Wait();
                var result = task.Result;
                Console.WriteLine("Created release id {0}", result.Id);
            }
        }

        private static void GitHubCommitRequest(ReleaseSettings settings, ModUpdateInfo updateInfo)
        {
            if (settings.HandleGitHub && UserAccepts("\nPush to github with commit? (Y/N) "))
                GitHubUtility.RunGitProcessWithArgs(updateInfo.Path, updateInfo.Name + " " + updateInfo.Description);
        }

        private static void RestartRimWorldRequest()
        {
            var wmiQueryString = "SELECT ProcessId, ExecutablePath, CommandLine FROM Win32_Process";
            using (var searcher = new ManagementObjectSearcher(wmiQueryString))
            using (var results = searcher.Get())
            {
                var query = from p in Process.GetProcesses()
                    join mo in results.Cast<ManagementObject>()
                        on p.Id equals (int) (uint) mo["ProcessId"]
                    select new
                    {
                        Process = p,
                        Path = (string) mo["ExecutablePath"],
                        CommandLine = (string) mo["CommandLine"],
                    };
                foreach (var item in query)
                {
                    if (item?.Path?.Contains("RimWorldWin64.exe") == true)
                    {
                        Console.WriteLine("\nActive RimWorld Detected.");
                        if (UserAccepts("Restart RimWorld process? (Y/N) "))
                        {
                            var processPath = item.Path;
                            ProcessStartInfo psi = new ProcessStartInfo();
                            psi.FileName = "CMD.EXE";
                            psi.Arguments = "/K \"" + processPath + "\"" + (item.Process?.StartInfo.Arguments ?? "");
                            Process.Start(psi);
                            item.Process.CloseMainWindow();
                            break;
                        }
                    }
                }
            }

            //var process = Process.GetProcessesByName("RimWorldWin64")[0];
        }

        private static void ZipFilesRequest(string targetPath, ModUpdateInfo info, string[] excludedZipFiles)
        {
            if (UserAccepts("\nWould you like to zip the files? (Y/N) "))
            {
                var name = info.Path.Substring(info.Path.LastIndexOf("\\", StringComparison.Ordinal) + 1);
                var note = "";
                Console.WriteLine("Add a note for the ZIP file or press ENTER to continue");
                note = Console.ReadLine();
                var path = targetPath + $"\\..\\{name}-({note})({info.Version})({info.PublishDateString}).zip";
                Console.WriteLine(path);

                var dest = Path.GetFullPath(path);
                if (File.Exists(dest)) File.Delete(dest);
                ZipFile.CreateFromDirectory(targetPath, dest);

                //Remove unwanted files from the zip
                using (var zipToOpen = new FileStream(dest, FileMode.Open))
                {
                    using (var archive = new ZipArchive(zipToOpen, ZipArchiveMode.Update))
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
                        var fileList = archive.Entries.Select(fileEntry => fileEntry.FullName).ToList();
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
            var ToCopy = new List<string>();
            foreach (var dirPath in Directory.GetDirectories(currentPath, "*",
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

            foreach (var file in Directory.GetFiles(currentPath, "*.*", SearchOption.AllDirectories))
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

        private static bool DeleteExistingDirectoriesIfAny(string releasePath, string workspacePath)
        {
            if (!Directory.Exists(releasePath))
            {
                Directory.CreateDirectory(releasePath);
                Console.WriteLine("Create Directory: " + releasePath);
            }

            if (!UserAccepts($"Copying from\n{workspacePath}\nTo\n{releasePath}\nIs this correct? (Y/N) ")) return false;
            if (Directory.EnumerateFileSystemEntries(releasePath).Any(file => file != null))
            {
                if (!UserAccepts("Target path is not empty. Ok to delete files? (Y/N) ")) return false;
                try
                {
                    if (Directory.Exists(releasePath)) Directory.Delete(releasePath, true);
                }
#pragma warning disable 168
                catch (Exception e)
#pragma warning restore 168
                {
                    var lockhunterPath = "C:\\Program Files\\LockHunter\\LockHunter.exe";
                    if (File.Exists(lockhunterPath))
                    {
                        Console.WriteLine("Failed to delete. Lock hunter detected. Attempting forced deletion...");
                        var process = Process.Start(lockhunterPath,
                            "-sm -d " + "\"" + releasePath + "\"");
                        while (process != null && !process.HasExited) process?.WaitForExit(Timeout.Infinite);
//Process.Start(Assembly.GetExecutingAssembly().Location, "\"" + path + "\"");
                        while (Directory.Exists(releasePath)) {}
//Environment.Exit(0);   
                    }
                }

                if (Directory.Exists(releasePath)) Directory.Delete(releasePath, true);
                Directory.CreateDirectory(releasePath);
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
                var jsonB = new StringBuilder();
                jsonB.Append("{");
                jsonB.Append("\"content\":\": \\n\\n..-==========================-.\\n");
                jsonB.Append("   __**" + updateInfoName + " Updated**__\\n");
                jsonB.Append("    **Version:** ***" + updateInfoVersion + "***\\n");
                jsonB.Append("    **Updated:** ***" + updateInfoPublishDateString + "***\\n");
                jsonB.Append("    **Description:** \\n```");
                jsonB.Append(updateInfoDescription + "```\\n");
                jsonB.Append("  Download now on...\\n");
                if (settings.HandlePatreon) jsonB.Append("  * [Patreon](" + updateInfoPatreonUrl + ")\\n");
                if (settings.HandleSteam) jsonB.Append("  * [Steam](" + updateInfoSteamUrl + ")\\n");
                if (settings.HandleGitHub) jsonB.Append("  * [GitHub](" + updateInfoUrl + ")\\n");
                jsonB.Append("'-==========================-'\"");
                jsonB.Append("}");

//                var json = "{" +
//                           "\"content\":\": \\n\\n..-==========================-.\\n" +
//                           "   __**" + updateInfoName + " Updated**__\\n" +
//                           "    **Version:** ***" + updateInfoVersion + "***\\n" +
//                           "    **Updated:** ***" + updateInfoPublishDateString + "***\\n" +
//                           "    **Description:** \\n```" +
//                           //"\"embeds\":[" +
//                           //"{" +
//                           updateInfoDescription + "```\\n" +
//                           "  Download now on...\\n" +
//                           "  * [Patreon](" + updateInfoPatreonUrl + ")\\n" +
//                           "  * [Steam](" + updateInfoSteamUrl + ")\\n" +
//                           "  * [GitHub](" + updateInfoUrl + ")\\n" +
//                           "'-==========================-'\"" +
//                           "}";
                HttpWebRequestWithJSON(imgJson, updateInfo.DiscordWebhookTokens);
                HttpWebRequestWithJSON(jsonB.ToString(), updateInfo.DiscordWebhookTokens);
            }
        }

        public static bool UserAccepts(string question)
        {
            char input;
            Console.Write(question);
            input = ' ';
            while (input != 'y' && input != 'n') input = char.ToLower(Console.ReadKey().KeyChar);
            Console.WriteLine();
            return input == 'y';
        }
    }
}