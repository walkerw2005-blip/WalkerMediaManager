Walker Media Manager 1.0.0 RC1 - Branding and Installer
=========================================================

This release candidate adds professional product branding and installer polish.

Highlights
----------
- Executable renamed to WalkerMediaManager.exe
- Product name: Walker Media Manager
- Company: Walker Software
- File description: Personal Media Collection Manager
- Version metadata: 1.0.0 RC1
- New W film-strip icon embedded in the EXE
- Matching Windows package logos and splash artwork
- Branded Inno Setup wizard artwork
- Start Menu and optional Desktop shortcuts
- Installer and uninstaller use the new application icon
- Duplicate using-directive warnings removed from App.xaml.cs

Build the portable application
------------------------------
Double-click Build-Portable.cmd.
The executable is created at:
artifacts\publish\win-x64\WalkerMediaManager.exe

Build the installer
-------------------
Install Inno Setup 6, then double-click Build-Installer.cmd.
The installer is created at:
Installer\Output\WalkerMediaManager-Setup-1.0.0-RC1.exe

Existing data
-------------
Your existing database remains in the same LocalAppData location and is not removed by an upgrade.
