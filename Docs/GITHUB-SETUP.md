# GitHub Setup — No Visual Studio Required

## Part 1: Create the repository

1. Sign in to GitHub.
2. Select **New repository**.
3. Repository name: `WalkerMediaManager`.
4. Choose **Public** so installed copies can read GitHub Releases for automatic updates. Use **Private** only if you are comfortable updating manually.
5. Do not add a README, license, or `.gitignore` during creation because those files are already included.
6. Select **Create repository**.

## Part 2: Upload Sprint 1

The easiest method is GitHub Desktop:

1. Install and open GitHub Desktop.
2. Choose **File → Add local repository**.
3. Select the extracted Sprint 1 folder.
4. If prompted, choose **Create a repository here**.
5. Commit the files with the message `Sprint 1 foundation`.
6. Select **Publish repository** and leave **Keep this code private** cleared for automatic update support.

The `.github/workflows/build-windows-installer.yml` file must be present. It may be hidden in Windows File Explorer because its folder begins with a period.

## Part 3: Build the installer

1. Open the repository on GitHub.
2. Select **Actions**.
3. Select **Build Windows Installer** on the left.
4. Select **Run workflow**.
5. Leave the branch set to `main` and confirm.
6. Wait for the workflow to finish with a green check.
7. Open the completed workflow run.
8. Under **Artifacts**, download `WalkerMediaManager-Setup-...`.
9. Extract the downloaded ZIP.
10. Run `WalkerMediaManagerSetup.exe`.

## Part 4: Publish the first release

The app's update feature looks at GitHub Releases. To activate it, publish version `v0.1.0`:

1. In GitHub Desktop, open **Repository → Open in Command Prompt**.
2. Run:

```text
git tag v0.1.0
git push origin v0.1.0
```

3. GitHub Actions will build the installer again and automatically create the release.
4. Future installed versions can check that release feed for updates.

## Windows warning during installation

Early builds are not digitally signed. Windows SmartScreen may show **Windows protected your PC**. Select **More info**, verify the application name, and choose **Run anyway**. A commercial code-signing certificate can be added before a public release.
