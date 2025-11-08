## Phase 1 Plan — Windows Screen Tint Control (reduce blue light)

### Goals
- Build a Windows app that can adjust the global screen tint, prioritizing reduction of the blue channel.
- Survey all viable APIs and approaches; identify trade-offs, constraints, and fallback strategies.
- Choose a primary implementation language and define a modular architecture to test multiple strategies.

### Recommended language
**C# (.NET 8)**
- Excellent Windows interop via P/Invoke for Win32 (`gdi32`, `dxva2`, `user32`) and COM.
- Mature desktop UI stacks (WPF/WinUI 3) and background service capability.
- Healthy ecosystem and wrappers for vendor SDKs exist or are straightforward to P/Invoke.
- Fast iteration for prototyping, good tooling on Windows.

Alternatives (when and why)
- C++: Most direct access to native SDKs (DXGI, vendor APIs). Prefer for ultra-low level or if we later need drivers/hooks. Higher engineering cost.
- Rust: Good FFI; fewer ready-made Windows display control examples and vendor wrappers.
- Python: Possible via `ctypes` but poorer distribution story and perf for GUI/system integration.

Conclusion: Start in C# for Phase 1. If we hit hard limits that demand native-only access, we can isolate those in a C++ helper DLL and call from C#.

### API and approach landscape
1) System gamma ramp (global LUT)
   - API: `SetDeviceGammaRamp` (GDI, `gdi32.dll`).
   - Scope: Affects the entire desktop pipeline for SDR displays.
   - Pros: Simple, widely supported, no admin needed, per-adapter HDC.
   - Cons: Can be overridden by calibration loaders, GPU control panels, games; limited or ignored with HDR; precision is LUT-only (no arbitrary matrix on all systems).

2) DXGI/D3D exclusive fullscreen gamma control
   - API: `IDXGIOutput::SetGammaControl` (requires owning a fullscreen swap chain).
   - Scope: Only while your app owns fullscreen output.
   - Pros: Precise control for that session.
   - Cons: Not global; unusable for general desktop tinting.

3) Windows Color System / ICM profiles
   - APIs: WCS (`WcsSetDefaultColorProfile`, `InstallColorProfile`, etc.).
   - Scope: System color management with ICC/WCS profiles, calibration loader applies VCGT.
   - Pros: Standards-based; persistent.
   - Cons: Not designed for real-time adjustments; can require elevation and profile installs; UX friction; HDR pathways complicate behavior.

4) Monitor control via DDC/CI (per-physical display)
   - APIs: High-Level Monitor Configuration Functions in `Dxva2.dll` (`GetPhysicalMonitorsFromHMONITOR`, `Get/SetVCPFeature`).
   - VCP codes of interest (monitor-dependent):
     - 0x18 Red Gain, 0x1A Green Gain, 0x1C Blue Gain
     - 0x16 Brightness, 0x12 Contrast, 0x14 Color Preset/Temperature
   - Pros: Hardware-level change, persists on the monitor; independent of OS gamma; HDR-friendly.
   - Cons: Only works if monitor exposes DDC/CI and supports those VCP codes; laptop internal panels often unsupported; vendor variance.

5) GPU vendor SDKs (per-adapter)
   - NVIDIA NVAPI: Color controls, gamma ramps, digital vibrance; well-documented, 64-bit DLLs.
   - AMD ADL/AGS: Display color adjustments available; licensing and versioning to manage.
   - Intel: Public options are limited/in flux; less reliable for display color control.
   - Pros: Deep control, may work in HDR where GDI gamma is ignored; per-head adjustments.
   - Cons: Vendor-specific binaries, EULAs, maintenance per vendor, detection logic needed.

6) Desktop overlay tint (composition layer)
   - Approach: Create an always-on-top, click-through, borderless, per-monitor layered window; apply a color matrix/shader (e.g., WinUI + Composition, Direct2D/Direct3D, or ShaderEffect) to simulate tint.
   - Pros: Works even when gamma is locked; vendor-agnostic; flexible effects.
   - Cons: Not truly global (doesn’t affect protected video/fullscreen exclusive); potential performance impact; multi-monitor handling; HDR tone-mapping interactions.

7) OS features
   - Windows Night Light: No supported public API to control; OS-managed.
   - DWM colorization APIs affect UI chrome only, not display tint.

### Comparison at a glance
| Method | Global | HDR | Multi-monitor | Admin | Vendor-free | Persistence |
|---|---|---|---|---|---|---|
| GDI Gamma Ramp | Yes (SDR) | Weak | Yes (per HDC) | No | Yes | No (can be overridden) |
| DXGI Fullscreen | No | N/A | N/A | No | Yes | Session only |
| WCS/ICM Profiles | Yes | Mixed | Yes | Sometimes | Yes | Yes |
| DDC/CI (Dxva2) | Yes (per monitor) | Good | Yes | No | Mostly | Yes (monitor) |
| NVAPI/ADL | Yes (per adapter/output) | Good | Yes | No | No | Session/driver-dependent |
| Overlay | Visual only | Mixed | Yes | No | Yes | While running |

### Proposed architecture (Phase 1)
- Core engine (C# class library) with a strategy interface: `IDisplayTintStrategy`.
- Strategies implemented incrementally:
  1. `GammaRampStrategy` (GDI): fast baseline for SDR.
  2. `DdcCiStrategy` (Dxva2): adjust RGB gains when supported.
  3. `NvapiStrategy` and `AmdAdlStrategy`: optional plugins loaded if vendor DLLs present.
  4. `OverlayStrategy`: fallback when others fail or are blocked.
- Capability detection and priority order (configurable), per-display handling, and safe rollback.
- Small CLI to set blue reduction percentage, list displays, and show which strategy is active per display.

### Phase 1 deliverables
1) Language/tooling setup: .NET 8 solution; core library + CLI app.
2) Gamma ramp prototype: reduce blue channel with adjustable strength; multi-monitor aware.
3) DDC/CI prototype: enumerate physical monitors; set RGB gains where available; safe bounds and restore.
4) Vendor probes: detect NVIDIA/AMD presence; load SDKs if available (stubs acceptable in Phase 1).
5) Overlay prototype: per-monitor layered window with tint; click-through, minimal perf impact.
6) Basic settings persistence and restore-on-exit.
7) Test matrix across SDR/HDR, internal vs external displays, multiple GPUs.

### Risks and constraints
- HDR pipelines may ignore gamma LUT; rely on DDC/CI or vendor SDKs in that case.
- Conflicts with calibration software or GPU control panels can reset gamma or VCP values.
- DDC/CI support varies; some monitors clamp or step values, some block during DPMS.
- Vendor SDK redistribution and licensing; 64-bit process alignment.

### Next steps (after this document)
- Initialize repository structure and .NET solution.
- Implement `GammaRampStrategy` and CLI command: `tint --blue-reduction 30`.
- Add DDC/CI enumeration and capability check; report which VCP codes are supported.
- Design a simple persistence mechanism (JSON) and safe restore on crash.

### Clarifying questions for you
1) Do you require the tint to work under HDR and with protected video playback?
2) Are you targeting internal laptop panels, external monitors, or both?
3) Is vendor-specific support (NVIDIA/AMD) acceptable as an optional enhancer?
4) Should changes persist after app exit or always restore to original?
5) Any constraints around admin rights, background service, or enterprise deployment?
6) Do you prefer a CLI first, or should we start with a small GUI?



