## ADDED Requirements

### Requirement: PlaybackEngine defines a four-state machine
`PlaybackEngine` SHALL implement a state machine with exactly four states: `Idle`, `Cued`, `Playing`, and `Paused`. The `PlaybackState` enum SHALL be defined in `VideoJam/Model/AppState.cs`. `PlaybackEngine` SHALL expose a `PlaybackState State { get; }` property and a `StateChanged` event that fires (on the WPF UI thread) whenever state transitions. Invalid transitions (e.g. calling `Go()` from `Idle`) SHALL throw `InvalidOperationException`.

#### Scenario: Initial state is Idle
- **WHEN** `PlaybackEngine` is constructed
- **THEN** `State` SHALL be `PlaybackState.Idle`

#### Scenario: Invalid transition throws
- **WHEN** `Go()` is called while `State` is `Idle`
- **THEN** `InvalidOperationException` is thrown and `State` remains `Idle`

#### Scenario: StateChanged fires on UI thread
- **WHEN** any state transition occurs
- **THEN** the `StateChanged` event is raised on the WPF dispatcher thread

---

### Requirement: PlaybackEngine.Cue() pre-loads the song pipeline
`PlaybackEngine.Cue(int songIndex)` SHALL be an `async Task` method that:
1. Cancels any in-progress cue operation for a previous song.
2. Disposes any existing `AudioEngine` and calls `VideoEngine.Stop()` on any active video session.
3. Resolves the song folder path via `PathResolver.Resolve()`.
4. Calls `SongScanner.Scan(folderPath)` to obtain a `SongManifest`.
5. Constructs a new `AudioEngine` and calls `AudioEngine.Load(manifest, channelSettings)`.
6. Calls `VideoEngine.LoadAll(manifest, windows, cancellationToken)` to pre-buffer all video files concurrently.
7. Stores `songIndex` as the currently cued index and transitions state to `Cued`.

If any step from 3–6 throws, the state SHALL remain `Idle` and the exception SHALL be re-thrown to the caller.

#### Scenario: Cue transitions to Cued on success
- **WHEN** `Cue(0)` is called with a valid song at index 0
- **THEN** `State` transitions to `Cued` after loading completes

#### Scenario: Cue replaces a previous Cue
- **WHEN** `Cue(0)` is in progress and `Cue(1)` is called
- **THEN** the first cue operation is cancelled, the new song at index 1 is loaded, and `State` transitions to `Cued` for song 1

#### Scenario: Cue failure leaves state Idle
- **WHEN** `Cue(n)` is called for a song whose folder does not exist
- **THEN** an exception is thrown and `State` remains `Idle`

#### Scenario: Cue disposes previous song resources
- **WHEN** `Cue(1)` is called after a previous `Cue(0)` completed
- **THEN** the `AudioEngine` from song 0 is disposed before loading song 1

---

### Requirement: PlaybackEngine.Go() starts playback
`PlaybackEngine.Go()` SHALL:
1. Validate that `State == Cued`; throw `InvalidOperationException` otherwise.
2. Subscribe to `AudioEngine.PlaybackEnded` before calling `SyncCoordinator.Start()`.
3. Call `SyncCoordinator.Start(audioEngine, videoEngine)`.
4. Hide `MainWindow` (call `mainWindow.Hide()`).
5. Transition `State` to `Playing`.

`Go()` SHALL NOT be async — it must complete in under 5 ms on any supported machine (the sync-critical path is inside `SyncCoordinator.Start()`).

#### Scenario: Go transitions to Playing
- **WHEN** `Go()` is called while `State == Cued`
- **THEN** `SyncCoordinator.Start()` is called and `State` transitions to `Playing`

#### Scenario: MainWindow is hidden on Go
- **WHEN** `Go()` is called
- **THEN** `MainWindow.Hide()` is called before the method returns

#### Scenario: Go throws if not Cued
- **WHEN** `Go()` is called while `State == Idle` or `State == Playing`
- **THEN** `InvalidOperationException` is thrown and state is unchanged

---

### Requirement: PlaybackEngine handles natural song end and auto-advances
When `AudioEngine.PlaybackEnded` fires, `PlaybackEngine` SHALL:
1. Call `VideoEngine.Stop()` to revert all displays to fallback PNG.
2. Show `MainWindow` (call `mainWindow.Show(); mainWindow.Activate()`).
3. Determine the next song index (current index + 1).
4. If a next song exists: call `Cue(nextIndex)` asynchronously (fire-and-forget with error logging).
5. If no next song exists (end of setlist): transition `State` to `Idle` and set `SelectedSongIndex` to -1 (no selection).

#### Scenario: Auto-advance cues next song
- **WHEN** playback of song 0 ends naturally and the show has a song at index 1
- **THEN** `VideoEngine.Stop()` is called, `MainWindow` is shown, and `Cue(1)` is initiated

#### Scenario: End of setlist returns to Idle
- **WHEN** playback of the last song ends naturally
- **THEN** `VideoEngine.Stop()` is called, `MainWindow` is shown, and `State` transitions to `Idle`

#### Scenario: MainWindow is restored after song ends
- **WHEN** `PlaybackEnded` fires
- **THEN** `mainWindow.Show()` and `mainWindow.Activate()` are called before the next cue begins

---

### Requirement: PlaybackEngine.StopAndRewind() implements two-phase rewind
`PlaybackEngine.StopAndRewind()` SHALL implement a two-phase stop:

- **Phase 1 — Playing → Paused:**
  - Validates `State == Playing`.
  - Pauses all audio via `AudioEngine` (set `WasapiOut` to paused state — see note below).
  - Pauses all video via `VideoEngine` (call `MediaPlayer.SetPause(true)` on all active players).
  - Transitions `State` to `Paused`.
  - Does NOT restore `MainWindow` — the operator can see that playback is paused.

- **Phase 2 — Paused → Cued:**
  - Validates `State == Paused`.
  - Calls `AudioEngine.Stop()` (disposes the pipeline).
  - Calls `VideoEngine.Stop()` (stops and disposes MediaPlayers; shows fallback PNGs).
  - Restores `MainWindow` (calls `mainWindow.Show(); mainWindow.Activate()`).
  - Calls `Cue(currentSongIndex)` asynchronously to reload and re-buffer the same song.

> **Note on audio pause:** NAudio's `WasapiOut` does not support true pause/resume. Phase 1 pause SHALL call `AudioEngine.Stop()` and record the intention as "paused" — the state machine is `Paused` but the audio pipeline is stopped. Phase 2 rewind then simply calls `Cue()` on the same song, which is equivalent behaviour since the rewind always goes to the beginning.

#### Scenario: First Escape pauses (Playing → Paused)
- **WHEN** `StopAndRewind()` is called while `State == Playing`
- **THEN** `AudioEngine.Stop()` is called, `VideoEngine` media players are paused, and `State` transitions to `Paused`

#### Scenario: Second Escape rewinds (Paused → Cued)
- **WHEN** `StopAndRewind()` is called while `State == Paused`
- **THEN** `VideoEngine.Stop()` is called, `MainWindow` is shown, `Cue(currentSongIndex)` is initiated, and `State` eventually transitions to `Cued`

#### Scenario: StopAndRewind throws from Idle or Cued
- **WHEN** `StopAndRewind()` is called while `State == Idle` or `State == Cued`
- **THEN** `InvalidOperationException` is thrown and state is unchanged

---

### Requirement: PlaybackEngine exposes the cued song index
`PlaybackEngine` SHALL expose `int CuedSongIndex { get; }` returning the index of the currently cued song (0-based), or `-1` when no song is cued (`State == Idle`). `MainViewModel` SHALL bind `SelectedSong` from this index.

#### Scenario: CuedSongIndex is -1 in Idle
- **WHEN** `State == Idle`
- **THEN** `CuedSongIndex == -1`

#### Scenario: CuedSongIndex reflects current song
- **WHEN** `Cue(2)` completes successfully
- **THEN** `CuedSongIndex == 2`

---

### Requirement: PlaybackEngine implements IDisposable
`PlaybackEngine.Dispose()` SHALL stop any active playback (`AudioEngine.Stop()`, `VideoEngine.Stop()`), unsubscribe all events, and ensure all engine resources are released. It SHALL be safe to call in any state.

#### Scenario: Dispose during playback does not throw
- **WHEN** `Dispose()` is called while `State == Playing`
- **THEN** no exception is thrown and all `AudioEngine`/`VideoEngine` resources are released
