## ADDED Requirements

### Requirement: AudioEngine loads a multi-stem pipeline from a SongManifest
`AudioEngine.Load(SongManifest manifest, IReadOnlyDictionary<string, ChannelSettings> channelSettings)` SHALL construct a NAudio pipeline for all audio channels in the manifest:
- `.wav` and `.mp3` channels use `AudioFileReader`
- `.aiff` channels use `AiffFileReader`
- `.mp4` audio channels use `MediaFoundationReader`

Each reader SHALL be wrapped in a `VolumeSampleProvider`. The initial `Volume` is computed as:
- `0f` if `ChannelSettings.Muted` is `true`
- `ChannelSettings.Level` if the channel has a settings entry and is not muted
- `1.0f` (default) if the channel has no settings entry

The `_volumeProviders` dictionary SHALL be populated during `Load()`, keyed by channel ID, so that `ApplyChannelSettings()` can update volumes without re-reading files.

All providers SHALL be composed into a single `MixingSampleProvider` targeting 44100 Hz, 32-bit float, stereo. The mix SHALL be attached to a `WasapiOut` instance configured for shared mode with a 50ms latency buffer. `Load` SHALL NOT start playback.

#### Scenario: Loading a folder with three WAV stems creates three VolumeSampleProviders
- **WHEN** `Load` is called with a manifest containing three `Stem` channels
- **THEN** the internal pipeline contains three `VolumeSampleProvider` instances feeding a single `MixingSampleProvider`, and `WasapiOut` is initialised but not playing

#### Scenario: Channel level from ChannelSettings is applied to the VolumeSampleProvider
- **WHEN** `Load` is called and `channelSettings["drums.wav"].Level` is `0.5f` and `Muted` is `false`
- **THEN** the `VolumeSampleProvider` for `drums.wav` has `Volume == 0.5f`

#### Scenario: Muted channel gets volume zero during Load
- **WHEN** `Load` is called and `channelSettings["video.mp4:audio"].Muted` is `true`
- **THEN** the `VolumeSampleProvider` for `video.mp4:audio` has `Volume == 0f`

#### Scenario: Channel with no ChannelSettings entry defaults to level 1.0
- **WHEN** `Load` is called and `channelSettings` does not contain an entry for a channel
- **THEN** that channel's `VolumeSampleProvider` has `Volume == 1.0f`

---

### Requirement: AudioEngine.ApplyChannelSettings() re-applies volume levels to the active pipeline
`AudioEngine.ApplyChannelSettings(IReadOnlyDictionary<string, ChannelSettings> channelSettings)` SHALL iterate every entry in the internal `_volumeProviders` dictionary and re-apply the effective volume for each channel. The effective volume is computed identically to `Load()`:
- `0f` if the channel's settings entry has `Muted == true`
- `ChannelSettings.Level` if the entry exists and is not muted
- `1.0f` (default) if the channel has no entry in `channelSettings`

Channel IDs present in `channelSettings` but absent from `_volumeProviders` SHALL be silently ignored. `ApplyChannelSettings()` is called by `PlaybackEngine` immediately before `SyncCoordinator.Start()`, so that any level or mute changes made by the operator after `Cue()` are picked up before the first audio frame is rendered.

#### Scenario: Level applied — provider volume set to specified level
- **WHEN** `ApplyChannelSettings` is called with `channelSettings["drums.wav"].Level == 0.5f` and `Muted == false`, and `"drums.wav"` is in `_volumeProviders`
- **THEN** the `VolumeSampleProvider` for `"drums.wav"` has `Volume == 0.5f`

#### Scenario: Mute sets provider volume to zero
- **WHEN** `ApplyChannelSettings` is called with `channelSettings["keys.wav"].Muted == true`
- **THEN** the `VolumeSampleProvider` for `"keys.wav"` has `Volume == 0f`

#### Scenario: Unmute restores level
- **WHEN** `ApplyChannelSettings` is called with `channelSettings["bass.wav"].Level == 0.75f` and `Muted == false`, and the provider was previously at `0f`
- **THEN** the `VolumeSampleProvider` for `"bass.wav"` has `Volume == 0.75f`

#### Scenario: Unknown channel in settings is ignored
- **WHEN** `ApplyChannelSettings` is called with a settings key that does not exist in `_volumeProviders`
- **THEN** no exception is thrown; the unknown key is silently ignored

#### Scenario: Empty providers dictionary does not throw
- **WHEN** `ApplyChannelSettings` is called before `Load()` (i.e. `_volumeProviders` is empty)
- **THEN** no exception is thrown

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
`AudioEngine` SHALL expose a `PlaybackEnded` event. This event SHALL be raised when `WasapiOut` raises `PlaybackStopped` after the `MixingSampleProvider` has exhausted all input (natural end of all stems). The event SHALL NOT be raised when `Stop()` is called explicitly. The event SHALL be raised on the WPF UI thread (via `Application.Current.Dispatcher.InvokeAsync` or equivalent).

#### Scenario: PlaybackEnded fires after all stems complete naturally
- **WHEN** `Play()` is called and all stems reach their end
- **THEN** `PlaybackEnded` is raised exactly once on the UI thread

#### Scenario: PlaybackEnded does not fire after explicit Stop
- **WHEN** `Stop()` is called while playback is active
- **THEN** `PlaybackEnded` is NOT raised

---

### Requirement: AudioEngine.Stop() halts playback and disposes all resources
`AudioEngine.Stop()` SHALL call `WasapiOut.Stop()`, dispose all audio readers and the `WasapiOut` instance, clear `_volumeProviders`, and reset the engine to an unloaded state. `Stop()` SHALL be safe to call in any state (including before `Load()` or after a previous `Stop()`).

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
