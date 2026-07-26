#define MyAppName "Walker Media Manager"
#define MyAppVersion "1.0.0 RC1"
#define MyAppPublisher "Walker Software"
#define MyAppExeName "WalkerMediaManager.exe"

[Setup]
AppId={{7B1F1D0A-BC75-4CD6-8796-BD27E1EA0E70}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL=https://github.com/walkerw2005-blip/WalkerMediaManager
DefaultDirName={autopf}\Walker Media Manager
DefaultGroupName=Walker Media Manager
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=WalkerMediaManager-Setup-1.0.0-RC1
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=..\Assets\WalkerMediaManager.ico
WizardImageFile=..\Assets\InstallerWizard.bmp
WizardSmallImageFile=..\Assets\InstallerSmall.bmp
SetupLogging=yes
VersionInfoVersion=1.0.0.0
VersionInfoCompany=Walker Software
VersionInfoDescription=Walker Media Manager Setup
VersionInfoProductName=Walker Media Manager
VersionInfoCopyright=Copyright (C) 2026 Walker Software

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\artifacts\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Walker Media Manager"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\Walker Media Manager"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch Walker Media Manager"; Flags: nowait postinstall skipifsilent
