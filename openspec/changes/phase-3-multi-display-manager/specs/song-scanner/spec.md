## MODIFIED Requirements

### Requirement: SongScanner classifies MP4 files as video + video audio

`SongScanner.Scan` SHALL classify files with the `.mp4` extension (case-insensitive) as producing both a `VideoFileManifest` entry and an `AudioChannelManifest` with `Type = VideoAudio`. Both entries SHALL reference the file via a `FileInfo`. The `ChannelId` for the audio channel SHALL be `"{filename}:audio"`. The `VideoFileManifest.Suffix` SHALL be the underscore-prefixed portion of the filename before the extension (e.g. `opening_visuals_lyrics.mp4` → suffix `_lyrics`). If no underscore is present in the filename, `Suffix` SHALL be an empty string.

`SongScanner.Scan` SHALL accept an optional second parameter `IReadOnlyDictionary<string, int>? displayRouting`. Each MP4 file's `VideoFileManifest.DisplayIndex` SHALL be resolved by calling `DisplayManager.ResolveDisplayIndex(suffix, displayRouting ?? empty)`. If `displayRouting` is `null` or omitted, all video files SHALL receive `DisplayManager.PrimaryDisplayIndex`.

#### Scenario: MP4 with a suffix produces a VideoFileManifest and a VideoAudio channel
- **WHEN** a folder contains `show_visuals.mp4`
- **THEN** the manifest contains a `VideoFileManifest` with `Suffix = "_visuals"` and an `AudioChannelManifest` with `ChannelId = "show_visuals.mp4:audio"` and `Type = VideoAudio`

#### Scenario: MP4 with no underscore suffix has an empty Suffix
- **WHEN** a folder contains `performance.mp4`
- **THEN** the manifest contains a `VideoFileManifest` with `Suffix = ""`

#### Scenario: Multiple MP4 files each produce independent entries
- **WHEN** a folder contains `track_lyrics.mp4` and `track_visuals.mp4`
- **THEN** the manifest contains two `VideoFileManifest` entries with suffixes `_lyrics` and `_visuals` respectively, and two `AudioChannelManifest` entries of type `VideoAudio`

#### Scenario: Display index resolved from routing when suffix matches
- **WHEN** `Scan` is called with a folder containing `show_lyrics.mp4` and `displayRouting = {"_lyrics": 2}`
- **THEN** the resulting `VideoFileManifest` has `DisplayIndex = 2`

#### Scenario: Display index falls back to primary when suffix absent from routing
- **WHEN** `Scan` is called with a folder containing `show_visuals.mp4` and `displayRouting = {"_lyrics": 2}`
- **THEN** the resulting `VideoFileManifest` has `DisplayIndex = DisplayManager.PrimaryDisplayIndex`

#### Scenario: Display index falls back to primary when no routing provided
- **WHEN** `Scan` is called with a folder containing `show_visuals.mp4` and no `displayRouting` argument
- **THEN** the resulting `VideoFileManifest` has `DisplayIndex = DisplayManager.PrimaryDisplayIndex`
