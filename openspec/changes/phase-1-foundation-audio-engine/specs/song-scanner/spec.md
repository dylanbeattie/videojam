## ADDED Requirements

### Requirement: SongScanner classifies audio stem files by extension
`SongScanner.Scan(DirectoryInfo folder)` SHALL classify files with extensions `.wav`, `.mp3`, and `.aiff` (case-insensitive) as `AudioChannelType.Stem`. Each SHALL produce one `AudioChannelManifest` with `Type = Stem`, `File` set to a `FileInfo` for the discovered file, and `ChannelId` set to the filename (no directory component).

#### Scenario: WAV file produces a Stem channel
- **WHEN** a folder contains `drums.wav`
- **THEN** the returned `SongManifest` contains an `AudioChannelManifest` with `ChannelId = "drums.wav"` and `Type = Stem`

#### Scenario: MP3 file produces a Stem channel
- **WHEN** a folder contains `bass.mp3`
- **THEN** the returned `SongManifest` contains an `AudioChannelManifest` with `ChannelId = "bass.mp3"` and `Type = Stem`

#### Scenario: AIFF file produces a Stem channel
- **WHEN** a folder contains `keys.aiff`
- **THEN** the returned `SongManifest` contains an `AudioChannelManifest` with `ChannelId = "keys.aiff"` and `Type = Stem`

#### Scenario: Extension matching is case-insensitive
- **WHEN** a folder contains `DRUMS.WAV` and `Bass.Mp3`
- **THEN** both produce `Stem` channels and neither is ignored

---

### Requirement: SongScanner classifies MP4 files as video + video audio
`SongScanner.Scan` SHALL classify files with the `.mp4` extension (case-insensitive) as producing both a `VideoFileManifest` entry and an `AudioChannelManifest` with `Type = VideoAudio`. Both entries SHALL reference the file via a `FileInfo`. The `ChannelId` for the audio channel SHALL be `"{filename}:audio"`. The `VideoFileManifest.Suffix` SHALL be the underscore-prefixed portion of the filename before the extension (e.g. `opening_visuals_lyrics.mp4` → suffix `_lyrics`). If no underscore is present in the filename, `Suffix` SHALL be an empty string.

#### Scenario: MP4 with a suffix produces a VideoFileManifest and a VideoAudio channel
- **WHEN** a folder contains `show_visuals.mp4`
- **THEN** the manifest contains a `VideoFileManifest` with `Suffix = "_visuals"` and an `AudioChannelManifest` with `ChannelId = "show_visuals.mp4:audio"` and `Type = VideoAudio`

#### Scenario: MP4 with no underscore suffix has an empty Suffix
- **WHEN** a folder contains `performance.mp4`
- **THEN** the manifest contains a `VideoFileManifest` with `Suffix = ""`

#### Scenario: Multiple MP4 files each produce independent entries
- **WHEN** a folder contains `track_lyrics.mp4` and `track_visuals.mp4`
- **THEN** the manifest contains two `VideoFileManifest` entries with suffixes `_lyrics` and `_visuals` respectively, and two `AudioChannelManifest` entries of type `VideoAudio`

---

### Requirement: SongScanner ignores unrecognised file types
Files with extensions other than `.wav`, `.mp3`, `.aiff`, and `.mp4` SHALL be silently ignored by `SongScanner.Scan(DirectoryInfo folder)`. No error, warning, or exception is raised for unrecognised files.

#### Scenario: Non-media files are ignored
- **WHEN** a folder contains `setlist.txt`, `cover.png`, `notes.pdf`, and `readme.md`
- **THEN** the returned `SongManifest` contains no audio channels and no video files

#### Scenario: Mixed folder — only media files are classified
- **WHEN** a folder contains `drums.wav`, `notes.txt`, and `cover.jpg`
- **THEN** only `drums.wav` produces a channel; `notes.txt` and `cover.jpg` are silently ignored

---

### Requirement: SongScanner handles an empty folder
`SongScanner.Scan` SHALL return a valid `SongManifest` with empty collections when the target folder contains no files.

#### Scenario: Empty folder produces an empty manifest
- **WHEN** `Scan` is called on a `DirectoryInfo` pointing to a folder containing no files
- **THEN** the returned manifest has zero `AudioChannels` and zero `VideoFiles`, and no exception is thrown

---

### Requirement: SongScanner sets SongName and Folder on the manifest
`SongScanner.Scan` SHALL set `SongManifest.SongName` to `folder.Name` (the folder name, not the full path) and `SongManifest.Folder` to the `DirectoryInfo` passed in.

#### Scenario: SongName is the folder name only
- **WHEN** `Scan` is called with a `DirectoryInfo` whose `FullName` is `C:\songs\01_OpeningTrack`
- **THEN** `SongManifest.SongName` is `"01_OpeningTrack"` and `SongManifest.Folder.FullName` is `"C:\songs\01_OpeningTrack"`
