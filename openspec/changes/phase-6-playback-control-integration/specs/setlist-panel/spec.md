## MODIFIED Requirements

### Requirement: Click to cue a song
Clicking a song row in the setlist SHALL call `PlaybackEngine.Cue(songIndex)` asynchronously. This SHALL only be permitted when `PlaybackState` is `Idle` or `Cued` — clicking SHALL have no effect when `PlaybackState` is `Playing` or `Paused`. `SelectedSong` in `MainViewModel` SHALL be derived from `PlaybackEngine.CuedSongIndex` and SHALL update when `PlaybackEngine.StateChanged` fires, not directly from a click handler.

#### Scenario: Click cues song in Idle state
- **WHEN** `PlaybackState` is `Idle` and the operator clicks a song
- **THEN** `PlaybackEngine.Cue(songIndex)` is called and `IsLoading` becomes `true` while loading

#### Scenario: Click cues song in Cued state
- **WHEN** `PlaybackState` is `Cued` and the operator clicks a different song
- **THEN** `PlaybackEngine.Cue(newSongIndex)` is called, cancelling the previous cue

#### Scenario: Click ignored during playback
- **WHEN** `PlaybackState` is `Playing` and the operator clicks a different song
- **THEN** `PlaybackEngine.Cue()` is NOT called and `SelectedSong` SHALL NOT change

#### Scenario: Click ignored when paused
- **WHEN** `PlaybackState` is `Paused` and the operator clicks a different song
- **THEN** `PlaybackEngine.Cue()` is NOT called and `SelectedSong` SHALL NOT change

---

## MODIFIED Requirements

### Requirement: Drag-and-drop reordering
The setlist panel SHALL support drag-and-drop reordering of songs via a `DragDropBehaviour` attached behaviour. Dragging a song row and dropping it at a new position SHALL reorder `MainViewModel.Songs` using `ObservableCollection.Move()`. Reordering SHALL set `HasUnsavedChanges` to `true`. Drag-drop SHALL be disabled when `PlaybackState` is `Playing` **or `Paused`**.

#### Scenario: Drag song to a new position
- **WHEN** the operator drags song at index 0 and drops it at index 2
- **THEN** `Songs` SHALL be reordered so the dragged song is at index 2 and `HasUnsavedChanges` SHALL be `true`

#### Scenario: Drop on same position is a no-op
- **WHEN** the operator drags a song and drops it back on its original position
- **THEN** `Songs` order SHALL be unchanged and `HasUnsavedChanges` SHALL NOT be set

#### Scenario: Drag-drop disabled during playback
- **WHEN** `PlaybackState` is `Playing`
- **THEN** drag-drop reordering SHALL be disabled and songs SHALL NOT be movable

#### Scenario: Drag-drop disabled when paused
- **WHEN** `PlaybackState` is `Paused`
- **THEN** drag-drop reordering SHALL be disabled and songs SHALL NOT be movable
