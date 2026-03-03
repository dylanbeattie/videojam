## ADDED Requirements

### Requirement: PathResolver converts absolute paths to show-file-relative paths
The system SHALL provide `PathResolver.MakeRelative(string absoluteTargetPath, string showFileDirectory)` which returns the path of `absoluteTargetPath` expressed relative to `showFileDirectory`, using forward slashes as separators.

#### Scenario: Target in the same directory as the show file
- **WHEN** `MakeRelative("C:/shows/song1", "C:/shows")` is called
- **THEN** the result is `"song1"`

#### Scenario: Target in a subdirectory of the show file directory
- **WHEN** `MakeRelative("C:/shows/songs/song1", "C:/shows")` is called
- **THEN** the result is `"songs/song1"`

#### Scenario: Target in a sibling directory
- **WHEN** `MakeRelative("C:/media/songs/song1", "C:/shows")` is called
- **THEN** the result is `"../media/songs/song1"`

#### Scenario: Result uses forward slashes regardless of OS separator
- **WHEN** `MakeRelative()` is called on Windows where `Path.GetRelativePath()` returns backslashes
- **THEN** the returned string contains only forward slashes

---

### Requirement: PathResolver resolves relative paths to absolute paths
The system SHALL provide `PathResolver.Resolve(string relativePath, string showFileDirectory)` which combines `showFileDirectory` with `relativePath` and returns a normalised absolute path.

#### Scenario: Simple single-component relative path resolves correctly
- **WHEN** `Resolve("song1", "C:/shows")` is called
- **THEN** the result is `"C:\shows\song1"` (or the OS-normalised equivalent)

#### Scenario: Subdirectory relative path resolves correctly
- **WHEN** `Resolve("songs/song1", "C:/shows")` is called
- **THEN** the result is `"C:\shows\songs\song1"`

#### Scenario: Parent-traversal relative path resolves correctly
- **WHEN** `Resolve("../media/songs/song1", "C:/shows")` is called
- **THEN** the result is `"C:\media\songs\song1"` with no `..` segments remaining

#### Scenario: Forward-slash paths are accepted on Windows
- **WHEN** `Resolve("songs/song1", "C:/shows")` is called on Windows
- **THEN** the method succeeds and returns a valid absolute path

---

### Requirement: PathResolver round-trip is lossless
Resolving a relative path that was produced by `MakeRelative` SHALL return the original absolute path.

#### Scenario: Round-trip recovers the original absolute path
- **WHEN** `relPath = MakeRelative(absPath, dir)` and then `Resolve(relPath, dir)` is called
- **THEN** the result equals `absPath` (after OS path normalisation)
