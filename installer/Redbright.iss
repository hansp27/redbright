; Inno Setup script for Redbright
; Requires Inno Setup 6.x (iscc.exe)

#define MyAppName "Redbright"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Redbright"
#define MyAppExeName "Redbright.App.exe"

; Path to published binaries (self-contained or framework-dependent)
; Adjust if your publish path differs
#define PublishDir "..\\Redbright.App\\bin\\Release\\net8.0-windows\\win-x64\\publish"

[Setup]
AppId={{A4D2F2B8-4E8E-4E01-9A8B-6E7A7E2F7C3B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName}
UninstallDisplayName={#MyAppName}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputBaseFilename=Redbright-Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
; Installer icon (compile-time)
SetupIconFile=..\\assets\\icon_crop.ico
; Icon shown in Apps & Features / Uninstall (runtime)
UninstallDisplayIcon={app}\\{#MyAppExeName}

; Optional: use app icon for installer
; SetupIconFile={#PublishDir}\\assets\\icon_crop.ico

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
; Optionally start after install
Name: "startafterinstall"; Description: "Launch {#MyAppName} after setup"; Flags: unchecked

[Files]
Source: "{#PublishDir}\\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\\{#MyAppName}"; Filename: "{app}\\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\\{#MyAppName}"; Filename: "{app}\\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: 

[Run]
Filename: "{app}\\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent; Tasks: startafterinstall

[UninstallDelete]
; Clean up per-user settings on uninstall (optional)
Type: filesandordirs; Name: "{userappdata}\\Redbright"

