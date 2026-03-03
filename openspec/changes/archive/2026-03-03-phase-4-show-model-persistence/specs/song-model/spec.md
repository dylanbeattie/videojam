## ADDED Requirements

### Requirement: SongEntry can be created from a SongManifest scan result
The system SHALL provide `SongEntry.CreateFromScan(SongManifest manifest, string showFileDirectory)` as a static factory method that constructs a `SongEntry` with correct defaults from a runtime scan result.

#### Scenario: FolderPath is stored as a relative path
- **WHEN** `CreateFromScan` is called with a manifest whose `Folder` is an absolute path
- **THEN** `SongEntry.FolderPath` is the path relative to `showFileDirectory`, expressed with forward slashes

#### Scenario: Name defaults to the song folder name
- **WHEN** `CreateFromScan` is called with a manifest
- **THEN** `SongEntry.Name` equals `manifest.Folder.Name` (the folder's leaf name, not the full path)

#### Scenario: Stem channels get level 1.0 and muted false
- **WHEN** `CreateFromScan` processes an `AudioChannelManifest` with `Type == AudioChannelType.Stem`
- **THEN** `Channels[channelId].Level == 1.0f` and `Channels[channelId].Muted == false`

#### Scenario: Video audio channels get level 1.0 and muted true
- **WHEN** `CreateFromScan` processes an `AudioChannelManifest` with `Type == AudioChannelType.VideoAudio`
- **THEN** `Channels[channelId].Level == 1.0f` and `Channels[channelId].Muted == true`

#### Scenario: Channels dictionary is keyed by ChannelId from the manifest
- **WHEN** `CreateFromScan` processes audio channels
- **THEN** each entry in `SongEntry.Channels` is keyed by the `AudioChannelManifest.ChannelId` value

#### Scenario: DisplayRoutingOverrides is initialised empty
- **WHEN** `CreateFromScan` is called
- **THEN** `SongEntry.DisplayRoutingOverrides` is a non-null, empty dictionary
