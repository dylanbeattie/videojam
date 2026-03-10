## ADDED Requirements

### Requirement: Mixer panel displays channels for the selected song
The mixer panel SHALL display one row per `ChannelSettings` entry in `MainViewModel.SelectedSong.Channels`. Each row SHALL contain:
- The channel name (derived from the channel ID)
- A `Slider` control bound to `ChannelSettings.Level` (range 0.0–1.0)
- A `CheckBox` bound to `ChannelSettings.Muted`

When `SelectedSong` is `null`, the mixer panel SHALL display a placeholder message (e.g., "Select a song to view channels").

#### Scenario: Channels shown for selected song
- **WHEN** a song with three audio channels is selected
- **THEN** the mixer panel SHALL display three rows, one per channel, with the channel name, level slider, and mute checkbox

#### Scenario: Mixer shows placeholder when no song selected
- **WHEN** `SelectedSong` is `null`
- **THEN** the mixer panel SHALL display a placeholder and no channel rows

---

### Requirement: Level slider updates model
Moving the level `Slider` for a channel SHALL update `ChannelSettings.Level` on the underlying model object and SHALL set `MainViewModel.HasUnsavedChanges` to `true`.

#### Scenario: Level change marks show dirty
- **WHEN** the operator moves a level slider from 1.0 to 0.5
- **THEN** `ChannelSettings.Level` SHALL be `0.5` and `HasUnsavedChanges` SHALL be `true`

---

### Requirement: Mute checkbox updates model
Toggling the mute `CheckBox` for a channel SHALL update `ChannelSettings.Muted` and SHALL set `MainViewModel.HasUnsavedChanges` to `true`.

#### Scenario: Mute toggle marks show dirty
- **WHEN** the operator checks the mute checkbox for a channel
- **THEN** `ChannelSettings.Muted` SHALL be `true` and `HasUnsavedChanges` SHALL be `true`

---

### Requirement: Mixer controls disabled during playback
All channel controls (sliders and checkboxes) in the mixer panel SHALL be disabled (`IsEnabled = false`) when `MainViewModel.PlaybackState` is `Playing` or `Paused`. They SHALL be re-enabled when `PlaybackState` returns to `Idle` or `Cued`.

#### Scenario: Controls locked while playing
- **WHEN** `PlaybackState` is `Playing`
- **THEN** all level sliders and mute checkboxes SHALL be disabled and non-interactive

#### Scenario: Controls enabled when idle
- **WHEN** `PlaybackState` is `Idle`
- **THEN** all level sliders and mute checkboxes SHALL be enabled

---

### Requirement: Video audio channels visually distinguished
Channel rows whose channel ID ends with `:audio` (video audio tracks) SHALL be visually distinguished from stem channels. The implementation SHALL use italic channel name text or a camera icon glyph (Unicode `🎬` or a simple `[V]` text prefix) — exact styling is at implementer discretion within MVP constraints.

#### Scenario: Video audio channel renders differently
- **WHEN** a song contains a channel with ID `visuals.mp4:audio`
- **THEN** that channel row SHALL have a visual distinction (italic or icon) compared to plain stem rows
