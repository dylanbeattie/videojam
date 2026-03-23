### Requirement: VlcDisplayWindow is a freely positionable, resizable window

A `VlcDisplayWindow` SHALL be a WPF `Window` with:
- `WindowStyle="SingleBorderWindow"` — standard title bar and border
- `ResizeMode="CanResize"` — freely resizable by the operator
- No `Topmost` property (default `false`) — must not obscure the operator UI

It SHALL have a black `Background` so unrendered areas do not show system chrome.

After the window is `Loaded`, it SHALL expose its Win32 HWND via `nint Hwnd`.

The window is keyed by `int SlotIndex` (zero-based). The title bar SHALL read `"VideoJam — Video {SlotIndex + 1}"`, set via `UpdateTitle()`.

#### Scenario: HWND is available after Loaded
- **WHEN** the `VlcDisplayWindow.Loaded` event has fired
- **THEN** `VlcDisplayWindow.Hwnd` is a non-zero, valid Win32 window handle

#### Scenario: Window title reflects slot index
- **WHEN** `SlotIndex` is set to `1` and `UpdateTitle()` is called
- **THEN** the window title is `"VideoJam — Video 2"`

---

### Requirement: VlcDisplayWindow close button hides rather than destroys

Clicking the window's close button (or pressing Alt+F4) SHALL hide the window rather than destroy it. The window SHALL be reused by `PlaybackEngine.EnsureVideoWindows()` on the next cue.

`ForceClose()` SHALL bypass this guard and close the window permanently. It is called only during application shutdown.

#### Scenario: Close button hides the window
- **WHEN** the operator clicks the close button on a `VlcDisplayWindow`
- **THEN** the window is hidden (`Visibility.Hidden`) and is not destroyed; the `Hwnd` remains valid

#### Scenario: ForceClose destroys the window
- **WHEN** `ForceClose()` is called
- **THEN** the window is closed and destroyed

---

### Requirement: VlcDisplayWindow supports layout capture and restore

`GetLayout()` SHALL return a `VideoWindowLayout` record capturing the window's current position, size, and maximised state. If the window is maximised, `RestoreBounds` SHALL be used so the non-maximised position is preserved.

`ApplyLayout(VideoWindowLayout layout)` SHALL restore a previously captured layout: set `Left`, `Top`, `Width`, `Height`, and if `layout.IsMaximised` is `true`, set `WindowState = Maximized`.

#### Scenario: GetLayout captures non-maximised bounds
- **WHEN** the window is at a known position and size and `GetLayout()` is called
- **THEN** the returned `VideoWindowLayout` has matching `Left`, `Top`, `Width`, `Height`, and `IsMaximised == false`

#### Scenario: GetLayout uses RestoreBounds when maximised
- **WHEN** the window is maximised and `GetLayout()` is called
- **THEN** `Left`, `Top`, `Width`, `Height` reflect the pre-maximised restore bounds, not the maximised screen dimensions

#### Scenario: ApplyLayout restores position and size
- **WHEN** `ApplyLayout(layout)` is called with a saved layout
- **THEN** `Left`, `Top`, `Width`, `Height` match the saved values

#### Scenario: ApplyLayout maximises the window when saved as maximised
- **WHEN** `ApplyLayout(layout)` is called with `layout.IsMaximised == true`
- **THEN** `WindowState` is set to `Maximized`

---

### Requirement: VlcDisplayWindow shows fallback PNG or video surface

A `VlcDisplayWindow` SHALL support two display states: **Fallback** (showing a static PNG) and **Video** (showing the LibVLC render surface).

- `ShowFallback(BitmapImage? image)` SHALL set the fallback image and bring it to the foreground layer. If `image` is `null`, the window displays solid black.
- `ShowVideo()` SHALL hide the fallback image layer, making the VLC render surface the foreground.
- The default state at window creation SHALL be Fallback with no image (solid black).

#### Scenario: Fallback image is shown
- **WHEN** `ShowFallback(image)` is called with a loaded `BitmapImage`
- **THEN** the fallback `Image` element is visible and displays the provided bitmap

#### Scenario: Video surface is shown
- **WHEN** `ShowVideo()` is called
- **THEN** the fallback `Image` element is hidden and the VLC HWND surface is the foreground

#### Scenario: Default state is solid black
- **WHEN** a `VlcDisplayWindow` is shown without calling `ShowFallback()` or `ShowVideo()`
- **THEN** the window displays solid black (no fallback image, no VLC content)

---

### Requirement: VideoEngine loads a video file for a slot

`VideoEngine.Load(SongManifest manifest, int slotIndex, VlcDisplayWindow window)` SHALL:
- Find the `VideoFileManifest` in `manifest.VideoFiles` whose `SlotIndex` matches the `slotIndex` parameter.
- Create a `MediaPlayer` for that video file.
- Set `MediaPlayer.Hwnd` to `window.Hwnd` so LibVLC renders into that window.
- Open the file with the VLC options `--no-audio` and `--no-osd`.
- Execute the pre-buffer sequence: call `MediaPlayer.Play()`, wait for the `Playing` state event (timeout: 2 seconds total), then call `MediaPlayer.SetPause(true)` from within the `Playing` event handler, then await the `Paused` state event, then seek to position 0.
- Call `window.ShowVideo()` after the pre-buffer completes.
- If no video file in the manifest has the given `slotIndex`, leave the window in its current state.

The `Playing` event MUST be used to gate the `SetPause(true)` call. Calling `SetPause(true)` before the `Playing` event fires is not permitted; VLC may be in `Opening` or `Buffering` state and will silently ignore the pause request.

#### Scenario: Video file loads and pre-buffers successfully
- **WHEN** `VideoEngine.Load()` is called with a valid MP4 file and a ready `VlcDisplayWindow`
- **THEN** the `MediaPlayer` fires `Playing`, then fires `Paused`, is seeked to position 0, and the display shows the video surface

#### Scenario: No video for the given slot index
- **WHEN** `VideoEngine.Load()` is called and no video file in the manifest has the given `slotIndex`
- **THEN** `VideoEngine.Load()` returns without error and the `VlcDisplayWindow` remains in its previous state

#### Scenario: Pre-buffer times out
- **WHEN** neither the `Playing` event nor the subsequent `Paused` event is received within 2 seconds total
- **THEN** `VideoEngine.Load()` logs a warning and returns, leaving the display in fallback state (GO may still be pressed; that display will show fallback for the song)

#### Scenario: Two slots pre-buffer concurrently without race
- **WHEN** `LoadAll` is called with two video files (e.g. slot 0 and slot 1) and both `Load()` calls run concurrently
- **THEN** both slots successfully pre-buffer regardless of which file VLC opens faster, because each `SetPause(true)` is gated on its own `Playing` event

---

### Requirement: VideoEngine plays all loaded videos on cue

`VideoEngine.Play(long audioStartTimestamp)` SHALL:
- Call `MediaPlayer.Play()` on all active (pre-buffered) MediaPlayers in sequence with no deliberate delay between calls.
- Not block the calling thread; the call MUST return in under 5 ms on any supported machine.

#### Scenario: Play dispatches all active MediaPlayers
- **WHEN** `VideoEngine.Play(timestamp)` is called after a successful `Load()`
- **THEN** all active `MediaPlayer` instances receive a `Play()` call and begin rendering

#### Scenario: Elapsed time is logged
- **WHEN** `VideoEngine.Play(timestamp)` completes
- **THEN** the time elapsed since `audioStartTimestamp` is recorded in the log at `Debug` level by `SyncCoordinator`

---

### Requirement: VideoEngine stops cleanly and reverts displays to fallback

`VideoEngine.Stop()` SHALL:
- Call `MediaPlayer.Stop()` on all active MediaPlayers.
- Dispose all active `MediaPlayer` instances.
- Call `window.ShowFallback(currentFallbackImage)` for every managed `VlcDisplayWindow` (or `ShowFallback(null)` / solid black if no fallback was set).
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

---

### Requirement: VideoEngine loads all slots concurrently

`VideoEngine.LoadAll(SongManifest manifest, IReadOnlyDictionary<int, VlcDisplayWindow> windows, CancellationToken cancellationToken)` SHALL call `Load()` concurrently for every `(slotIndex, window)` pair in `windows` and await all calls via `Task.WhenAll`.

If a `Load()` call times out during pre-buffering, it returns without adding a slot — `LoadAll()` SHALL still complete normally. If a `Load()` call throws an unhandled exception, `LoadAll()` SHALL propagate it to the caller.

#### Scenario: Two slots load concurrently
- **WHEN** `LoadAll` is called with a manifest containing video files for slot indices 0 and 1, and `windows` contains entries for both
- **THEN** both `Load()` calls are dispatched concurrently and `LoadAll` completes after both have finished pre-buffering

#### Scenario: Pre-buffer timeout on one slot does not prevent the other from loading
- **WHEN** `LoadAll` is called for two slots and the pre-buffer for slot 1 times out
- **THEN** `LoadAll` completes normally, slot 0 is in its pre-buffered state, and slot 1 remains in fallback state

#### Scenario: Empty windows dictionary completes immediately
- **WHEN** `LoadAll` is called with an empty `windows` dictionary
- **THEN** `LoadAll` returns a completed task without error
