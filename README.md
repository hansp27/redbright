<div align="center">
  <img src="assets/icon.png" alt="redbright-icon" width="80" height="80" style="margin-bottom: 16px;">
  
  <h1 style="margin: 0; font-size: 2.5rem; font-weight: bold; color: #1a1a1a;">
      Redbright
  </h1>
  
  <p style="margin: 8px 0 0 0; font-size: 1.2rem; color: #667; font-weight: 300;">
    An app that takes the strain away from your eyes.
  </p>
</div>

## Features

- Turn the screen red and black 
- Dim brightness the better way 


## Technical Features
- Tray icon with quick actions:
  - Toggle Red
  - Pause/Unpause Brightness
  - Toggle Red + Brightness
- Global hotkeys (configurable in-app) for all actions
- Starts minimized (optional) and remembers last settings

## Requirements

- Windows 10/11 (SDR recommended; HDR pipelines may ignore gamma ramps)
- .NET Desktop Runtime 8 (if running framework-dependent builds)

## Install

Download and run the installer produced under `installer/Output/Redbright-Setup.exe`, or build it yourself (see Build below).

The installer:
- Installs to Program Files (`C:\Program Files\Redbright`)
- Creates Start Menu and optional Desktop shortcuts
- Does not require elevation (PrivilegesRequired=lowest)

## Usage

- Launch Redbright. The main window shows:
  - Big “Turn Screen Red” button (toggles color only)
  - Brightness slider (software dimming)
  - “Pause brightness” checkbox (locks dimming to 100% and disables the slider)
  - Autostart and “start minimized to tray” checkboxes

- Tray icon menu:
  - “Toggle Red”
  - “Pause/Unpause Brightness”
  - “Toggle Red + Brightness”
  - “Show/Hide”, “Exit”

- Hotkeys:
  - Three rows in the window let you set (or clear) shortcuts by clicking the field and pressing the desired combo (e.g., Ctrl+Alt+R). The field click clears existing first, then captures.

Notes:
- Brightness slider affects software dimming regardless of color state.
- “Toggle Red + Brightness” pauses brightness at 100% when enabling red-only, and restores your previous brightness when disabling red-only

## Crash logs

- Global crash handling writes logs to:
  `%LocalAppData%\Redbright\logs\crash-YYYYMMDD-HHMMSS.txt`
- If the app crashes, you’ll see a message with the log path. Share the log to help diagnose.

## Build (Developer)

Prereqs:
- .NET 8 SDK
- Optional for installer: Inno Setup 6 (iscc.exe on PATH)

Build & run (Release):

```powershell
dotnet build Redbright.sln -c Release
dotnet run --project .\Redbright.App\Redbright.App.csproj
```

### Build Installer

From the `installer` folder:

```powershell
cd installer
.\build.ps1 -Restore   # first time or when NuGet restore is needed
# Next times (if restore already done):
.\build.ps1
```

Output: `installer\Output\Redbright-Setup.exe`

Build script steps:
- `dotnet publish` Redbright.App (single-file, self-contained by default, RID=win-x64)
- Compile Inno Setup script `installer\Redbright.iss` to generate the installer

If `iscc.exe` is not found:
- Install Inno Setup 6 and add its folder to PATH

## Known Limitations

- HDR: Some HDR pipelines ignore gamma ramp changes. Dimming/tint may not apply. 
- Exclusive fullscreen apps/games may bypass desktop gamma.
- Vendor calibration tools or GPU control panels can overwrite gamma ramps. The app re-applies on changes, but conflicts may exist.

## Privacy

- The app does not collect or transmit data. Logs are written locally only when a crash occurs.

## License

Apache-2.0

## Thank-yous 

Thanks to Iris for heavy inspiration.