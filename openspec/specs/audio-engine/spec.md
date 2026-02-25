## ADDED Requirements

### Requirement: AudioEngine loads a multi-stem pipeline from a SongManifest
`AudioEngine.Load(SongManifest manifest, Dictionary<string, ChannelSettings> channelSettings)` SHALL construct a NAudio pipeline for all audio channels in the manifest:
- `.wav` and `.mp3` channels use `AudioFileReader`
- `.aiff` channels use `AiffFileReader`
- `.mp4` audio channels use `MediaFoundationReader`

Each reader SHALL be wrapped in a `VolumeSampleProvider` whose `Volume` is set from `ChannelSettings.Level` (defaulting to `1.0f` if the channel has no entry in `channelSettings`). All providers SHALL be composed into a single `MixingSampleProvider` targeting 44100 Hz, 32-bit float, stereo. The mix SHALL be attached to a `WasapiOut` instance configured for shared mode with a 50ms latency buffer. `Load` SHALL NOT start playback.

#### Scenario: Loading a folder with three WAV stems creates three VolumeSampleProviders
- **WHEN** `Load` is called with a manifest containing three `Stem` channels
- **THEN** the internal pipeline contains three `VolumeSampleProvider` instances feeding a single `MixingSampleProvider`, and `WasapiOut` is initialised but not playing

#### Scenario: Channel level from ChannelSettings is applied to the VolumeSampleProvider
- **WHEN** `Load` is called and `channelSettings["drums.wav"].Level` is `0.5f`
- **THEN** the `VolumeSampleProvider` for `drums.wav` has `Volume == 0.5f`

#### Scenario: Channel with no ChannelSettings entry defaults to level 1.0
- **WHEN** `Load` is called and `channelSettings` does not contain an entry for a channel
- **THEN** that channel's `VolumeSampleProvider` has `Volume == 1.0f`

---

### Requirement: AudioEngine.Play() starts all stems simultaneously and returns a timestamp
`AudioEngine.Play()` SHALL call `WasapiOut.Play()` and immediately capture a `Stopwatch.GetTimestamp()` value. It SHALL return this timestamp as a `long`. All stems start in the same WASAPI callback cycle — synchronisation is exact by construction.

#### Scenario: Play returns a non-zero timestamp
- **WHEN** `Play()` is called after a successful `Load()`
- **THEN** the returned `long` is greater than zero

#### Scenario: Play transitions WASAPI to the playing state
- **WHEN** `Play()` is called
- **THEN** audio is rendered to the output device and stems are audible (verified manually in the integration harness)

---

### Requirement: AudioEngine raises PlaybackEnded on natural end-of-content
`AudioEngine` SHALL expose a `PlaybackEnded` event. This event SHALL be raised when `WasapiOut` raises `PlaybackStopped` after the `MixingSampleProvider` has exhausted all input (natural end of all stems). The event SHALL NOT be raised when `Stop()` is called explicitly. The event SHALL be raised on the WPF UI thread (via `Application.Current.Dispatcher.Invoke` or equivalent).

#### Scenario: PlaybackEnded fires after all stems complete naturally
- **WHEN** `Play()` is called and all stems reach their end
- **THEN** `PlaybackEnded` is raised exactly once on the UI thread

#### Scenario: PlaybackEnded does not fire after explicit Stop
- **WHEN** `Stop()` is called while playback is active
- **THEN** `PlaybackEnded` is NOT raised

---

### Requirement: AudioEngine.Stop() halts playback and disposes all resources
`AudioEngine.Stop()` SHALL call `WasapiOut.Stop()`, dispose all audio readers and the `WasapiOut` instance, and reset the engine to an unloaded state. `Stop()` SHALL be safe to call in any state (including before `Load()` or after a previous `Stop()`).

#### Scenario: Stop disposes all resources
- **WHEN** `Stop()` is called after `Play()`
- **THEN** all `AudioFileReader` / `AiffFileReader` / `MediaFoundationReader` instances are disposed and `WasapiOut` is disposed

#### Scenario: Stop is idempotent
- **WHEN** `Stop()` is called twice in succession
- **THEN** no exception is thrown on the second call

#### Scenario: Stop before Load does not throw
- **WHEN** `Stop()` is called on a freshly constructed `AudioEngine` that has never had `Load()` called
- **THEN** no exception is thrown

---

### Requirement: AudioEngine implements IDisposable and cleans up on disposal
`AudioEngine` SHALL implement `IDisposable`. Calling `Dispose()` SHALL have the same effect as `Stop()` if playback is active, releasing all NAudio resources.

#### Scenario: Dispose on a playing engine does not throw
- **WHEN** `Dispose()` is called while `AudioEngine` is in the playing state
- **THEN** no exception is thrown and all resources are released

---

### Requirement: Phase 1 integration harness allows manual multi-stem verification
A minimal WPF window (`MainWindow`) SHALL be implemented for Phase 1 that contains:
- A **Load** button: opens a folder picker, calls `SongScanner.Scan()`, then calls `AudioEngine.Load()` with default `ChannelSettings`
- A **Play** button: calls `AudioEngine.Play()`; disabled until a folder is loaded
- A **Stop** button: calls `AudioEngine.Stop()`
- A status label showing the current state (`Idle`, `Loaded`, `Playing`, `Stopped`)

This harness is temporary and will be replaced by the full operator UI in Phase 5.

#### Scenario: Loading a folder enables the Play button
- **WHEN** the operator clicks Load and selects a folder containing audio files
- **THEN** the Play button becomes enabled and the status label reads "Loaded"

#### Scenario: Playing multiple stems produces synchronised audio output
- **WHEN** the operator clicks Play with three or more WAV stems loaded
- **THEN** all stems are audible simultaneously with no perceptible timing offset (manual verification)
