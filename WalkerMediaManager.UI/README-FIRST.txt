WALKER MEDIA MANAGER v0.7.0 - CLICKABLE WINDOWS APP
===================================================

WHAT THIS UPDATE DOES
---------------------
- Publishes Walker Media Manager as a Release x64 Windows application.
- Makes the Windows App SDK and .NET runtime self-contained.
- Produces a normal EXE that opens by double-clicking it.
- Can create a standard Windows installer with Start Menu and optional Desktop shortcuts.
- Keeps the database under LocalAppData, so updates and uninstalls do not erase the collection.

INSTALL THE UPDATE FILES
------------------------
1. Close Walker Media Manager and Visual Studio.
2. Copy this package into the WalkerMediaManager.UI project folder.
3. Replace WalkerMediaManager.UI.csproj when prompted.
4. Keep the new Properties, Installer, CMD, and PowerShell files.

FASTEST OPTION: PORTABLE APP
----------------------------
1. Double-click Build-Portable.cmd.
2. Wait for the Release publish to finish.
3. Open artifacts\publish\win-x64.
4. Double-click WalkerMediaManager.UI.exe.
5. Right-click the EXE and choose Show more options > Send to > Desktop (create shortcut), if desired.

NORMAL INSTALLER OPTION
-----------------------
1. Install Inno Setup 6 once.
2. Double-click Build-Installer.cmd.
3. The installer will be created at:
   Installer\Output\WalkerMediaManager-Setup-0.7.0.exe
4. Double-click that setup file and follow the installer.
5. The installed app can then be opened from the Start Menu or Desktop shortcut.

REQUIREMENTS TO BUILD
---------------------
- Windows 10 or Windows 11, x64
- Visual Studio with the .NET desktop development and Windows application development workloads,
  or the .NET 8 SDK with the project's required Windows build tools
- Internet access during the first NuGet restore
- Inno Setup 6 only when building the setup EXE

IMPORTANT
---------
The application database is stored at:
%LOCALAPPDATA%\WalkerMediaManager\walker.db

The installer does not delete that database during upgrades or uninstall.
