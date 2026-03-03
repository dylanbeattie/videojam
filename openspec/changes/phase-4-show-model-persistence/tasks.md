## 1. ShowFileException

- [x] 1.1 Create `VideoJam/Model/ShowFileException.cs` — typed exception class inheriting from `Exception` with a single string-message constructor; XML doc comments; in `VideoJam.Services` namespace

## 2. PathResolver Implementation

- [x] 2.1 Implement `PathResolver.MakeRelative(string absoluteTargetPath, string showFileDirectory)` — use `Path.GetRelativePath()`, normalise result to forward slashes
- [x] 2.2 Implement `PathResolver.Resolve(string relativePath, string showFileDirectory)` — combine directory and relative path with `Path.Combine()`, call `Path.GetFullPath()` to normalise `..` segments

## 3. ShowFileService Implementation

- [x] 3.1 Add `JsonSerializerOptions` constant (private static readonly): `WriteIndented = true`, `PropertyNameCaseInsensitive = true`, `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`
- [x] 3.2 Implement `ShowFileService.Save(Show show, string filePath)` — convert `SongEntry.FolderPath` and `FallbackImages` values to relative paths via `PathResolver.MakeRelative()`, serialise to UTF-8 JSON without BOM, write to `<target>.tmp`, rename over target
- [x] 3.3 Implement `ShowFileService.Load(string filePath)` — read bytes, strip UTF-8 BOM if present, deserialise; validate `version == 1`, `songs != null`, `globalDisplayRouting != null`; throw `ShowFileException` with field-specific message on validation failure; store paths as raw relative strings

## 4. SongEntry Factory Method

- [x] 4.1 Implement `SongEntry.CreateFromScan(SongManifest manifest, string showFileDirectory)` static method — set `FolderPath` via `PathResolver.MakeRelative()`, `Name` from `manifest.Folder.Name`, populate `Channels` with per-type defaults (Stem: Level=1.0, Muted=false; VideoAudio: Level=1.0, Muted=true), `DisplayRoutingOverrides` empty

## 5. PathResolver Tests

- [x] 5.1 Create `VideoJam.Tests/PathResolverTests.cs`
- [x] 5.2 Test: target in same directory → single-component result
- [x] 5.3 Test: target in subdirectory → forward-slash relative path
- [x] 5.4 Test: target in sibling directory → `../` prefixed path
- [x] 5.5 Test: result uses forward slashes (no backslashes)
- [x] 5.6 Test: `Resolve()` with simple path
- [x] 5.7 Test: `Resolve()` with parent-traversal (`../`) path
- [x] 5.8 Test: round-trip `Resolve(MakeRelative(abs, dir), dir) == abs`

## 6. ShowFileService Tests

- [x] 6.1 Create `VideoJam.Tests/ShowFileServiceTests.cs`
- [x] 6.2 Test: round-trip — save then load produces identical `Show` (Version, Songs, GlobalDisplayRouting, FallbackImages)
- [x] 6.3 Test: load a valid minimal show (empty songs list) → succeeds
- [x] 6.4 Test: load with missing `version` field → `ShowFileException`
- [x] 6.5 Test: load with `version: 99` → `ShowFileException`
- [x] 6.6 Test: load with missing `songs` field → `ShowFileException`
- [x] 6.7 Test: load with missing `globalDisplayRouting` field → `ShowFileException`
- [x] 6.8 Test: load file with UTF-8 BOM → succeeds
- [x] 6.9 Test: save writes relative paths; load restores raw relative strings (not absolute)
- [x] 6.10 Test: no `.tmp` file remains after successful save

## 7. SongEntry.CreateFromScan Tests

- [x] 7.1 Add `SongEntryCreateFromScanTests` class to a suitable existing or new test file
- [x] 7.2 Test: `FolderPath` is relative to show directory
- [x] 7.3 Test: `Name` equals folder leaf name
- [x] 7.4 Test: stem channel → Level=1.0, Muted=false
- [x] 7.5 Test: video audio channel → Level=1.0, Muted=true
- [x] 7.6 Test: `Channels` keyed by `ChannelId`
- [x] 7.7 Test: `DisplayRoutingOverrides` is empty

## 8. Build & Verify

- [x] 8.1 Ensure solution builds with zero errors and zero warnings
- [x] 8.2 Run all tests — all pass
- [ ] 8.3 Commit on branch `feat/phase-4-show-model-persistence`
