## ADDED Requirements

### Requirement: VlcDisplayWindow covers one physical display

A `VlcDisplayWindow` SHALL be a borderless, topmost WPF `Window` that fills exactly one physical display.
- It SHALL have `WindowStyle="None"`, `ResizeMode="NoResize"`, and `Topmost="True"`.
- It SHALL have a black `Background` so unrendered areas do not show system chrome.
- Its position and size SHALL be set in device-independent units derived from the physical screen bounds divided by the monitor's DPI scale factor.
- After the window is `Loaded`, it SHALL expose its Win32 HWND via `IntPtr Hwnd`.

#### Scenario: Window covers primary display at 100% DPI
- **WHEN** a `VlcDisplayWindow` is positioned and shown for the primary display at 100% DPI
- **THEN** the window covers the full screen with no visible taskbar, borders, or chrome

#### Scenario: Window covers primary display at 150% DPI
- **WHEN** a `VlcDisplayWindow` is positioned and shown for the primary display at 150% DPI (HiDPI)
- **THEN** the window covers the full physical screen without undershooting or overshooting

#### Scenario: HWND is available after Loaded
- **WHEN** the `VlcDisplayWindow.Loaded` event has fired
- **THEN** `VlcDisplayWindow.Hwnd` is a non-zero, valid Win32 window handle

---

### Requirement: VlcDisplayWindow shows fallback PNG or video surface

A `VlcDisplayWindow` SHALL support two display states: **Fallback** (showing a static PNG) and **Video** (showing the LibVLC render surface).

- `ShowFallback(BitmapImage image)` SHALL set the fallback image and bring it to the foreground layer.
- `ShowVideo()` SHALL hide the fallback image layer, making the VLC render surface the foreground.
- The default state at window creation SHALL be Fallback with no image (solid black).

#### Scenario: Fallback image is shown
- **WHEN** `ShowFallback(image)` is called with a loaded `BitmapImage`
- **THEN** the fallback `Image` element is visible and displays the provided bitmap

#### Scenario: Video surface is shown
- **WHEN** `ShowVideo()` is called
- **THEN** the fallback `Image` element is hidden (not visible) and the VLC HWND surface is the foreground

#### Scenario: Default state is solid black
- **WHEN** a `VlcDisplayWindow` is shown without calling `ShowFallback()` or `ShowVideo()`
- **THEN** the window displays solid black (no fallback image, no VLC content)

---

### Requirement: VideoEngine loads a video file onto a display

`VideoEngine.Load(SongManifest manifest, int displayIndex, VlcDisplayWindow window)` SHALL:
- Create a `MediaPlayer` for the video file matching `displayIndex` in `manifest.VideoFiles`.
- Set `MediaPlayer.Hwnd` to `window.Hwnd` so LibVLC renders into that window.
- Open the file with the VLC options `--no-audio` and `--no-osd`.
- Execute the pre-buffer sequence: call `MediaPlayer.Play()`, wait for the `Paused` state event (timeout: 2 seconds), then seek to position 0.
- Call `window.ShowVideo()` after the pre-buffer completes.
- If no video file in the manifest targets `displayIndex`, leave the window in its current state.

#### Scenario: Video file loads and pre-buffers successfully
- **WHEN** `VideoEngine.Load()` is called with a valid MP4 file and a ready `VlcDisplayWindow`
- **THEN** the `MediaPlayer` reaches the `Paused` state, is seeked to position 0, and the display shows the video surface

#### Scenario: No video for the given display index
- **WHEN** `VideoEngine.Load()` is called and no video file in the manifest targets the given display index
- **THEN** `VideoEngine.Load()` returns without error and the `VlcDisplayWindow` remains in its previous state

#### Scenario: Pre-buffer times out
- **WHEN** the `Paused` state event is not received within 2 seconds
- **THEN** `VideoEngine.Load()` logs a warning and returns, leaving the display in an indeterminate state (GO may still be pressed at operator risk)

---

### Requirement: VideoEngine plays all loaded videos on cue

`VideoEngine.Play(long audioStartTimestamp)` SHALL:
- Call `MediaPlayer.Play()` on all active (pre-buffered) MediaPlayers in sequence with no deliberate delay between calls.
- Record the elapsed time since `audioStartTimestamp` using `Stopwatch.GetTimestamp()` and log it at `Debug` level.
- Not block the calling thread; the call MUST return in under 5 ms on any supported machine.

#### Scenario: Play dispatches all active MediaPlayers
- **WHEN** `VideoEngine.Play(timestamp)` is called after a successful `Load()`
- **THEN** all active `MediaPlayer` instances receive a `Play()` call and begin rendering

#### Scenario: Elapsed time is logged
- **WHEN** `VideoEngine.Play(timestamp)` completes
- **THEN** the time elapsed since `audioStartTimestamp` is recorded in the log at `Debug` level by `SyncCoordinator` (which captures the post-`video.Play()` timestamp and emits the single authoritative Δt entry — `VideoEngine.Play()` itself logs only the MediaPlayer dispatch count)

---

### Requirement: VideoEngine stops cleanly and reverts displays to fallback

`VideoEngine.Stop()` SHALL:
- Call `MediaPlayer.Stop()` on all active MediaPlayers.
- Dispose all active `MediaPlayer` instances.
- Call `window.ShowFallback(currentFallbackImage)` for every managed `VlcDisplayWindow` (or `ShowFallback` with null / solid black if no fallback was set).
- Reset internal state so `Load()` can be called again for the next song.

#### Scenario: Stop clears active MediaPlayers
- **WHEN** `VideoEngine.Stop()` is called after `Play()`
- **THEN** all `MediaPlayer` instances are stopped and disposed

#### Scenario: Displays revert to fallback after stop
- **WHEN** `VideoEngine.Stop()` is called
- **THEN** every managed `VlcDisplayWindow` calls `ShowFallback()` and no longer shows the video surface

---

### Requirement: VideoEngine disposes all LibVLC resources in correct order

`VideoEngine.Dispose()` SHALL:
- Dispose all `MediaPlayer` instances before disposing the shared `LibVLC` instance.
- Be safe to call in any state, including before `Load()` or after a previous `Stop()`.
- Ensure no native LibVLC threads are running after `Dispose()` returns.

#### Scenario: Dispose is idempotent
- **WHEN** `VideoEngine.Dispose()` is called twice
- **THEN** no exception is thrown and all resources are released exactly once

#### Scenario: MediaPlayer disposed before LibVLC
- **WHEN** `VideoEngine.Dispose()` is called
- **THEN** all `MediaPlayer` instances are disposed before the shared `LibVLC` instance is disposed

---

### Requirement: LibVLC audio output is unconditionally silenced

The shared `LibVLC` instance SHALL be constructed with `"--no-audio"` in its options array, ensuring LibVLC never opens an audio device for any `MediaPlayer` it manages.

#### Scenario: VLC does not open an audio device
- **WHEN** `VideoEngine` is constructed and a video file is loaded and played
- **THEN** no Windows audio session is created by the LibVLC process and all audio is produced exclusively by `AudioEngine`
