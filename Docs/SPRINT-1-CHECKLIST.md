# Sprint 1 Acceptance Checklist

## Installer and application

- [ ] GitHub Actions completes successfully.
- [ ] The artifact contains `WalkerMediaManagerSetup.exe`.
- [ ] The installer launches without Visual Studio or a separate .NET installation.
- [ ] The application appears in the Start Menu.
- [ ] The optional desktop shortcut is created when selected.
- [ ] The application can be uninstalled through Windows Settings.

## Local foundation

- [ ] The application launches.
- [ ] SQLite database is created automatically.
- [ ] Dashboard reports the database as ready.
- [ ] Logs are written under `%LOCALAPPDATA%\Walker Media Manager`.

## Plex connection

- [ ] Plex server URL can be saved.
- [ ] Plex token is retained after restarting the app.
- [ ] The token is not present in `settings.json`.
- [ ] Test Connection returns a clear success or failure message.
- [ ] Movie and TV libraries display after a successful connection.
- [ ] Discovered libraries remain in SQLite after restarting.

## Updates

- [ ] A tagged build creates a GitHub Release.
- [ ] The Updates page reports the installed version.
- [ ] The Updates page identifies a newer tagged release.
- [ ] Download Update retrieves and opens the installer.

## Deferred to Sprint 2

- Movie record import
- TV series, season, and episode import
- Spreadsheet import
- Plex-to-spreadsheet matching
- Duplicate purchase warnings
