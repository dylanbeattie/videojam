## ADDED Requirements

### Requirement: Runtime song manifest records are defined
The system SHALL define the following immutable record types for in-memory use (not persisted):
- `SongManifest(string SongName, DirectoryInfo Folder, IReadOnlyList<AudioChannelManifest> AudioChannels, IReadOnlyList<VideoFileManifest> VideoFiles)`
- `AudioChannelManifest(FileInfo File, string ChannelId, AudioChannelType Type)` where `File` refers to the media file on disk and `ChannelId` is the channel identifier used as a key in the show file
- `VideoFileManifest(FileInfo File, int DisplayIndex, string Suffix)` where `File` refers to the video file on disk and `Suffix` is the underscore-prefixed filename suffix (e.g. `_lyrics`)
- `AudioChannelType` enum with values `Stem` and `VideoAudio`

#### Scenario: Records are value-equal when constructed with identical arguments
- **WHEN** two `AudioChannelManifest` instances are created with the same `File`, `ChannelId`, and `Type`
- **THEN** they are considered equal (C# record equality)

#### Scenario: Channel ID format for a stem file
- **WHEN** a stem file named `drums.wav` is represented as an `AudioChannelManifest`
- **THEN** `ChannelId` is `"drums.wav"` (filename only, no path)

#### Scenario: Channel ID format for a video audio channel
- **WHEN** a video file named `opening_visuals.mp4` is represented as an `AudioChannelManifest`
- **THEN** `ChannelId` is `"opening_visuals.mp4:audio"`

---

### Requirement: Persisted show model classes are defined
The system SHALL define the following mutable classes for JSON persistence:
- `Show` with properties: `int Version`, `List<SongEntry> Songs`, `Dictionary<string, int> GlobalDisplayRouting`, `Dictionary<int, string> FallbackImages`
- `SongEntry` with properties: `string FolderPath`, `string Name`, `Dictionary<string, int> DisplayRoutingOverrides`, `Dictionary<string, ChannelSettings> Channels`
- `ChannelSettings` with properties: `float Level`, `bool Muted`

> **Note:** `SongEntry.FolderPath` and the values in `Show.FallbackImages` are intentionally typed as `string`. These are *relative* paths stored in the JSON `.show` file; they are not resolvable in isolation and require the `.show` file's own directory to be combined via `PathResolver` before a `DirectoryInfo` or `FileInfo` can be constructed from them. Path resolution is a Phase 4 concern.

#### Scenario: Show has a version field defaulting to 1
- **WHEN** a new `Show` instance is constructed with `new Show()`
- **THEN** `Version` is `1`

#### Scenario: SongEntry channel dictionary is initialised empty
- **WHEN** a new `SongEntry` is constructed
- **THEN** `Channels` is a non-null, empty `Dictionary<string, ChannelSettings>`

---

### Requirement: ChannelSettings defaults are correct by channel type
When a `SongEntry` is created from a `SongManifest` scan, each audio channel SHALL have `ChannelSettings` with defaults appropriate to its type.

#### Scenario: Stem channel gets default level and unmuted state
- **WHEN** a `ChannelSettings` entry is created for an `AudioChannelType.Stem` channel
- **THEN** `Level` is `1.0f` and `Muted` is `false`

#### Scenario: Video audio channel gets default level and muted state
- **WHEN** a `ChannelSettings` entry is created for an `AudioChannelType.VideoAudio` channel
- **THEN** `Level` is `1.0f` and `Muted` is `true`

#### Scenario: ChannelSettings default constructor produces stem defaults
- **WHEN** `new ChannelSettings()` is called with no arguments
- **THEN** `Level` is `1.0f` and `Muted` is `false`
