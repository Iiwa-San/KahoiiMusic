; Script generated for Kahoii Music Installer
; Uses Inno Setup (https://jrsoftware.org/isinfo.php)

#define MyAppName "Kahoii Music"
#define MyAppVersion "1.0.12"
#define MyAppPublisher "Kahoii"
#define MyAppExeName "KahoiiMusic.exe"

[Setup]
; App Identity
AppId={{D37E6B4A-7351-4E7B-89E2-9A4B3A8242A5}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL=https://github.com/Kahoii
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes

; Output Configuration
OutputDir=publish\Installer
OutputBaseFilename=KahoiiMusic_Setup_v{#MyAppVersion}
SetupIconFile=app.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern

; Privileges: allows non-admin and admin install smoothly
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

; Uninstaller configuration (creates clean entry in Settings > Installed Apps)
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; Main application files from publish directory
Source: "publish\KahoiiMusic.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "publish\yt-dlp.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "publish\app.ico"; DestDir: "{app}"; Flags: ignoreversion
; Pre-create data and temp directories
Source: "publish\Saved\*"; DestDir: "{app}\Saved"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "publish\KahoiiTemp\*"; DestDir: "{app}\KahoiiTemp"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\app.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
