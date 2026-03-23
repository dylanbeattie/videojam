## 1. PlaybackState Enum & AppState

- [x] 1.1 Add `PlaybackState { Idle, Cued, Playing, Paused }` enum to `VideoJam/Model/AppState.cs`
- [x] 1.2 Verify `AppState.cs` is referenced from both `Engine/` and `UI/ViewModels/` without circular dependencies

## 2. HotkeySettings & appsettings.json

- [x] 2.1 Create `VideoJam/Input/HotkeySettings.cs` with properties `ButtonA` and `ButtonB` (`System.Windows.Input.Key`)
- [x] 2.2 Create `appsettings.json` in the project root with defaults `{ "HotkeySettings": { "ButtonA": "Space", "ButtonB": "Escape" } }` and set `Copy to Output Directory: Copy if newer`
- [x] 2.3 Implement `HotkeySettings.Load(string appDirectory)` — reads and deserialises `appsettings.json` via `System.Text.Json`; falls back to defaults if file missing or keys invalid

## 3. HotkeyService

- [x] 3.1 Create `VideoJam/Input/HotkeyService.cs` with P/Invoke declarations for `SetWindowsHookEx`, `UnhookWindowsHookEx`, `CallNextHookEx`, and `KBDLLHOOKSTRUCT`
- [x] 3.2 Implement constructor: validate UI thread, load `HotkeySettings`, install `WH_KEYBOARD_LL` hook; throw `InvalidOperationException` if not on UI thread
- [x] 3.3 Implement hook callback: parse VKey from `KBDLLHOOKSTRUCT`, compare to configured keys, dispatch `ButtonAPressed`/`ButtonBPressed` via `Dispatcher.BeginInvoke` on WM_KEYDOWN only, always call `CallNextHookEx`
- [x] 3.4 Expose `event EventHandler ButtonAPressed` and `event EventHandler ButtonBPressed`
- [x] 3.5 Implement `IDisposable.Dispose()`: call `UnhookWindowsHookEx` and release the `GCHandle` on the callback delegate

## 4. PlaybackEngine — Core State Machine

- [x] 4.1 Create `VideoJam/Engine/PlaybackEngine.cs` implementing `IDisposable`
- [x] 4.2 Implement `PlaybackState State { get; private set; }` and `StateChanged` event (UI thread dispatch)
- [x] 4.3 Implement `int CuedSongIndex { get; private set; }` initialised to -1
- [x] 4.4 Implement `Go()`: validate `State == Cued`, subscribe to `AudioEngine.PlaybackEnded`, call `SyncCoordinator.Start()`, call `mainWindow.Hide()`, set `State = Playing`
- [x] 4.5 Implement `StopAndRewind()`: Phase 1 (Playing → Paused) — call `AudioEngine.Stop()`, pause video players, set `State = Paused`; Phase 2 (Paused → Cued) — call `VideoEngine.Stop()`, call `mainWindow.Show() + Activate()`, call `Cue(CuedSongIndex)`; throw `InvalidOperationException` from other states
- [x] 4.6 Implement `Dispose()`: stop engines, unsubscribe events, release resources

## 5. PlaybackEngine — Cue and Auto-Advance

- [x] 5.1 Implement `async Task Cue(int songIndex, CancellationToken ct = default)`: cancel any prior cue task, dispose existing `AudioEngine`, stop existing `VideoEngine`, resolve song folder, scan, load audio, load all video concurrently, set `CuedSongIndex`, set `State = Cued`
- [x] 5.2 Add per-cue `CancellationTokenSource` management so calling `Cue()` twice cancels the first operation cleanly
- [x] 5.3 Implement `OnPlaybackEnded()` handler: call `VideoEngine.Stop()`, call `mainWindow.Show() + Activate()`, determine next song index, if next exists fire-and-forget `Cue(nextIndex)`, else set `State = Idle` and `CuedSongIndex = -1`

## 6. MainViewModel — Playback Wiring

- [x] 6.1 Inject `PlaybackEngine` and `HotkeyService` into `MainViewModel` constructor
- [x] 6.2 Subscribe to `PlaybackEngine.StateChanged` → update `PlaybackState`, `SelectedSong`, `StatusText`, `IsLoading`; unsubscribe on disposal
- [x] 6.3 Subscribe to `HotkeyService.ButtonAPressed` → execute `GoCommand` if `CanExecute`; subscribe to `ButtonBPressed` → execute `StopAndRewindCommand` if `CanExecute`
- [x] 6.4 Add `bool IsLoading { get; }` property notifying UI while `Cue()` is in progress
- [x] 6.5 Implement `GoCommand` (disabled when `State != Cued || IsLoading`) — calls `PlaybackEngine.Go()`
- [x] 6.6 Implement `StopAndRewindCommand` (disabled when `State != Playing && State != Paused`) — calls `PlaybackEngine.StopAndRewind()`
- [x] 6.7 Update `StatusText` logic for all six status conditions from the `operator-shell` spec
- [x] 6.8 Update setlist click handler (`CueSongCommand`) to call `PlaybackEngine.Cue(index)` async; guard against `Playing` and `Paused` states

## 7. MainWindow & App Wiring

- [x] 7.1 Update `App.xaml.cs` to construct `HotkeyService` after `MainWindow` is shown and inject into `MainViewModel`
- [x] 7.2 Update `App.xaml.cs` to construct `PlaybackEngine` with references to `AudioEngine` factory, `VideoEngine`, `SyncCoordinator`, `DisplayManager`, and `MainWindow`; inject into `MainViewModel`
- [x] 7.3 Ensure `HotkeyService` and `PlaybackEngine` are disposed in `Application.Exit` or `MainWindow.Closing`

## 8. Setlist Panel — Phase 6 Behaviour

- [x] 8.1 Ensure setlist item click calls `CueSongCommand` (which routes to `PlaybackEngine.Cue()`) rather than directly setting `SelectedSong`
- [x] 8.2 Bind `DragDropBehaviour.IsEnabled` to `PlaybackState == Idle || PlaybackState == Cued` (disable in both Playing and Paused)
- [x] 8.3 Bind `SelectedItem` highlighting to `CuedSongIndex` via `PlaybackEngine.StateChanged` propagation through `MainViewModel.SelectedSong`

## 9. End-to-End Manual Test

- [ ] 9.1 Load a show with 3 songs; verify Display 0 shows fallback PNG, `MainWindow` visible, `StatusText = "Cued: {song 1}"`
- [ ] 9.2 Press Space → verify `MainWindow` hides, audio + video start in sync, `StatusText = "Playing: {song 1}"`
- [ ] 9.3 Let song 1 complete → verify auto-advance to song 2, `MainWindow` reappears momentarily, transitions to loading/cued
- [ ] 9.4 Press Space, then press Escape → verify `State == Paused`, displays remain in video state, `MainWindow` still hidden
- [ ] 9.5 Press Escape again → verify `State` transitions to `Cued`, `MainWindow` reappears, fallback PNG restored
- [ ] 9.6 Press Space to restart song → verify clean playback from beginning
- [ ] 9.7 Let all 3 songs complete → verify `State == Idle`, all displays show fallback PNG, `MainWindow` visible
