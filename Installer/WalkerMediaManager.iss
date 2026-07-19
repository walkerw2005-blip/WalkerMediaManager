#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif

#define AppName "Walker Media Manager"
#define AppPublisher "Walker Media Manager"
#define AppExeName "WalkerMediaManager.exe"

[Setup]
AppId={{7C2805B4-8CC7-4D9B-BD04-70D71EF5D54A}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\Walker Media Manager
DefaultGroupName=Walker Media Manager
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=WalkerMediaManagerSetup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
UninstallDisplayIcon={app}\{#AppExeName}
SetupLogging=yes
CloseApplications=yes
RestartApplications=no
VersionInfoVersion={#AppVersion}
VersionInfoProductName={#AppName}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription=Walker Media Manager Installer

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Walker Media Manager"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\Walker Media Manager"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch Walker Media Manager"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;
