## 1. DPI Awareness & Project Prep

- [x] 1.1 Add `app.manifest` to `VideoJam/` with `<dpiAware>True/PM</dpiAware>` and `<dpiAwareness>PerMonitorV2</dpiAwareness>` declarations
- [x] 1.2 Reference the manifest in `VideoJam.csproj` via `<ApplicationManifest>app.manifest</ApplicationManifest>`
- [x] 1.3 Verify the solution still builds with zero errors and zero warnings after the manifest addition
- [x] 1.4 Confirm `VideoLAN.LibVLC.Windows` copies native VLC DLLs and the `plugins/` folder to the build output — run a `dotnet build` and inspect `bin/Debug/net10.0-windows/` — add an explicit `<ItemGroup>` copy task to the `.csproj` if the folder is missing

## 2. VlcDisplayWindow — Full Implementation

- [x] 2.1 Update `VlcDisplayWindow.xaml`: add a named `<Border x:Name="VlcHost" Background="Black" />` element behind the fallback `Image` so VLC has a host panel to render into; keep `Background="Black"` on the root Grid
- [x] 2.2 Update `VlcDisplayWindow.xaml.cs`: add `SetBounds(double left, double top, double width, double height)` method to position and size the window in device-independent units; call it before `Show()` in the harness
- [x] 2.3 Verify `ShowFallback(BitmapImage image)` sets `FallbackImage.Source` and makes the image `Visibility.Visible`; `ShowVideo()` sets it to `Visibility.Hidden`; add XML doc comments on all public members
- [x] 2.4 Manual smoke test: window positioning and DPI verified via the Play flow — `VlcDisplayWindow` covers the primary display correctly at the current DPI setting. `ShowFallback(BitmapImage)` with a real PNG is not wired up in the Phase 2 harness; deferred to Phase 4 when `ShowFileService` loads fallback images from the `.show` file.

## 3. VideoEngine — Single Display

- [x] 3.1 Add `ILogger<VideoEngine>` constructor parameter and store it; add named constants for the pre-buffer timeout (`PreBufferTimeoutMs = 2000`) and VLC options (`NoAudio = "--no-audio"`, `NoOsd = "--no-osd"`)
- [x] 3.2 In `VideoEngine` constructor, instantiate a shared `LibVLC` with options `["--no-audio", "--no-osd"]` and store it; instantiate a `ILogger<VideoEngine>`
- [x] 3.3 Implement `VideoEngine.Load(SongManifest manifest, int displayIndex, VlcDisplayWindow window)` — find the first `VideoFileManifest` in `manifest.VideoFiles` where `DisplayIndex == displayIndex`; if none found, return without error
- [x] 3.4 In `Load()`: create a `MediaPlayer` from the shared `LibVLC`; set `MediaPlayer.Hwnd = window.Hwnd`; create a `Media` from the file path; assign `MediaPlayer.Media = media`; store the player and window reference
- [x] 3.5 In `Load()`: execute the pre-buffer sequence — call `MediaPlayer.Play()`; use a `TaskCompletionSource<bool>` with a 2-second timeout wired to `MediaPlayer.Paused` event; if the event fires, seek to 0 and call `window.ShowVideo()`; if timeout, log a `Warning` and return
- [x] 3.6 Implement `VideoEngine.Play(long audioStartTimestamp)` — call `MediaPlayer.Play()` on all active players in sequence; compute elapsed ticks since `audioStartTimestamp` using `Stopwatch.GetTimestamp()` and `Stopwatch.Frequency`; log at `Debug` level
- [x] 3.7 Implement `VideoEngine.Stop()` — call `MediaPlayer.Stop()` on all active players; dispose each `MediaPlayer`; clear the internal list; call `window.ShowFallback()` (with current fallback image or null) for every managed window
- [x] 3.8 Implement `VideoEngine.Dispose()` — call `Stop()` if not already stopped; dispose the shared `LibVLC` instance last; guard against double-dispose with a `bool _disposed` flag; add XML doc comments on all public members

## 4. SyncCoordinator — Implementation

- [x] 4.1 Add `ILogger<SyncCoordinator>` constructor parameter and store it; add named constant `AvDispatchLabel = "A/V dispatch Δt"`
- [x] 4.2 Implement `SyncCoordinator.Start(AudioEngine audio, VideoEngine video)` — call `audio.Play()` and capture `t_start`; immediately call `video.Play(t_start)`; capture `t_end = Stopwatch.GetTimestamp()`; compute Δt in milliseconds using `Stopwatch.Frequency`; log at `Debug` level with the label and value
- [x] 4.3 Verify `SyncCoordinator` holds no fields other than the logger — no song state, no timers, no event subscriptions; add XML doc comments on all public members

## 5. Phase 2 Integration Harness

- [x] 5.1 Extend `MainWindow.xaml` with a "Load Video" button and a file path label for the selected video file
- [x] 5.2 In `MainWindow.xaml.cs`, on "Load Video" click: open a file picker filtered to `*.mp4`; store the selected path
- [x] 5.3 On "Load" button click: after `AudioEngine.Load()`, create one `VlcDisplayWindow` for the primary display (`Screen.PrimaryScreen`); compute its bounds (physical pixels ÷ DPI scale); set bounds and show it; call `VideoEngine.Load()` with the manifest, display index 0, and the window
- [x] 5.4 On "Play" button click: call `SyncCoordinator.Start(audioEngine, videoEngine)` instead of (or in addition to) the direct `AudioEngine.Play()` call from Phase 1
- [x] 5.5 On "Stop" button click: call `AudioEngine.Stop()` and `VideoEngine.Stop()`; ensure `VlcDisplayWindow` returns to fallback state
- [x] 5.6 Wire `AudioEngine.PlaybackEnded` event: when fired, call `VideoEngine.Stop()` and update the status label to "Stopped"
- [x] 5.7 Manual A/V sync verification: play a known-good test set (at least one MP4 with a visible frame-count overlay or audio cue); confirm audio and video start together with no perceptible offset; check the Debug log for the Δt value
- [x] 5.8 Audio isolation test: play a video file that has loud embedded audio; confirm no audio bleed from VLC through the Windows audio mixer — only NAudio stems are audible
