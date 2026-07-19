# Walker Media Manager

**Sprint 1 / Alpha 0.1**

Walker Media Manager is a Windows collection manager designed to prevent duplicate movie and TV purchases by comparing ownership records with a Plex library.

## Sprint 1 delivers

- A native WinUI 3 Windows desktop application
- A professional `WalkerMediaManagerSetup.exe` installer
- Start Menu entry and optional desktop shortcut
- Automatic local SQLite database creation
- Plex server URL and token settings
- Secure Plex token storage in Windows Credential Locker
- Plex connection testing and library discovery
- Local persistence of discovered libraries
- Application logging under `%LOCALAPPDATA%\Walker Media Manager`
- GitHub Releases update checking, download, and installer launch
- Automated Windows builds through GitHub Actions

Sprint 1 does **not** import individual movies or television episodes yet. That is Sprint 2.

## Build without Visual Studio

GitHub Actions performs the Windows build and creates the installer.

1. Create a public GitHub repository named `WalkerMediaManager`.
2. Upload every file and folder in this package, including the hidden `.github` folder.
3. Open the repository's **Actions** tab.
4. Select **Build Windows Installer**.
5. Select **Run workflow**.
6. When the workflow finishes, open the completed run.
7. Download the `WalkerMediaManager-Setup-...` artifact.
8. Extract the artifact and run `WalkerMediaManagerSetup.exe`.

Detailed steps are in [`Docs/GITHUB-SETUP.md`](Docs/GITHUB-SETUP.md).

## Publishing a release

Create and push a tag such as `v0.1.0`. The workflow will:

1. Build the self-contained Windows application.
2. Compile `WalkerMediaManagerSetup.exe`.
3. Create a GitHub Release.
4. Attach the installer to the release.

Installed copies use that release feed to find and download later updates.

## First run

1. Open **Plex Connection**.
2. Enter the local Plex address, usually similar to `http://192.168.1.100:32400`.
3. Enter the Plex token.
4. Select **Test Connection**.
5. Confirm that your movie and television libraries appear.

## Local files

- Database: `%LOCALAPPDATA%\Walker Media Manager\walker-media.db`
- Settings: `%LOCALAPPDATA%\Walker Media Manager\settings.json`
- Log: `%LOCALAPPDATA%\Walker Media Manager\walker-media.log`

The Plex token is stored separately in Windows Credential Locker and is not written into `settings.json`.

## Architecture

- .NET 10
- WinUI 3 / Windows App SDK
- SQLite through Microsoft.Data.Sqlite
- Inno Setup installer
- GitHub Actions and GitHub Releases
