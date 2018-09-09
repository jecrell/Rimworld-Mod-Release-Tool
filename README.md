# RimWorld Command Line Mod Release Tool
### for your sanity

## Current Status: Not User Friendly

## What does this program do?
This program does the following.
1. Replaces the target mod directory with your unaltered mod workspace directory.
   -> Does not include /Source/ folder and other excluded files (see Program.cs for info)
2. Detects running RimWorld process. If it's on, the program asks for permission to restart it for the user.
3. Asks user if they want to publish the mod.
4. Asks user for update title.
5. Asks user for update description.
6. Automatically generates version number based on date and RimWorld version (RimWorld Version.Days since initial publish date) -> (B19.668).
7. Automatically finds GitHub link based on the mod's name by searching GitHub's repositories.

((The following steps are skipped if the URLs are already stored))
8. Creates Discord link from user input. (stores in <ModWorkspaceDir>/About/DiscordURL.txt)
9. Creates Patreon link from user input. (stores in <ModWorkspaceDir>/About/PatreonURL.txt)
10. Creates Ludeon link from user input. (stores in <ModWorkspaceDir>/About/LudeonURL.txt)
11. Creates Discord webhook link from user input. (stores in <ModWorkspaceDir>/Source/DiscordWebhookToken)
	
((End data input))

12. Asks user if they want to publish a commit to GitHub.
13. Asks user if they want to publish a release to GitHub.
	-> Asks user for credentials (username and pw)
14. Sends JSON post to Discord webhooks with mod preview image followed by mod description.
15. Automatically generates Steam and BBCode versions of the update text and opens Notepad.exe.
16. Asks user if they want to zip up their RiMWorld mod.
	-> Zips will exclude PublishedId.txt and other exceptions (see Program.cs for details)


## How to use this command line:
<<Make sure you create a workspace folder for your mod outside of the RimWorld/Mods directory. This will be your mod workspace where you can keep your source code excluded from your published mod.>>
1. Place this program and Octokit.dll in your workspace directory.
2. Pass the target mod directory as your argument on the command line.
  e.g.
  ``````RimworldModReleaseTool.exe "C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Call-of-Cthulhu---Cosmic-Horrors"```
3. Follow the instructions on screen.

Note: This release tool was originally started by Samboy from @SamboyCoding to delete a mod directory and replace it with a workspace directory without extra fluff. I've since added more changes to automate my coding process in RimWorld to make things easier on myself. Hopefully, you will find RimWorld easier to mod as well.
