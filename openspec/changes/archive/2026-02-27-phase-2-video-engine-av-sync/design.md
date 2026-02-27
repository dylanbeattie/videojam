## Context

Phase 1 delivered a proven NAudio/WASAPI audio pipeline capable of playing multiple stems in sample-accurate synchronisation. The existing `VideoEngine`, `SyncCoordinator`, and `VlcDisplayWindow` are compile-only stubs. Phase 2 breathes life into these stubs, adding LibVLC-backed H.264 video playback on the primary display and the tight-sequence coordinator that fires audio and video together within the 10 ms budget.

The tech stack already includes `LibVLCSharp` and `VideoLAN.LibVLC.Windows` (added in Phase 1 setup); no new packages are needed. The primary unknowns are behavioural: how reliably does LibVLC's pre-buffer trick eliminate startup latency, and does WPF's DPI virtualisation misplace the `VlcDisplayWindow` on the primary display.

## Goals / Non-Goals

**Goals:**
- A borderless, topmost `VlcDisplayWindow` covers the primary display with no visible chrome
- An MP4/H.264 file plays in that window with hardware-accelerated decoding
- LibVLC's internal audio output is unconditionally silenced — all audio runs through NAudio
- Audio starts, then the video signal is dispatched; measured Δt is logged
- When no video is assigned, the window shows a fallback PNG (black if none provided)
- The Phase 2 harness lets a developer verify A/V sync manually against a known-good test set

**Non-Goals:**
- Multi-display enumeration and routing (Phase 3 — `DisplayManager`)
- Pause, seek, or rewind on the video stream (Phase 6)
- Fallback PNG assignment through a UI (Phase 5/6)
- Any global hotkey or performance-mode state machine (Phase 6)

## Decisions

### Decision 1 — LibVLC HWND rendering over WPF `MediaElement`

WPF's built-in `MediaElement` cannot be silenced at the audio-output level without disabling the media clock, conflicts with WASAPI exclusive-mode scenarios, and does not expose a reliable "pre-buffered and ready" event. LibVLC renders directly into a Win32 HWND (`MediaPlayer.Hwnd`), giving complete audio control via the `--no-audio` VLC option and a well-documented pause-and-seek pre-buffer mechanism.

*Alternative considered:* `Windows.Media.Playback.MediaPlayer` (WinRT) — requires WinUI interop plumbing that would add disproportionate complexity. Rejected.

### Decision 2 — Pre-buffer via Play → Pause → Seek(0)

LibVLC's decode pipeline does not start until `Play()` is called. A "play, immediately pause, seek to 0" sequence forces the decoder to prime at least one frame before the GO signal is fired. This eliminates VLC's cold-start latency (~150–400 ms on typical hardware) from the sync path, leaving only the negligible `Play()` dispatch time.

*Caveat:* The pre-buffer is an async operation on VLC's internal thread. The implementation will wait for the `Paused` state event (with a 2-second timeout) before considering the load complete and enabling the GO button. If the wait times out, the harness will surface a warning and allow a degraded start.

*Fallback:* If the pre-buffer proves unreliable across test files, the implementation will skip it and instead record a fixed empirical offset (e.g. 180 ms) at `VideoEngine.Play()` time to compensate. This fallback is a Phase 2 decision — it must be resolved before Phase 6 ships.

### Decision 3 — `LibVLC` singleton, one `MediaPlayer` per display slot

A single `LibVLC` instance is created once (with `--no-audio --no-osd` options) and shared across all `MediaPlayer` instances within `VideoEngine`. `MediaPlayer` instances are created per display slot at `VideoEngine.Load()` time and disposed at `VideoEngine.Stop()`. This mirrors the recommended LibVLCSharp usage pattern.

*Why not re-use MediaPlayer across songs?* `MediaPlayer.Stop()` followed by a new `Media` assignment leaves the player in an undefined internal state across VLC versions. Disposing and recreating is the only reliable reset path.

### Decision 4 — `SyncCoordinator` is a stateless fire sequence, not a persistent coordinator

The coordinator's sole job is to call `AudioEngine.Play()`, capture the timestamp, then call `VideoEngine.Play(timestamp)`. It holds no state between calls and is not responsible for stopping, rewinding, or monitoring progress. This keeps the class simple and testable.

### Decision 5 — DPI awareness via app manifest (`PerMonitorV2`)

Without a DPI manifest, WPF virtualises window coordinates, causing `VlcDisplayWindow` to position relative to a scaled coordinate space. On a 150 % DPI primary display, a "full-screen" window would cover only part of the physical screen. `PerMonitorV2` DPI awareness makes `SystemParameters.WorkArea` and `Screen.Bounds` return physical pixel values, and requires the window position calculation to be done in DIPs using the monitor's DPI scale factor.

### Decision 6 — VLC audio disabled unconditionally via VLC option, not via NAudio

Setting `--no-audio` at the `LibVLC` constructor level ensures VLC never opens an audio device, not even briefly during pre-buffer. This is preferable to routing VLC's audio to a null sink or attempting to mute a VLC audio track, both of which are fragile and version-dependent.

## Risks / Trade-offs

| Risk | Mitigation |
|------|-----------|
| Pre-buffer async state is non-deterministic — `Paused` event fires before decode is complete on some files/hardware | Implement a short warm-up delay (one frame period) after receiving the `Paused` event before clearing the loading gate |
| LibVLC HWND integration mispositions the render surface on high-DPI displays | Apply DPI manifest early; test on HiDPI hardware in the Phase 2 harness before any Phase 3 work |
| VLC native plugin folder not copied to publish output by the NuGet package | Verify `VideoLAN.LibVLC.Windows` copy behaviour in publish output immediately; add explicit `<ItemGroup>` copy task if needed |
| Audio bleed from video file via Windows audio mixer (e.g. VLC ignores `--no-audio` on some builds) | Verify silence in the harness with a video file that has loud embedded audio; use audio analyser if uncertain |
| Disposing `LibVLC` while a `MediaPlayer` is still active can crash the VLC native thread | Always dispose `MediaPlayer` instances before the `LibVLC` instance; enforce via disposal order in `VideoEngine.Dispose()` |

## Migration Plan

Phase 2 replaces stub classes with full implementations in-place — no database migrations, no schema changes, no existing behaviour modified. The Phase 1 integration harness is extended (not replaced) to add the video path.

The only deploy-time concern is the DPI manifest addition: it changes window coordinate calculations. The primary display window should be manually verified to cover the screen correctly on both standard (100 %) and high-DPI (125 %, 150 %, 200 %) configurations.

## Open Questions

| # | Question | Resolution Deadline |
|---|----------|---------------------|
| 1 | Does the Play → Pause → Seek(0) pre-buffer reliably prime the decoder for the target MP4 test set? | Resolved during task 2.2 implementation |
| 2 | Does `VideoLAN.LibVLC.Windows` 3.x copy VLC plugin DLLs correctly under `dotnet publish -r win-x64`? | Resolved when building the Phase 2 harness (task 2.4) |
| 3 | Is the `PerMonitorV2` DPI manifest sufficient for correct window placement on the primary display, or does WPF also need `UseLayoutRounding` / physical pixel adjustments? | Resolved during task 2.1 |
