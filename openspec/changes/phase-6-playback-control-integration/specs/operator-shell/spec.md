## MODIFIED Requirements

### Requirement: MainViewModel is the sole source of UI state
`MainViewModel` SHALL implement `INotifyPropertyChanged` and own all UI state. No business logic SHALL exist in `MainWindow` code-behind beyond WPF lifecycle glue (e.g., `InitializeComponent`). All WPF controls SHALL bind to `MainViewModel` properties.

`MainViewModel` SHALL expose:
- `ObservableCollection<SongEntry> Songs` — the setlist
- `SongEntry? SelectedSong` — the currently cued/selected song (derived from `PlaybackEngine.CuedSongIndex`)
- `PlaybackState PlaybackState` — current engine state (sourced from `PlaybackEngine.State`)
- `bool HasUnsavedChanges` — dirty flag
- `string StatusText` — human-readable status bar message
- `Show? LoadedShow` — the currently open show (null when none loaded)
- `bool IsLoading` — true while `PlaybackEngine.Cue()` is in progress (used to show a loading indicator and disable GO)

`MainViewModel` SHALL expose commands:
- `AddSongCommand`, `RemoveSongCommand`, `ReorderSongCommand`
- `NewShowCommand`, `OpenShowCommand`, `SaveShowCommand`, `SaveAsShowCommand`
- `GoCommand` — calls `PlaybackEngine.Go()`; disabled when `PlaybackState != Cued` or `IsLoading == true`
- `StopAndRewindCommand` — calls `PlaybackEngine.StopAndRewind()`; disabled when `PlaybackState != Playing && PlaybackState != Paused`

`MainViewModel` SHALL subscribe to `PlaybackEngine.StateChanged` and `HotkeyService.ButtonAPressed` / `HotkeyService.ButtonBPressed` at construction and unsubscribe on disposal.

#### Scenario: ViewModel properties notify on change
- **WHEN** `SelectedSong` is assigned a new value
- **THEN** `PropertyChanged` SHALL fire with `nameof(SelectedSong)` so the mixer panel updates

#### Scenario: No business logic in code-behind
- **WHEN** a developer inspects `MainWindow.xaml.cs`
- **THEN** the only code present SHALL be `InitializeComponent()` and WPF event wiring that delegates to ViewModel commands

#### Scenario: GoCommand is disabled when loading
- **WHEN** `PlaybackEngine.Cue()` is in progress (`IsLoading == true`)
- **THEN** `GoCommand.CanExecute()` returns `false` and the bound GO button is disabled

#### Scenario: ButtonA triggers Go via HotkeyService
- **WHEN** `HotkeyService.ButtonAPressed` fires
- **THEN** `MainViewModel` calls `PlaybackEngine.Go()` if `GoCommand.CanExecute()` is true; otherwise the press is silently ignored

#### Scenario: ButtonB triggers StopAndRewind via HotkeyService
- **WHEN** `HotkeyService.ButtonBPressed` fires
- **THEN** `MainViewModel` calls `PlaybackEngine.StopAndRewind()` if `StopAndRewindCommand.CanExecute()` is true; otherwise the press is silently ignored

---

## ADDED Requirements

### Requirement: Status bar reflects playback state
The status bar at the bottom of `MainWindow` SHALL display `StatusText` from `MainViewModel`. `StatusText` SHALL reflect the following states:

| Condition | StatusText |
|-----------|-----------|
| No show loaded | `"Ready"` |
| Show loaded, no song selected | `"Show loaded — select a song to cue"` |
| `PlaybackEngine.Cue()` in progress | `"Loading: {song name}…"` |
| `PlaybackState == Cued` | `"Cued: {song name} — press GO to start"` |
| `PlaybackState == Playing` | `"Playing: {song name}"` |
| `PlaybackState == Paused` | `"Paused: {song name} — press ESC to rewind"` |

#### Scenario: Status bar shows loading state
- **WHEN** `PlaybackEngine.Cue()` is in progress for a song named "Summer Nights"
- **THEN** `StatusText` SHALL be `"Loading: Summer Nights…"`

#### Scenario: Status bar shows playing state
- **WHEN** `PlaybackState` transitions to `Playing` for a song named "Summer Nights"
- **THEN** `StatusText` SHALL be `"Playing: Summer Nights"`

#### Scenario: Status bar shows paused state
- **WHEN** `PlaybackState` transitions to `Paused`
- **THEN** `StatusText` SHALL be `"Paused: {song name} — press ESC to rewind"`
