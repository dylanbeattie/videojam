## MODIFIED Requirements

### Requirement: VideoEngine loads a video file onto a display
`VideoEngine.Load(SongManifest manifest, int displayIndex, VlcDisplayWindow window)` SHALL:
- Create a `MediaPlayer` for the video file matching `displayIndex` in `manifest.VideoFiles`.
- Set `MediaPlayer.Hwnd` to `window.Hwnd` so LibVLC renders into that window.
- Open the file with the VLC options `--no-audio` and `--no-osd`.
- Execute the pre-buffer sequence: call `MediaPlayer.Play()`, wait for the `Playing` state event (timeout: 2 seconds total), then call `MediaPlayer.SetPause(true)` from within the `Playing` event handler, then await the `Paused` state event, then seek to position 0.
- Call `window.ShowVideo()` after the pre-buffer completes.
- If no video file in the manifest targets `displayIndex`, leave the window in its current state.

The `Playing` event MUST be used to gate the `SetPause(true)` call. Calling `SetPause(true)` before the `Playing` event fires is not permitted; VLC may be in `Opening` or `Buffering` state and will silently ignore the pause request.

#### Scenario: Video file loads and pre-buffers successfully
- **WHEN** `VideoEngine.Load()` is called with a valid MP4 file and a ready `VlcDisplayWindow`
- **THEN** the `MediaPlayer` fires `Playing`, then fires `Paused`, is seeked to position 0, and the display shows the video surface

#### Scenario: No video for the given display index
- **WHEN** `VideoEngine.Load()` is called and no video file in the manifest targets the given display index
- **THEN** `VideoEngine.Load()` returns without error and the `VlcDisplayWindow` remains in its previous state

#### Scenario: Pre-buffer times out
- **WHEN** neither the `Playing` event nor the subsequent `Paused` event is received within 2 seconds total
- **THEN** `VideoEngine.Load()` logs a warning and returns, leaving the display in fallback state (GO may still be pressed; that display will show fallback for the song)

#### Scenario: Two displays pre-buffer concurrently without race
- **WHEN** `LoadAll` is called with two video files (e.g. performer and audience) and both `Load()` calls run concurrently
- **THEN** both displays successfully pre-buffer regardless of which file VLC opens faster, because each `SetPause(true)` is gated on its own `Playing` event
