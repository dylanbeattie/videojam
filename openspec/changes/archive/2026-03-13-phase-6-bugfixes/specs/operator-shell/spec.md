## MODIFIED Requirements

### Requirement: Two-panel main window layout
`MainWindow` SHALL present a toolbar row, a two-panel layout (setlist panel on the left, mixer panel on the right), and a status bar across the bottom. The window title SHALL follow the pattern `VideoJam — {show name}` with an asterisk suffix when the show has unsaved changes (`VideoJam — {show name}*`). When no show is loaded, the title SHALL read `VideoJam — (no show)`.

`MainWindow` SHALL never be programmatically hidden during playback. It SHALL remain visible and interactive in all `PlaybackState` values (`Idle`, `Cued`, `Playing`, `Paused`).

#### Scenario: Window displays correct title when show is loaded
- **WHEN** a show named "Summer Tour" is loaded with no unsaved changes
- **THEN** the window title SHALL be `VideoJam — Summer Tour`

#### Scenario: Window title shows dirty indicator
- **WHEN** any model change is made after the last save
- **THEN** an asterisk SHALL appear at the end of the window title (`VideoJam — Summer Tour*`)

#### Scenario: Window title when no show is loaded
- **WHEN** the application starts with no show open
- **THEN** the window title SHALL be `VideoJam — (no show)`

#### Scenario: MainWindow remains visible during playback
- **WHEN** `PlaybackState` transitions to `Playing`
- **THEN** `MainWindow` SHALL remain visible; `Hide()` SHALL NOT be called

---

### Requirement: VlcDisplayWindow is not always-on-top
`VlcDisplayWindow` SHALL NOT use `Topmost="True"`. Its Z-order SHALL be managed by explicit `Activate()` calls:
- `PlaybackEngine.Go()` SHALL call `window.Activate()` on every managed `VlcDisplayWindow` after starting playback, bringing them to the foreground.
- `PlaybackEngine.StopAndRewind()` phase 2 (Paused → Cued) SHALL call `_mainWindow.Activate()` to return focus to the operator UI.
- `PlaybackEngine.OnPlaybackEnded()` SHALL call `_mainWindow.Activate()` to return focus when a song completes.

#### Scenario: Display windows come to front on Go
- **WHEN** `PlaybackEngine.Go()` is called
- **THEN** all managed `VlcDisplayWindow` instances receive `Activate()` and appear in the foreground

#### Scenario: MainWindow regains focus after stop/rewind
- **WHEN** `PlaybackEngine.StopAndRewind()` transitions state from `Paused` to `Cued`
- **THEN** `_mainWindow.Activate()` is called and the operator UI is in the foreground

#### Scenario: VlcDisplayWindow does not cover MainWindow involuntarily
- **WHEN** a `VlcDisplayWindow` is shown during `Cue()` and `PlaybackState == Cued`
- **THEN** the operator can click `MainWindow` controls because `VlcDisplayWindow` is not topmost

---

### Requirement: Ctrl+Tab returns focus to MainWindow from a display window
`VlcDisplayWindow` SHALL handle the `Ctrl+Tab` key combination. When `Ctrl+Tab` is pressed while a `VlcDisplayWindow` has keyboard focus, it SHALL call `Application.Current.MainWindow.Activate()` to return focus to the operator UI.

#### Scenario: Ctrl+Tab from display window activates MainWindow
- **WHEN** a `VlcDisplayWindow` has keyboard focus and the operator presses `Ctrl+Tab`
- **THEN** `MainWindow` is activated and brought to the foreground
