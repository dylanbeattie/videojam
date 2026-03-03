## Why

The engine and UI layers are fully capable of playing media, but there is no way to save or reload a configured show — every run starts from scratch. Phase 4 introduces `.show` file persistence so that a setlist, channel levels, display routing, and fallback images can be authored once and reliably reloaded, even when the project folder is moved to a different machine.

## What Changes

- Implement `ShowFileService.Save()` — atomic UTF-8 JSON serialisation with relative path conversion
- Implement `ShowFileService.Load()` — JSON deserialisation with schema validation and typed `ShowFileException` on failure
- Implement `PathResolver.MakeRelative()` and `PathResolver.Resolve()` — bidirectional conversion between absolute paths and show-file-relative paths
- Implement `SongEntry.CreateFromScan()` — static factory that builds a `SongEntry` from a `SongManifest` with correct channel defaults and relative folder path
- Add `ShowFileException` — a typed exception for show file validation failures
- Unit test all serialisation, validation, and path resolution scenarios

## Capabilities

### New Capabilities
- `show-file-service`: Save and load `.show` files as UTF-8 JSON; validate schema on load; throw `ShowFileException` on invalid input; perform atomic writes (temp-file rename)
- `path-resolver`: Convert absolute paths to show-file-relative paths on save; reverse the conversion on load; normalise separators; round-trip correctness

### Modified Capabilities
- `song-model`: Add `SongEntry.CreateFromScan(SongManifest, string showFileDirectory)` static factory method — this is a behavioural addition to an existing model class, requiring a delta spec

## Impact

- `VideoJam/Services/ShowFileService.cs` — full implementation (was stub)
- `VideoJam/Services/PathResolver.cs` — full implementation (was stub)
- `VideoJam/Model/SongEntry.cs` — add `CreateFromScan` static method
- `VideoJam/Model/ShowFileException.cs` — new file
- `VideoJam.Tests/ShowFileServiceTests.cs` — new test file
- `VideoJam.Tests/PathResolverTests.cs` — new test file
- No new NuGet packages required; `System.Text.Json` is already part of .NET 10
- No breaking changes to existing interfaces
