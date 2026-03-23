## ADDED Requirements

### Requirement: Playback toolbar in MainWindow
`MainWindow` SHALL include a `ToolBar` row docked below the menu bar and above the main content panel. The toolbar SHALL always be visible regardless of playback state.

The toolbar SHALL contain:
- A **Go** button (▶) bound to `GoCommand`. Enabled only when `PlaybackState == Cued` and `IsLoading == false`.
- A **Pause** button (⏸) bound to `StopAndRewindCommand`. Visible and enabled only when `PlaybackState == Playing`.
- A **Stop / Rewind** button (⏮) bound to `StopAndRewindCommand`. Visible and enabled only when `PlaybackState == Paused`.
- A **loading indicator** (e.g. italicised text "Loading…") visible only when `IsLoading == true`.

The Pause and Stop/Rewind buttons are mutually exclusive in visibility — at most one SHALL be visible at any time.

#### Scenario: Go button enabled when song is cued
- **WHEN** `PlaybackState == Cued` and `IsLoading == false`
- **THEN** the Go button SHALL be enabled and clickable

#### Scenario: Go button disabled when not cued
- **WHEN** `PlaybackState == Idle` or `PlaybackState == Playing` or `PlaybackState == Paused`
- **THEN** the Go button SHALL be disabled

#### Scenario: Pause button shown during playback
- **WHEN** `PlaybackState == Playing`
- **THEN** the Pause button (⏸) SHALL be visible and enabled; the Stop/Rewind button SHALL be hidden

#### Scenario: Stop/Rewind button shown when paused
- **WHEN** `PlaybackState == Paused`
- **THEN** the Stop/Rewind button (⏮) SHALL be visible and enabled; the Pause button SHALL be hidden

#### Scenario: Both transport buttons hidden when idle or cued
- **WHEN** `PlaybackState == Idle` or `PlaybackState == Cued`
- **THEN** both the Pause and Stop/Rewind buttons SHALL be hidden (Collapsed)

#### Scenario: Loading indicator visible during cue operation
- **WHEN** `IsLoading == true`
- **THEN** the loading indicator SHALL be visible in the toolbar

#### Scenario: Toolbar buttons invoke correct commands
- **WHEN** the operator clicks the Go button while `PlaybackState == Cued`
- **THEN** `GoCommand.Execute()` is called and playback begins

#### Scenario: Pause button invokes stop/rewind command
- **WHEN** the operator clicks the Pause button while `PlaybackState == Playing`
- **THEN** `StopAndRewindCommand.Execute()` is called and `PlaybackState` transitions to `Paused`
