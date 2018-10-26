# RimWorld Command Line Mod Release Tool
### for your sanity

## Current Status: Not User Friendly

## What does this program do?
This program does the following.
1. Replaces the target mod directory with your unaltered mod workspace directory.
   -> Does not include /Source/ folder and other excluded files (see Program.cs for info)
2. Restarts RimWorld.
3. Manages version numbers.
4. Creates/uses Discord, Patreon, Ludeon, and Discord Webhook links (stores in <ModWorkspaceDir>/About/... and <ModWorkspaceDir/Source/...)
5. Auto-generates changelogs and About.xml by using a pre-existing Description.txt file.
6. Commits to GitHub
7. Releases to GitHub
8. Sends JSON post to Discord webhooks with mod preview image followed by mod description.
9. Automatically generates Steam and BBCode versions of the update text and opens Notepad.exe.
10. Zips RiMWorld mod with version number and note.
	-> Zips will exclude PublishedId.txt and other exceptions (see Program.cs for details)

## How to use this command line:
<<Make sure you create a workspace folder for your mod outside of the RimWorld/Mods directory. This will be your mod workspace where you can keep your source code excluded from your published mod.>>
1. Place this program and Octokit.dll in your workspace directory.
2. Pass the target mod directory as your argument on the command line.
  e.g.
  ``````RimworldModReleaseTool.exe "C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Call-of-Cthulhu---Cosmic-Horrors"```
3. Follow the instructions on screen.

Note: This release tool was originally started by Samboy from @SamboyCoding to delete a mod directory and replace it with a workspace directory without extra fluff. I've since added more changes to automate my coding process in RimWorld to make things easier on myself. Hopefully, you will find RimWorld easier to mod as well.
