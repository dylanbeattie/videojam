## Context

VideoJam already has stub implementations of `ShowFileService` and `PathResolver` (empty classes) and fully-defined model classes (`Show`, `SongEntry`, `ChannelSettings`). Phase 3 is complete; the engine can play media across multiple displays. The missing piece is persistence — without `.show` files there is no way to save a setlist or reload it between sessions.

`System.Text.Json` is already on the classpath (part of .NET 10). No new packages are needed. Tests use real temp-folder I/O (no mocking library is in use in this project).

## Goals / Non-Goals

**Goals:**
- Implement `ShowFileService.Save()` and `ShowFileService.Load()` with full path relativisation and schema validation
- Implement `PathResolver.MakeRelative()` and `PathResolver.Resolve()` with Windows-correct path handling
- Add `SongEntry.CreateFromScan()` factory method
- Add typed `ShowFileException` for validation failures
- Unit test all public surface of the above

**Non-Goals:**
- UI for opening/saving show files (Phase 5)
- Auto-save or file-watching
- Migration between schema versions (only version 1 exists)
- Non-Windows path separator support (app targets win-x64 exclusively)
- Async file I/O (show files are small; sync I/O is acceptable and simpler to test)

## Decisions

### D1: `System.Text.Json` with `JsonSerializerOptions` — not Newtonsoft

**Decision:** Use `System.Text.Json` (inbox, no extra dependency).
**Rationale:** Already present in .NET 10. The model classes are simple POCOs with no polymorphic serialisation needs. `System.Text.Json` is faster and avoids a third-party dependency.
**Alternative considered:** Newtonsoft.Json — richer but unnecessary here; would add a NuGet dependency that requires user approval.

### D2: Atomic write via temp-file rename

**Decision:** `Save()` writes to `<target>.tmp` in the same directory, then calls `File.Replace()` (or `File.Move(dest, overwrite: true)`) to atomically swap.
**Rationale:** Prevents a half-written `.show` file if the process crashes mid-write. Keeps the last good file intact.
**Alternative considered:** Write directly — simpler but risks corruption on crash.

### D3: `PathResolver` is a static utility class, not an injected service

**Decision:** Both `MakeRelative` and `Resolve` are `public static` methods.
**Rationale:** Pure functions with no external state or I/O. No benefit to making them instance methods or injecting them; callers can call them directly. Makes unit tests trivial (no setup).
**Alternative considered:** Interface + DI — adds indirection without benefit for pure path math.

### D4: Relative paths stored raw in the model; resolution deferred to point of use

**Decision:** `SongEntry.FolderPath` and `Show.FallbackImages` values are stored as raw relative-path strings after load. `PathResolver.Resolve()` is called at the point of use (e.g., when `SongScanner` needs to open the folder).
**Rationale:** Keeps the model serialisation-isomorphic — what you load is exactly what was in the JSON. Avoids needing the show-file directory at model-construction time. Consistent with the existing spec note in `song-model`.
**Alternative considered:** Resolve to absolute immediately on load — simpler for callers but couples the model to its load-time location and breaks in-memory construction.

### D5: `ShowFileException` is a checked custom exception, not `InvalidOperationException`

**Decision:** A dedicated `ShowFileException : Exception` class in the `VideoJam.Services` namespace.
**Rationale:** Allows callers to catch show-file problems specifically without catching unrelated exceptions. Carries a descriptive message about which validation failed.
**Alternative considered:** `InvalidOperationException` — too broad; callers can't distinguish it from engine errors.

### D6: `JsonSerializerOptions` uses `PropertyNameCaseInsensitive = true` and `WriteIndented = true`

**Decision:** Case-insensitive reading; indented writing.
**Rationale:** Case-insensitive deserialization is more resilient to hand-edited `.show` files. Indented JSON is human-readable — these files will be inspected and sometimes authored by hand.

## Risks / Trade-offs

- **[Risk] Windows path separator in JSON** — `Path.GetRelativePath()` returns backslashes on Windows. If a `.show` file is moved to a non-Windows machine the paths would be wrong.
  **Mitigation:** Normalise to forward slashes on save; `PathResolver.Resolve()` normalises back. App is win-x64 only so this is a future concern, not a current one — document it.

- **[Risk] BOM in UTF-8 files** — Some editors write UTF-8 BOM. `System.Text.Json` rejects BOM by default.
  **Mitigation:** Use `new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)` for writing; use `JsonDocument` or strip BOM before deserialization. The spec explicitly requires BOM-resilience.

- **[Risk] `File.Replace()` across volumes** — Atomic rename fails if temp file and target are on different drives.
  **Mitigation:** Write temp file to the same directory as the target (guaranteed same volume). Document the constraint.

## Open Questions

_None — all design choices are resolved above._
