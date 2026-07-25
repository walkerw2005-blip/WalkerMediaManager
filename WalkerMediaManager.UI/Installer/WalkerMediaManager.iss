#define MyAppName "Walker Media Manager"
#define MyAppVersion "0.7.0"
#define MyAppPublisher "Walker Media Manager"
#define MyAppExeName "WalkerMediaManager.UI.exe"

[Setup]
AppId={{7B1F1D0A-BC75-4CD6-8796-BD27E1EA0E70}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Walker Media Manager
DefaultGroupName=Walker Media Manager
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=WalkerMediaManager-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupLogging=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\artifacts\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Walker Media Manager"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\Walker Media Manager"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch Walker Media Manager"; Flags: nowait postinstall skipifsilent
