; Inno Setup 6 script for NotepadMoreOS
; Build: ISCC tools\release\setup.iss /DMyAppVersion=1.7.0
; Or from macOS/Linux: tools/release/build-windows.sh

#define MyAppName "NotepadMoreOS"
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#define MyAppPublisher "NotepadMoreOS contributors"
#define MyAppExeName "NotepadMoreOS.exe"
#define MyAppDescription "A fast, cross-platform text and source code editor"
#define MyAppUrl "https://github.com/Agarni/NotepadMoreOS"
#define SourceDir "..\..\artifacts\windows-win-x64"

[Setup]
AppId={{896BC282-BFA8-4C2A-931F-FB6B5BC65B53}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppUrl}
AppSupportURL={#MyAppUrl}
AppUpdatesURL={#MyAppUrl}
AppCopyright=MIT License
VersionInfoVersion={#MyAppVersion}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=..\..\LICENSE
OutputDir=..\..\artifacts
OutputBaseFilename=NotepadMoreOS-{#MyAppVersion}-windows-x64-setup
SetupIconFile=..\..\src\NotepadMoreOS\Assets\appicon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DisableProgramGroupPage=yes
MinVersion=10.0
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce
Name: "associatetext"; Description: "Associate common text files (.txt, .log, .md)"; GroupDescription: "File associations:"; Flags: checkedonce
Name: "contextmenu"; Description: "Add ""Open with {#MyAppName}"" to the file context menu"; GroupDescription: "File associations:"; Flags: checkedonce

[Files]
Source: "{#SourceDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "..\..\README.md"; DestDir: "{app}"; DestName: "README.md"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Comment: "{#MyAppDescription}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; Comment: "{#MyAppDescription}"

[Registry]
; Lets "NotepadMoreOS" work from Win+R / Explorer address bar.
Root: HKA; Subkey: "Software\Microsoft\Windows\CurrentVersion\App Paths\{#MyAppExeName}"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName}"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Microsoft\Windows\CurrentVersion\App Paths\{#MyAppExeName}"; ValueType: string; ValueName: "Path"; ValueData: "{app}"; Flags: uninsdeletekey

; ProgID used by the optional associations.
Root: HKA; Subkey: "Software\Classes\NotepadMoreOS.TextFile"; ValueType: string; ValueName: ""; ValueData: "Text file"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\NotepadMoreOS.TextFile\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"
Root: HKA; Subkey: "Software\Classes\NotepadMoreOS.TextFile\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

Root: HKA; Subkey: "Software\Classes\.txt\OpenWithProgids"; ValueType: string; ValueName: "NotepadMoreOS.TextFile"; ValueData: ""; Tasks: associatetext; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.log\OpenWithProgids"; ValueType: string; ValueName: "NotepadMoreOS.TextFile"; ValueData: ""; Tasks: associatetext; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.md\OpenWithProgids"; ValueType: string; ValueName: "NotepadMoreOS.TextFile"; ValueData: ""; Tasks: associatetext; Flags: uninsdeletevalue

Root: HKA; Subkey: "Software\Classes\*\shell\NotepadMoreOS"; ValueType: string; ValueName: ""; ValueData: "Open with {#MyAppName}"; Tasks: contextmenu; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\*\shell\NotepadMoreOS"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName},0"; Tasks: contextmenu
Root: HKA; Subkey: "Software\Classes\*\shell\NotepadMoreOS\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: contextmenu

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
