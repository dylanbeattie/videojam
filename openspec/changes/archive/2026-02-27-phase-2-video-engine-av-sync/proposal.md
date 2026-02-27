## Why

Phase 1 delivered a proven multi-stem audio pipeline. The application cannot fulfil its core promise — synchronised A/V playback for live performance — until video runs alongside that audio. Phase 2 adds LibVLC-backed video playback on the primary display and the synchronisation coordinator that fires audio and video within the required 10 ms window.

## What Changes

- **New** — `VlcDisplayWindow` fully implemented: borderless, topmost WPF window that hosts a LibVLC render surface and can switch between a fallback PNG and live video
- **New** — `VideoEngine` implemented: wraps LibVLC `LibVLC` and `MediaPlayer` instances, pre-buffers video, routes audio-muted playback to one display's HWND
- **New** — `SyncCoordinator` implemented: calls `AudioEngine.Play()` then immediately `VideoEngine.Play(timestamp)`, recording the Δt for diagnostic logging
- **New** — Phase 2 integration harness: extends the Phase 1 WPF harness with video file selection, `VlcDisplayWindow` creation, and `SyncCoordinator.Start()` wiring
- **Updated** — `VlcDisplayWindow.xaml` gains a `Border` host element for the VLC HWND surface (previously a `<!-- placeholder -->` comment)
- **Updated** — `VideoJam.csproj` gains a DPI-awareness app manifest (`app.manifest`) to ensure correct full-screen window positioning on high-DPI displays

## Capabilities

### New Capabilities

- `video-engine`: LibVLC-powered single-display video playback — loads an MP4 file, pre-buffers it, renders to a WPF window HWND, starts silent on cue, disposes cleanly
- `sync-coordinator`: Tight-sequence orchestrator that fires AudioEngine then VideoEngine in under 1 ms and logs the measured Δt

### Modified Capabilities

*(none — no existing spec-level behaviours are changing)*

## Impact

- **VideoJam/UI/VlcDisplayWindow.xaml (.cs)** — full implementation replacing the Phase 1 stub
- **VideoJam/Engine/VideoEngine.cs** — full implementation replacing the Phase 1 stub
- **VideoJam/Engine/SyncCoordinator.cs** — full implementation replacing the Phase 1 stub
- **VideoJam/UI/MainWindow.xaml (.cs)** — Phase 2 harness additions (video file picker, VlcDisplayWindow wiring)
- **VideoJam/VideoJam.csproj** — add `app.manifest` reference for DPI awareness
- **VideoJam/app.manifest** — new file; sets `PerMonitorV2` DPI awareness
- **Dependencies in use** — `LibVLCSharp` and `VideoLAN.LibVLC.Windows` (already in the project from Phase 1 setup, now activated)
- **No new NuGet packages** — all required packages are already referenced

## Non-goals for this phase

- Multi-display support (Phase 3)
- Display enumeration / `DisplayManager` (Phase 3)
- Show model or `.show` file persistence (Phase 4)
- Operator UI (Phase 5)
- Global hotkey hook (Phase 6)
- Pause / rewind state machine (Phase 6)

## Primary Technical Risk

The LibVLC pre-buffer trick — calling `MediaPlayer.Play()` then `Pause()` and seeking back to position 0 — is assumed to eliminate VLC's internal startup delay from the sync path. In practice, VLC's internal state after a pause-and-seek is non-deterministic: media loading is asynchronous and the `Paused` state event may not guarantee the decoder is truly primed. **This must be validated with real MP4 files early in task 2.2.** If it proves unreliable, the fallback is to open and hold the file, then apply a calibrated fixed-delay offset at `VideoEngine.Play()` time.
