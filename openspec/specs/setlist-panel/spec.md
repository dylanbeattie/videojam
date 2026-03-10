## ADDED Requirements

### Requirement: Setlist displays songs in show order
The setlist panel SHALL display all `SongEntry` items from `MainViewModel.Songs` in order, each showing a 1-based index number and the song name. The currently selected song SHALL be visually highlighted (e.g., bold text or accent background). Songs SHALL NOT be reorderable by clicking alone — only by drag-and-drop.

#### Scenario: Songs displayed in order
- **WHEN** a show with three songs ("Intro", "Middle", "Outro") is loaded
- **THEN** the setlist SHALL show `01 Intro`, `02 Middle`, `03 Outro` in that order

#### Scenario: Selected song is highlighted
- **WHEN** the operator clicks "02 Middle"
- **THEN** that row SHALL be visually highlighted and `MainViewModel.SelectedSong` SHALL be set to the corresponding `SongEntry`

---

### Requirement: Click to cue a song
Clicking a song row in the setlist SHALL set `MainViewModel.SelectedSong` to that song. This SHALL only be permitted when `PlaybackState` is `Idle` or `Cued` — clicking SHALL have no effect when `PlaybackState` is `Playing` or `Paused`.

#### Scenario: Click selects song in Idle state
- **WHEN** `PlaybackState` is `Idle` and the operator clicks a song
- **THEN** `SelectedSong` SHALL be updated and the mixer panel SHALL refresh to show that song's channels

#### Scenario: Click ignored during playback
- **WHEN** `PlaybackState` is `Playing` and the operator clicks a different song
- **THEN** `SelectedSong` SHALL NOT change

---

### Requirement: Drag-and-drop reordering
The setlist panel SHALL support drag-and-drop reordering of songs via a `DragDropBehaviour` attached behaviour. Dragging a song row and dropping it at a new position SHALL reorder `MainViewModel.Songs` using `ObservableCollection.Move()`. Reordering SHALL set `HasUnsavedChanges` to `true`.

#### Scenario: Drag song to a new position
- **WHEN** the operator drags song at index 0 and drops it at index 2
- **THEN** `Songs` SHALL be reordered so the dragged song is at index 2 and `HasUnsavedChanges` SHALL be `true`

#### Scenario: Drop on same position is a no-op
- **WHEN** the operator drags a song and drops it back on its original position
- **THEN** `Songs` order SHALL be unchanged and `HasUnsavedChanges` SHALL NOT be set

#### Scenario: Drag-drop disabled during playback
- **WHEN** `PlaybackState` is `Playing`
- **THEN** drag-drop reordering SHALL be disabled and songs SHALL NOT be movable

---

### Requirement: Add Song workflow
An "Add Song" button SHALL be visible in the setlist panel. Clicking it SHALL:
1. Open a folder picker via `IDialogService.PickFolder()`
2. Pass the selected path to `SongScanner.Scan()`
3. Create a `SongEntry` via `SongEntry.CreateFromScan()`
4. Append the new `SongEntry` to `MainViewModel.Songs`
5. Set `HasUnsavedChanges` to `true`

If the folder picker is cancelled, no song SHALL be added.

#### Scenario: Add Song appends to setlist
- **WHEN** the operator clicks "Add Song", selects a valid folder, and the scan succeeds
- **THEN** a new song SHALL appear at the bottom of the setlist and `HasUnsavedChanges` SHALL be `true`

#### Scenario: Add Song cancelled by operator
- **WHEN** the operator clicks "Add Song" and then clicks Cancel in the folder picker
- **THEN** no song SHALL be added and `HasUnsavedChanges` SHALL be unchanged

#### Scenario: Add Song with scan failure shows error
- **WHEN** `SongScanner.Scan()` throws (e.g., no audio files found)
- **THEN** a user-visible error message SHALL be shown via `IDialogService` and no song SHALL be appended

---

### Requirement: Remove Song
A "Remove" action SHALL be available for each song (e.g., a context menu or a remove button per row). Invoking it SHALL remove the song from `MainViewModel.Songs` and set `HasUnsavedChanges` to `true`. If the removed song was `SelectedSong`, `SelectedSong` SHALL be set to `null`.

#### Scenario: Remove selected song clears selection
- **WHEN** the operator removes the currently selected song
- **THEN** `SelectedSong` SHALL become `null` and the mixer panel SHALL show an empty state

#### Scenario: Remove non-selected song preserves selection
- **WHEN** the operator removes a song that is not currently selected
- **THEN** `SelectedSong` SHALL be unchanged
