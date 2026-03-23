## Why

Phase 6 delivered the PlaybackEngine state machine and HotkeyService, but revealed three blocking regressions during user testing: the performer video never plays (pre-buffer race), the always-on-top display window buries the operator UI (design flaw), and there are no clickable transport controls in the main window (missing feature). These must be resolved before the application is usable in a live performance context.

## What Changes

- **Pre-buffer race fix**: `VideoEngine.Load()` calls `SetPause(true)` before VLC has reached the `Playing` state, so the `Paused` event never fires for concurrent loads — the performer's video slot times out, registers no slot, and that display stays black forever. Fix: gate `SetPause(true)` on the `Playing` event.
- **Remove Topmost + MainWindow.Hide()**: `VlcDisplayWindow` is `Topmost="True"` and is shown during `Cue()` — covering the MainWindow before playback even begins. `Go()` then hides MainWindow entirely, removing operator control. Fix: strip `Topmost`, remove `Hide()`/`Show()` calls, manage Z-order via `Activate()` instead.
- **Ctrl+Tab window switching**: `VlcDisplayWindow` will gain a `Ctrl+Tab` key binding that returns focus to `MainWindow`, so the operator can regain the operator UI from keyboard alone.
- **Playback toolbar**: Add a `ToolBar` to `MainWindow` with Go ▶, Pause ⏸, and Stop/Rewind ⏮ buttons bound to `GoCommand` and `StopAndRewindCommand`.

## Capabilities

### New Capabilities

- `playback-toolbar`: Transport controls toolbar in the operator shell — Go, Pause, Stop/Rewind buttons bound to existing ViewModel commands.

### Modified Capabilities

- `video-engine`: Pre-buffer sequence changes — `Playing` event gates the `SetPause(true)` call instead of firing it immediately after `Play()`.
- `operator-shell`: MainWindow is no longer hidden during playback; display windows are no longer Topmost; Z-order managed via `Activate()`. Toolbar added. Ctrl+Tab focus-return binding added to `VlcDisplayWindow`.

## Impact

- `VideoJam/Engine/VideoEngine.cs` — pre-buffer logic
- `VideoJam/Engine/PlaybackEngine.cs` — remove `Hide()`/`Show()`/`Activate()` mainWindow calls; add `Activate()` on display windows at Go
- `VideoJam/UI/VlcDisplayWindow.xaml` — remove `Topmost="True"`
- `VideoJam/UI/VlcDisplayWindow.xaml.cs` — add Ctrl+Tab KeyDown handler
- `VideoJam/UI/MainWindow.xaml` — add playback toolbar
- No new NuGet dependencies

## Non-goals for this phase

- Graceful recovery from corrupted or missing video files (Phase 7)
- Per-display Topmost configuration (e.g., topmost only on secondary displays)
- Full pause-and-resume mid-song (existing two-phase stop/rewind behaviour is unchanged)
- Any change to audio engine, sync coordinator, or show file format

## Primary Technical Risk

**VLC `Playing` event timing**: LibVLC fires `Playing` on an internal thread. If the event fires and the handler calls `SetPause(true)` before VLC's decoder has decoded a first frame, VLC may still not reliably pause — the timeout guard (2 s) provides a safety net, but we should validate with real MP4 files during manual testing (task 9).
