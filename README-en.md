<div align="center">

<img src="Assets/Square150x150Logo.png" alt="Windows AI VSR Demo logo" width="96" />

# Windows AI Video Super Resolution Demo

A WinUI 3 desktop demo that uses `Microsoft.Windows.AI.Video.VideoScaler` to apply AI super resolution to video and save the result as an MP4 file.

[![Windows 11](https://img.shields.io/badge/Windows_11-25H2-0078D4?logo=windows11&logoColor=white)](https://www.microsoft.com/windows/windows-11)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![WinUI 3](https://img.shields.io/badge/WinUI-3-0078D4)](https://learn.microsoft.com/windows/apps/winui/winui3/)
[![Windows App SDK](https://img.shields.io/badge/Windows_App_SDK-2.3.1-0078D4)](https://learn.microsoft.com/windows/apps/windows-app-sdk/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

**English** | [日本語](README.md)

</div>

> [!IMPORTANT]
> There is no UI for selecting CPU or NPU, and no API for forcing CPU execution on a Copilot+ PC. Windows selects the execution hardware automatically.

## Execution hardware

| Environment     | Execution provider                      |
| --------------- | --------------------------------------- |
| Copilot+ PC     | NPU                                     |
| Non-Copilot+ PC | CPU                                     |
| GPU             | Not supported by Video Super Resolution |

This project therefore demonstrates the automatically selected CPU path on a non-Copilot+ PC; it does not force CPU execution.

## Features

- Select and preview MP4, MOV, M4V, and WMV files
- Frame-synchronized, real-time comparison of the original and VSR video
- Dedicated side-by-side comparison view that uses the full window width
- 1.0x to 4.0x zoom adjustment
- Shared zoom centered on a point selected in either video
- VSR availability and recommended CPU specification checks
- Selectable output scaling from 1.25x to 4.0x in 0.25x increments for both real-time comparison and MP4 export, adjusted to the 480p-to-4K range
- Real-time VSR through frame server mode and MP4 export through `MediaComposition`
- H.264 + AAC MP4 output
- Progress, cancellation, output preview, and open-output-folder actions

## Project structure

```text
windows-ai-vsr-cpu-demo/
├─ WindowsAiVsrCpuDemo.csproj       WinUI 3 application
├─ MainWindow.xaml(.cs)             UI, file selection, progress, and previews
├─ Services/
│  └─ VideoProcessingService.cs     VSR readiness and MediaComposition export
└─ VsrVideoEffect/
   ├─ VsrVideoEffect.csproj         C#/WinRT component
   └─ VideoSuperResolutionEffect.cs IBasicVideoEffect + VideoScaler.Scale
```

Because `VideoEffectDefinition` uses Windows Runtime activation, `Package.appxmanifest` registers `WinRT.Host.dll` and `VsrVideoEffect.VideoSuperResolutionEffect`. The manifest fixes `MaxVersionTested` at `10.0.26226.0` and disables MSBuild replacement so the Windows AI capability is not disabled as an older compatibility target.

## Requirements

- Windows 11 version 25H2, **build 10.0.26200.7309 or later**
- Developer Mode
- Visual Studio 2022 or 2026 with:
  - Desktop development with C++
  - Windows application development components
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- [`winapp` CLI](https://learn.microsoft.com/windows/apps/windows-app-sdk/winapp-cli/)
- A VSR-capable CPU or a Copilot+ PC

Recommended CPU specifications:

- 4 or more physical cores
- Base clock of 3 GHz or higher
- 32 MB or more L3 cache

> [!NOTE]
> These CPU specifications are recommendations, not hard requirements. Lower-spec CPUs may reduce processing speed and real-time performance.

## Windows App SDK

This project pins `Microsoft.WindowsAppSDK` to **2.3.1 stable**.

- The VSR API became stable in Windows App SDK 2.2.
- Version 2.3.1 adds NPU detection fixes, CPU support, and VSR performance improvements.
- No Experimental package or Limited Access Feature token is required.
- The C#/WinRT component authoring tooling used to host `IBasicVideoEffect` from WinUI 3 is documented by Microsoft as preview tooling.

## Build and run

After obtaining the repository, run the following commands from the **repository root**. Every command uses relative paths and is independent of the clone location.

```powershell
dotnet restore .\WindowsAiVsrCpuDemo.csproj -p:Platform=x64
.\BuildAndRun.ps1 .\WindowsAiVsrCpuDemo.csproj
```

On an ARM64 machine, `BuildAndRun.ps1` detects the platform automatically. To select it explicitly:

```powershell
.\BuildAndRun.ps1 .\WindowsAiVsrCpuDemo.csproj /p:Platform=ARM64
```

To build without launching the app:

```powershell
.\BuildAndRun.ps1 .\WindowsAiVsrCpuDemo.csproj -SkipRun
```

## Usage

1. Select **Choose a video**.
2. Select **Play comparison** to inspect the original and VSR output.
3. Optionally select an **output destination**.
4. Select **Start VSR processing**.
5. When processing finishes, inspect the output preview or open the output folder.

## Real-time comparison mode

- The left side displays the original input video.
- `MediaPlayer.IsVideoFrameServerEnabled` obtains native-resolution NV12 frames.
- The original BGRA frame and `VideoScaler.Scale` VSR output are generated from the same input frame, keeping both sides frame-aligned.
- `MediaTimelineController` controls playback, pause, restart, and looping.
- Audio is played through the single frame-server media player.
- Real-time output is limited to the officially supported maximum of 1440p.
- Input and AI output resolutions, successful frames, and skipped frames are displayed below the comparison.
- Zoom is adjustable from 1.0x to 4.0x in 0.25 increments.
- Clicking either video uses the selected coordinate as the shared zoom center.
- Clicking at 1.0x automatically zooms to 2.0x.
- Frame drops may occur depending on CPU performance and input resolution.

> [!TIP]
> The comparison view is intended for demonstration. Inspect the saved MP4 for final quality.

## API and processing pipeline

1. Register certified execution providers with `ExecutionProviderCatalog.EnsureAndRegisterCertifiedAsync`.
2. Check availability with `VideoScaler.GetReadyState` / `EnsureReadyAsync`.
3. Read input resolution, FPS, and duration from `MediaClip`.
4. Obtain native-resolution NV12 surfaces from `VideoFrameAvailable` for real-time comparison.
5. Generate and draw the original and VSR BGRA frames from the same input.
6. Use `MediaComposition` and the C#/WinRT `IBasicVideoEffect` for MP4 export.
7. Apply `VideoScaler.Scale` to Direct3D surfaces inside `ProcessFrame`.
8. Save an H.264 + AAC MP4 with `RenderToFileAsync`.

## Supported range and limitations

| Item              | Supported range                                |
| ----------------- | ---------------------------------------------- |
| Input resolution  | 240p to 1440p                                  |
| Output resolution | 480p to 1440p; up to 4K for offline processing |
| Frame rate        | 15 to 60 FPS                                   |
| Direct3D input    | NV12                                           |
| Direct3D output   | BGRA or NV12                                   |

- VSR is optimized for talking-head video, video calls, social video, and short-form video.
- CPU processing may take significantly longer than the video's duration.
- Real-time processing above 1440p is demanding and may increase dropped frames.
- Export fails if the OS does not provide H.264 / AAC codecs.
- Rotation metadata, HDR, subtitles, multiple audio tracks, and DRM-protected video are out of scope.
- Files that the media pipeline cannot decode into a supported format cannot be processed.

> [!WARNING]
> AI super resolution estimates details that were not present in the input. Do not use it for medical, forensic, identity verification, or other fidelity-critical purposes.

## Official documentation

- [Get Started with AI Video Super Resolution](https://learn.microsoft.com/windows/ai/apis/video-super-resolution)
- [Windows App SDK 2.0 release notes](https://learn.microsoft.com/windows/apps/windows-app-sdk/release-notes/windows-app-sdk-2-0)
- [Custom video effects for WinUI 3](https://learn.microsoft.com/windows/apps/develop/media-authoring-processing/custom-video-effects)
- [Media compositions and editing](https://learn.microsoft.com/windows/apps/develop/media-authoring-processing/media-compositions-and-editing)

This project is available under the [MIT License](LICENSE). Copyright (c) 2026 tokawa-ms.
