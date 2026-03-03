## ADDED Requirements

### Requirement: Show files are serialised to UTF-8 JSON atomically
The system SHALL provide `ShowFileService.Save(Show show, string filePath)` which serialises the `Show` object to indented UTF-8 JSON (without BOM) and writes it atomically by first writing to a `.tmp` sibling file in the same directory, then renaming over the target.

#### Scenario: Save produces valid JSON at the target path
- **WHEN** `ShowFileService.Save(show, "/some/path/setlist.show")` is called with a valid `Show`
- **THEN** the file at `/some/path/setlist.show` exists and contains valid JSON representing the show

#### Scenario: Atomic write does not leave a temp file on success
- **WHEN** `Save()` completes successfully
- **THEN** no `.tmp` file remains alongside the target file

#### Scenario: Save converts absolute FolderPath values to relative paths
- **WHEN** a `SongEntry.FolderPath` is an absolute path at save time
- **THEN** the JSON contains the path relative to the `.show` file's directory

#### Scenario: Save converts absolute FallbackImages values to relative paths
- **WHEN** `Show.FallbackImages` contains absolute path values at save time
- **THEN** the JSON contains paths relative to the `.show` file's directory

#### Scenario: Round-trip preserves all fields
- **WHEN** a `Show` is saved to a file and then loaded from that same file
- **THEN** the loaded `Show` has identical `Version`, `Songs`, `GlobalDisplayRouting`, and `FallbackImages` values

---

### Requirement: Show files are deserialised with schema validation
The system SHALL provide `ShowFileService.Load(string filePath)` which reads and deserialises a `.show` JSON file. It SHALL throw `ShowFileException` with a descriptive message if any required field is missing or invalid.

#### Scenario: Load succeeds for a valid minimal show file
- **WHEN** a JSON file containing `version: 1`, an empty `songs` array, and a `globalDisplayRouting` object is loaded
- **THEN** a `Show` instance is returned with `Version == 1`, `Songs` empty, `GlobalDisplayRouting` empty

#### Scenario: Missing version field throws ShowFileException
- **WHEN** the JSON file has no `version` field
- **THEN** `ShowFileException` is thrown with a message referencing `version`

#### Scenario: Unsupported version number throws ShowFileException
- **WHEN** the JSON file has `version: 99`
- **THEN** `ShowFileException` is thrown with a message indicating the version is unsupported

#### Scenario: Missing songs field throws ShowFileException
- **WHEN** the JSON file has no `songs` field
- **THEN** `ShowFileException` is thrown with a message referencing `songs`

#### Scenario: Missing globalDisplayRouting field throws ShowFileException
- **WHEN** the JSON file has no `globalDisplayRouting` field
- **THEN** `ShowFileException` is thrown with a message referencing `globalDisplayRouting`

#### Scenario: UTF-8 file with BOM loads successfully
- **WHEN** the JSON file is encoded as UTF-8 with a byte-order mark
- **THEN** the file loads successfully without throwing

#### Scenario: Load stores FolderPath values as raw relative strings
- **WHEN** a `.show` file is loaded whose songs have relative `folderPath` values
- **THEN** `SongEntry.FolderPath` contains the raw relative string (not resolved to absolute)

---

### Requirement: ShowFileException is a typed exception for show file failures
The system SHALL define `ShowFileException : Exception` in the `VideoJam.Services` namespace, used exclusively to report `.show` file validation and parsing failures.

#### Scenario: ShowFileException carries a descriptive message
- **WHEN** `ShowFileException` is thrown by `Load()`
- **THEN** the `Message` property describes which field failed validation

#### Scenario: ShowFileException is catchable independently of other exceptions
- **WHEN** a caller wraps `Load()` in `catch (ShowFileException ex)`
- **THEN** only show-file failures are caught; unrelated exceptions propagate normally
