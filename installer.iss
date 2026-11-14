; Script generated for Kahoii Music Installer
; Uses Inno Setup (https://jrsoftware.org/isinfo.php)

#define MyAppName "Kahoii Music"
#define MyAppVersion "1.0.34"
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
; Main application files from release build directory
Source: "bin\Release\KahoiiMusic.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "yt-dlp.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "app.ico"; DestDir: "{app}"; Flags: ignoreversion

[Dirs]
Name: "{app}\Saved"; Permissions: users-modify
Name: "{app}\Saved\audio"; Permissions: users-modify
Name: "{app}\Saved\thumb"; Permissions: users-modify
Name: "{app}\Saved\lyrics"; Permissions: users-modify
Name: "{app}\KahoiiTemp"; Permissions: users-modify
Name: "{app}\KahoiiTemp\audio"; Permissions: users-modify
Name: "{app}\KahoiiTemp\thumb"; Permissions: users-modify
Name: "{app}\KahoiiTemp\lyrics"; Permissions: users-modify

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\app.ico"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\app.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

