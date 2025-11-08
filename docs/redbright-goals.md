## Redbright — Goals and Scope

### Objective
Build a Windows desktop program in C# that:
- Shows only the red channel (green and blue off) across the entire desktop.
- Provides a brightness slider that reduces perceived brightness by darkening colors (software dimming), while keeping the system’s hardware brightness at 100% to avoid lowering the PWM flicker rate.

The program should be simple: one large “Turn Screen Red” button, and a brightness slider below it.

### Primary Requirements
- Red-only mode: Set green and blue output to zero; preserve red channel response.
- Software brightness control: Reduce overall intensity by applying a gamma/LUT scaling to the red channel (and any residual channels when Red-only is off), not by using OS or monitor “brightness” controls.
- Keep system brightness at 100%: Do not change Windows brightness or monitor VCP brightness; only manipulate color output.
- Toggle and restore: Toggling off should restore the original color response safely.
- Multi-monitor baseline: Apply globally across all connected SDR displays. Phase 1 can apply the same effect to all; per-display controls are a later enhancement.
- Persistence: Remember last used settings (enabled state, brightness level). Restore them on app start, with a safe bypass.
- Safety escape hatch: Provide a quick way to revert (e.g., a keyboard shortcut and a timed auto-restore option after first enable).

### Non-Goals (Phase 1)
- HDR correctness (Windows HDR paths may ignore/alter gamma ramps) — we will detect HDR and fall back or warn.
- Per-application color control.
- Vendor-specific SDKs (NVAPI/ADL) or DDC/CI color gains — possible Phase 2+ enhancers.
- Full-screen exclusive apps that bypass the desktop pipeline.

### User Experience
- Main window:
  - One large button: “Turn Screen Red” / “Restore Normal Colors” (toggle).
  - Brightness slider beneath (0–100%). 100% = no dimming; lower values darken via software.
  - Clear labels indicating hardware brightness remains at 100% to maintain high PWM frequency.
- Accessibility:
  - Large hit targets, keyboard navigation, optional global hotkey to toggle and to restore defaults.
- Tray presence:
  - Optional: tray icon to quickly toggle and adjust brightness without opening the main window.

### Technical Approach
- API: Use the same API as the working script — GDI `SetDeviceGammaRamp` — to apply a per-channel 256-entry LUT:
  - Set green/blue LUTs to zero to achieve Red-only output.
  - Apply brightness as a scalar and/or gamma curve to the red LUT to reduce perceived luminance.
- Process:
  1) Capture and store the original gamma ramp on startup (per display device context).
  2) Build and apply a new ramp where:
     - `Red[i] = scale * f(i)`; `Green[i] = 0`; `Blue[i] = 0` for Red-only mode.
     - For “normal colors” mode (future toggle), we may re-enable G/B and still apply software dim.
  3) On exit or toggle-off, restore the original ramp(s) reliably.
- Multi-monitor handling:
  - Enumerate display devices and obtain an HDC per adapter where supported; apply the same ramp by default.
  - Log and gracefully degrade if a display path rejects gamma ramp (e.g., HDR).
- Conflicts and ordering:
  - Calibration loaders or GPU control panels might overwrite the ramp. We will re-apply on changes or expose a “sticky” mode (with care).

### Brightness Algorithm (Software Dimming)
- Target behavior: perceptually smooth dimming without banding or crushing blacks.
- Options:
  - Linear scalar on LUT (simple, fast): `R'[i] = clamp(scale * R[i])`.
  - Gamma-based curve for perceptual response: `R'[i] = clamp((R[i]/65535)^(gamma) * 65535)`, where `gamma > 1` darkens.
  - Hybrid: combine scalar + mild gamma for low-end control.
- Phase 1 default: scalar-based dimming with optional low-end lift to reduce near-black crush.

### Platform and Stack
- Language: C# (.NET 8)
- UI: WPF or WinUI 3 (pick one in design step; WPF offers mature P/Invoke samples and easy deployment).
- Interop: P/Invoke to `gdi32.dll` (`SetDeviceGammaRamp`) and `user32.dll` (`GetDC`/`ReleaseDC`), mirroring the working script.
- Packaging: Single-file or MSIX; x64 target.

### Telemetry and Diagnostics (optional, dev-only)
- Log display enumeration, ramp application success/failure, HDR detection, and restoration actions.
- Export current LUTs for debugging.

### Risks and Constraints
- HDR pipelines and some drivers may ignore or remap gamma ramps.
- Calibration loaders (ICM/WCS) and GPU tools may override ramps; coordinate or re-apply.
- Full range vs limited range displays and ICC profiles can alter perceived results.
- Laptops/internal panels might behave differently than external monitors via dGPU/iGPU muxing.

### Milestones
1) Prototype engine: capture/restore gamma ramp; apply Red-only LUT via `SetDeviceGammaRamp`.
2) Brightness slider: implement scalar dimming on the red channel; live updates.
3) UI shell: big toggle button + slider; tray optional.
4) Multi-display pass: apply to all SDR displays; detect HDR and warn/fallback.
5) Persistence and safety: save settings; restore on exit; panic hotkey; safe-mode bypass at startup.
6) Packaging: produce a distributable build.

### Out-of-Scope (for now)
- Vendor SDK integration (NVAPI/ADL) for deeper control.
- DDC/CI manipulation of RGB gains and monitor brightness.
- Per-app exclusion lists or overlay-based approaches.

### Next Actions
1) Decide UI stack (WPF vs WinUI 3) and window layout.
2) Set up .NET solution with core interop for gamma ramp.
3) Implement Red-only toggle and software brightness slider.
4) Add persistence and safe restore.
5) Test across multiple displays; document HDR behavior and guidance.


