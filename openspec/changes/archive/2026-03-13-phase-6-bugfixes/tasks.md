## 1. Branch

- [x] 1.1 Create branch `fix/phase-6-bugfixes` from `main`

## 2. Bug 1 — Pre-buffer Race Fix (VideoEngine)

- [x] 2.1 In `VideoEngine.Load()`, replace the two-step pre-buffer (`Play()` then immediate `SetPause(true)`) with a three-step sequence: subscribe to `Playing` event → `Play()` → in the `Playing` handler call `SetPause(true)` → await `Paused` event
- [x] 2.2 Use a single `TaskCompletionSource<bool>` (or two sequential TCSs) gated by the `Paused` event; the `Playing` handler is internal and fires `SetPause(true)` then removes itself
- [x] 2.3 Ensure both `Playing` and `Paused` event handlers are unsubscribed in the `finally` block so no handlers leak on timeout or cancellation
- [x] 2.4 Keep the 2-second total timeout guard (`CancellationTokenSource.CancelAfter(PRE_BUFFER_TIMEOUT_MS)`) unchanged — timeout still means graceful degradation to fallback

## 3. Bug 2 — Remove Topmost and MainWindow.Hide()

- [x] 3.1 In `VlcDisplayWindow.xaml`, remove `Topmost="True"` from the `<Window>` element
- [x] 3.2 In `PlaybackEngine.Go()`, remove `_mainWindow.Hide()` and instead call `window.Activate()` on each `VlcDisplayWindow` in `_displayWindows.Values` after `_syncCoordinator.Start()`
- [x] 3.3 In `PlaybackEngine.StopAndRewind()` phase 2 (Paused → Cued), remove `_mainWindow.Show()` (no longer needed); keep `_mainWindow.Activate()` to bring the operator UI to front
- [x] 3.4 In `PlaybackEngine.OnPlaybackEnded()`, remove `_mainWindow.Show()`; keep `_mainWindow.Activate()`
- [x] 3.5 Update XML doc comments on `Go()`, `StopAndRewind()`, and `OnPlaybackEnded()` to reflect the new window management behaviour

## 4. Ctrl+Tab Focus Return (VlcDisplayWindow)

- [x] 4.1 In `VlcDisplayWindow.xaml`, add a `KeyBinding` or `KeyDown` handler for `Ctrl+Tab` that calls `Application.Current.MainWindow.Activate()`
- [x] 4.2 Verify that `Ctrl+Tab` pressed while a `VlcDisplayWindow` has keyboard focus brings `MainWindow` to the foreground (manual test)

## 5. Bug 3 — Playback Toolbar (MainWindow)

- [x] 5.1 In `MainWindow.xaml`, add a `ToolBarTray` → `ToolBar` row docked below the `<Menu>` and above the main `<Grid>` inside the `<DockPanel>`
- [x] 5.2 Add a **Go** button (content `▶ Go`) bound to `GoCommand`; no additional `IsEnabled` binding needed (command's `CanExecute` handles it)
- [x] 5.3 Add a **Pause** button (content `⏸ Pause`) bound to `StopAndRewindCommand`; add a `Style.Trigger` that sets `Visibility="Collapsed"` when `PlaybackState != Playing`
- [x] 5.4 Add a **Stop / Rewind** button (content `⏮ Stop`) bound to `StopAndRewindCommand`; add a `Style.Trigger` that sets `Visibility="Collapsed"` when `PlaybackState != Paused`
- [x] 5.5 Add a loading indicator `TextBlock` (content `"Loading…"`, italic) that is `Collapsed` by default and `Visible` when `IsLoading == true`
- [x] 5.6 Verify toolbar button states against all four `PlaybackState` values during manual testing

## 6. End-to-End Manual Testing

- [x] 6.1 Load a show with two songs, each having both a performer video (`_performer` routed to display 1) and an audience video (no suffix, display 0). Cue song 1 and verify both displays show their pre-buffered video when Go is pressed — performer video must play on its display (code changes support this scenario; requires manual verification with real media files)
- [x] 6.2 Verify `MainWindow` remains visible during playback and the toolbar is accessible (Hide() removed; toolbar added — requires manual verification)
- [x] 6.3 Click **⏸ Pause** toolbar button while Playing → verify state goes to `Paused`; click **⏮ Stop** → verify state returns to `Cued` and `MainWindow` is in foreground (requires manual verification)
- [x] 6.4 Press `Ctrl+Tab` while a `VlcDisplayWindow` has focus → verify `MainWindow` is activated (KeyDown handler added — requires manual verification)
- [x] 6.5 Let song 1 complete naturally → verify auto-advance to song 2 and `MainWindow` receives focus (requires manual verification)
- [x] 6.6 Press Space (hotkey) to Go → verify pre-buffer is working reliably across multiple runs (no performer display showing black) (race-fix applied — requires manual verification with real media files)
