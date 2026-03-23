## ADDED Requirements

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

### Requirement: MainViewModel is the sole source of UI state
`MainViewModel` SHALL implement `INotifyPropertyChanged` and own all UI state. No business logic SHALL exist in `MainWindow` code-behind beyond WPF lifecycle glue (e.g., `InitializeComponent`). All WPF controls SHALL bind to `MainViewModel` properties.

`MainViewModel` SHALL expose:
- `ObservableCollection<SongEntry> Songs` — the setlist
- `SongEntry? SelectedSong` — the currently cued/selected song
- `PlaybackState PlaybackState` — current engine state (defaults to `Idle` in Phase 5)
- `bool HasUnsavedChanges` — dirty flag
- `string StatusText` — human-readable status bar message
- `Show? LoadedShow` — the currently open show (null when none loaded)

`MainViewModel` SHALL expose commands:
- `AddSongCommand`, `RemoveSongCommand`, `ReorderSongCommand`
- `NewShowCommand`, `OpenShowCommand`, `SaveShowCommand`, `SaveAsShowCommand`

#### Scenario: ViewModel properties notify on change
- **WHEN** `SelectedSong` is assigned a new value
- **THEN** `PropertyChanged` SHALL fire with `nameof(SelectedSong)` so the mixer panel updates

#### Scenario: No business logic in code-behind
- **WHEN** a developer inspects `MainWindow.xaml.cs`
- **THEN** the only code present SHALL be `InitializeComponent()` and WPF event wiring that delegates to ViewModel commands

---

### Requirement: RelayCommand implementation
A `RelayCommand` (non-generic) and `RelayCommand<T>` (generic) class SHALL be provided in `VideoJam/UI/ViewModels/RelayCommand.cs`, implementing `ICommand`. Both SHALL accept an optional `canExecute` predicate and SHALL raise `CanExecuteChanged` via `CommandManager.RequerySuggested`.

#### Scenario: RelayCommand respects canExecute
- **WHEN** the `canExecute` predicate returns `false`
- **THEN** `CanExecute()` SHALL return `false` and the bound WPF control SHALL be disabled

---

### Requirement: IDialogService abstraction
An `IDialogService` interface SHALL be defined in `VideoJam/UI/` with methods:
- `string? PickFolder(string title)` — returns the selected path or null if cancelled
- `string? PickOpenFile(string title, string filter)` — returns the selected path or null if cancelled
- `string? PickSaveFile(string title, string filter, string defaultExtension)` — returns the selected path or null if cancelled
- `bool Confirm(string message, string title)` — returns true if user confirms

The production implementation (`WpfDialogService`) SHALL use WPF dialogs and SHALL be injected into `MainViewModel` at application startup in `App.xaml.cs`.

#### Scenario: PickFolder returns null on cancel
- **WHEN** the user opens a folder picker and clicks Cancel
- **THEN** `PickFolder()` SHALL return `null` and no song SHALL be added

#### Scenario: Confirm returns false on No
- **WHEN** the user is shown an unsaved-changes prompt and clicks "No"
- **THEN** `Confirm()` SHALL return `false` and the pending operation SHALL be aborted

---

### Requirement: Status bar
A status bar at the bottom of `MainWindow` SHALL display `StatusText` from `MainViewModel`. In Phase 5, `StatusText` SHALL reflect:
- `"Ready"` when no show is loaded
- `"Show loaded — select a song to cue"` when a show is open but no song selected
- `"Cued: {song name}"` when a song is selected

#### Scenario: Status bar updates on song selection
- **WHEN** the operator clicks a song in the setlist
- **THEN** `StatusText` SHALL update to `"Cued: {song name}"`

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
