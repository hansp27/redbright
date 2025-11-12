; Inno Setup script for Redbright
; Requires Inno Setup 6.x (iscc.exe)

#define MyAppName "Redbright"
#define MyAppVersion "1.2.3"
#define MyAppPublisher "Redbright"
#define MyAppExeName "Redbright.App.exe"

; Architecture tag (can be overridden via iscc.exe /DMyArch=...)
#ifndef MyArch
#define MyArch "x64"
#endif

; Path to published binaries (self-contained or framework-dependent)
; Adjust if your publish path differs
#define PublishDir "..\\Redbright.App\\bin\\Release\\net8.0-windows\\win-x64\\publish"

[Setup]
AppId={{A4D2F2B8-4E8E-4E01-9A8B-6E7A7E2F7C3B}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName}
UninstallDisplayName={#MyAppName}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; Output file naming (includes version and arch)
; Example: Redbright-1.1.0-x64-Setup.exe
OutputBaseFilename={#MyAppName}-{#MyAppVersion}-{#MyArch}-Setup
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

[Files]
Source: "{#PublishDir}\\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\\{#MyAppName}"; Filename: "{app}\\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\\{#MyAppName}"; Filename: "{app}\\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: 
[Run]
Filename: "{app}\\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent; Parameters: "--force-show"

[UninstallDelete]
; Clean up per-user settings on uninstall (optional)
Type: filesandordirs; Name: "{userappdata}\\Redbright"

[Code]
function PreviousInstallExists: Boolean;
var
  keyName: string;
begin
  keyName := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\' +
             ExpandConstant('{#SetupSetting("AppId")}') + '_is1';
  Result :=
    RegKeyExists(HKLM, keyName) or
    RegKeyExists(HKCU, keyName)
{$IFDEF WIN64}
    or RegKeyExists(HKLM64, keyName) or
    RegKeyExists(HKCU64, keyName)
{$ENDIF}
    ;
end;

procedure InitializeWizard;
begin
  if PreviousInstallExists then
  begin
    MsgBox('An existing installation of ' + ExpandConstant('{#MyAppName}') + ' was detected.'#13#10#13#10 +
           'If you continue, it will be overwritten. Your settings will be preserved.',
           mbInformation, MB_OK);
  end;
end;

