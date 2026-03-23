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

#### Scenario: Save converts absolute FallbackImagePath to a relative path
- **WHEN** `Show.FallbackImagePath` is an absolute path at save time
- **THEN** the JSON contains the path relative to the `.show` file's directory

#### Scenario: Round-trip preserves all fields
- **WHEN** a `Show` is saved to a file and then loaded from that same file
- **THEN** the loaded `Show` has identical `Version`, `Songs`, `FallbackImagePath`, and `VideoWindowLayouts` values

---

### Requirement: Show files are deserialised with schema validation
The system SHALL provide `ShowFileService.Load(string filePath)` which reads and deserialises a `.show` JSON file. It SHALL throw `ShowFileException` with a descriptive message if any required field is missing or invalid.

Both schema version 1 and version 2 are accepted. Version 1 files are migrated to version 2 on load (see migration requirement below). Version numbers higher than 2 SHALL be rejected.

#### Scenario: Load succeeds for a valid v2 show file
- **WHEN** a JSON file containing `version: 2`, an empty `songs` array, an empty `videoWindowLayouts` object, and a null/absent `fallbackImagePath` is loaded
- **THEN** a `Show` instance is returned with `Version == 2`, `Songs` empty, `VideoWindowLayouts` empty

#### Scenario: Load succeeds for a valid v1 show file (migrated)
- **WHEN** a JSON file containing `version: 1`, a `globalDisplayRouting` object, and a `songs` array is loaded
- **THEN** a `Show` instance is returned with `Version == 2` (migrated), `GlobalDisplayRouting` stripped

#### Scenario: Missing version field throws ShowFileException
- **WHEN** the JSON file has no `version` field
- **THEN** `ShowFileException` is thrown with a message referencing `version`

#### Scenario: Unsupported version number throws ShowFileException
- **WHEN** the JSON file has `version: 99`
- **THEN** `ShowFileException` is thrown with a message indicating the version is unsupported

#### Scenario: Missing songs field throws ShowFileException
- **WHEN** the JSON file has no `songs` field
- **THEN** `ShowFileException` is thrown with a message referencing `songs`

#### Scenario: UTF-8 file with BOM loads successfully
- **WHEN** the JSON file is encoded as UTF-8 with a byte-order mark
- **THEN** the file loads successfully without throwing

#### Scenario: Load stores FolderPath values as raw relative strings
- **WHEN** a `.show` file is loaded whose songs have relative `folderPath` values
- **THEN** `SongEntry.FolderPath` contains the raw relative string (not resolved to absolute)

---

### Requirement: ShowFileService migrates v1 show files to v2 on load
When a version 1 `.show` file is loaded, the service SHALL silently migrate it to the v2 schema:
- Remove the `globalDisplayRouting` object
- Remove `displayRoutingOverrides` from every song entry
- Promote the first value in `fallbackImages` (if present) to `fallbackImagePath`; if `fallbackImages` is absent or empty, set `fallbackImagePath` to `null`
- Set `videoWindowLayouts` to an empty dictionary
- Set `Version` to `2`

The migration is in-memory only — the `.show` file on disk is NOT rewritten by `Load()`. It will only be updated when the operator explicitly saves.

#### Scenario: v1 file with globalDisplayRouting migrates cleanly
- **WHEN** a v1 `.show` file with `globalDisplayRouting: {"_lyrics": 1}` is loaded
- **THEN** the returned `Show` has no `GlobalDisplayRouting` property and `Version == 2`

#### Scenario: v1 file with fallbackImages promotes first value to FallbackImagePath
- **WHEN** a v1 `.show` file with `fallbackImages: {"0": "images/bg.png", "1": "images/bg2.png"}` is loaded
- **THEN** the returned `Show` has `FallbackImagePath == "images/bg.png"` (first value promoted)

#### Scenario: v1 file with no fallbackImages sets FallbackImagePath to null
- **WHEN** a v1 `.show` file with no `fallbackImages` field is loaded
- **THEN** the returned `Show` has `FallbackImagePath == null`

#### Scenario: v1 file with displayRoutingOverrides strips them from songs
- **WHEN** a v1 `.show` file has song entries with `displayRoutingOverrides` fields
- **THEN** the loaded `SongEntry` objects have no `DisplayRoutingOverrides` property

---

### Requirement: ShowFileException is a typed exception for show file failures
The system SHALL define `ShowFileException : Exception` in the `VideoJam.Services` namespace, used exclusively to report `.show` file validation and parsing failures.

#### Scenario: ShowFileException carries a descriptive message
- **WHEN** `ShowFileException` is thrown by `Load()`
- **THEN** the `Message` property describes which field failed validation

#### Scenario: ShowFileException is catchable independently of other exceptions
- **WHEN** a caller wraps `Load()` in `catch (ShowFileException ex)`
- **THEN** only show-file failures are caught; unrelated exceptions propagate normally
