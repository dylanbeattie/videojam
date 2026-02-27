## Context

Phase 2 hardcodes every video file to `DisplayIndex = 0` in `SongScanner` and `MainWindow`. The `VideoEngine` already supports N display slots via its thread-safe `_slots` list, but nothing above it actually populates more than one slot. Phase 3 bridges that gap by introducing `DisplayManager` — a lightweight utility that maps physical `Screen` objects to display indices, resolves suffix-based routing, and creates correctly-sized `VlcDisplayWindow` instances. `SongScanner` gains an optional routing parameter so it can stamp correct display indices onto manifests at scan time, and `VideoEngine` gains `LoadAll()` to drive concurrent multi-display pre-buffering.

The `System.Windows.Forms.Screen` API is already in scope (it was used in the Phase 2 harness to get `PrimaryScreen`). No new packages are required.

## Goals / Non-Goals

**Goals:**
- A `DisplayManager` class that enumerates physical displays, resolves suffix → display index, and produces correctly-positioned `VlcDisplayWindow` instances
- `SongScanner.Scan()` resolves each MP4's `DisplayIndex` from a routing dictionary at scan time; defaults to primary display if suffix is absent from the routing table
- `VideoEngine.LoadAll()` fires `Load()` concurrently for every display slot in the manifest and awaits them all via `Task.WhenAll`
- `PrimaryDisplayIndex = 0` retired from `SongScanner` and `MainWindow`; promoted to `DisplayManager.PrimaryDisplayIndex`
- Phase 2 harness extended to wire routing and multi-display loading

**Non-Goals:**
- Per-song routing overrides (Phase 4)
- Fallback PNG assignment (Phase 4)
- Stable display identity via EDID or device name (deferred — see risks)
- Hot-plug display detection
- Operator UI

## Decisions

### Decision 1 — `DisplayManager` is a stateless static utility, not a service

`DisplayManager` will be a `static class` with no instance state. It provides:
- `PrimaryDisplayIndex` — named constant (`0`)
- `ResolveDisplayIndex(string suffix, IReadOnlyDictionary<string, int> routing)` — looks up suffix in routing; returns `PrimaryDisplayIndex` if absent
- `CreateWindowForDisplay(int displayIndex)` — finds `Screen.AllScreens[displayIndex]`, computes DIP bounds, constructs, positions, and returns a `VlcDisplayWindow`
- `GetRequiredDisplayIndices(SongManifest manifest)` — returns the distinct set of display indices referenced by video files in the manifest

*Alternative considered:* A stateful `IDisposable` service that owns the window pool. Rejected — window lifecycle is already managed by the harness (which holds `_displayWindow` references and calls `Close()`). Owning windows inside `DisplayManager` would add a second lifecycle owner and complicate disposal ordering.

*Alternative considered:* Injecting `DisplayManager` as an interface for testability. Rejected — there is nothing to mock in Phase 3 (no I/O, no side effects except WPF window creation, which is not tested in unit tests). A static class is simpler and sufficient.

### Decision 2 — `SongScanner.Scan()` gains an optional `displayRouting` parameter

Signature change:
```csharp
public static SongManifest Scan(
    DirectoryInfo folder,
    IReadOnlyDictionary<string, int>? displayRouting = null)
```

When `displayRouting` is `null` or empty, every MP4 falls back to `DisplayManager.PrimaryDisplayIndex` — identical to current behaviour. Existing call sites (tests, Phase 2 harness) require no changes.

*Alternative considered:* A separate `ResolveDisplayIndices(SongManifest, routing)` post-processing step. Rejected — it would leave `SongManifest` in a transiently invalid state (indices all 0) between scan and resolution, and force callers to remember the two-step sequence. Resolving at scan time is simpler and keeps the manifest always correct.

### Decision 3 — `VideoEngine.LoadAll()` drives concurrency, not the harness

```csharp
public Task LoadAll(
    SongManifest manifest,
    IReadOnlyDictionary<int, VlcDisplayWindow> windows,
    CancellationToken cancellationToken = default)
```

Internally calls `Load()` for each `(displayIndex, window)` pair concurrently and returns `Task.WhenAll(...)`. The harness calls `LoadAll()` with the window map produced by `DisplayManager`.

*Alternative considered:* Keeping the concurrency in the harness (multiple `await` calls on `Load()`). Rejected — the harness would need to know about display-index-to-window mapping in detail, duplicating logic that belongs inside the engine layer. A single `LoadAll()` keeps the harness thin.

*Note:* Partial failure is already handled — if a `Load()` times out for one display, it logs a warning and returns without adding a slot. `Task.WhenAll` will still complete; the remaining displays play normally. Audio is unaffected.

### Decision 4 — Window creation is deferred to the harness flow, not construction time

`DisplayManager.CreateWindowForDisplay(int displayIndex)` creates and shows a `VlcDisplayWindow` only when called. The harness calls it once per required display index just before `LoadAll()`. This means no windows are created until the user actually presses Load, which avoids ghost windows appearing on all displays at application start.

### Decision 5 — Display index = `Screen.AllScreens` array position

`Screen.AllScreens[0]` is the primary display, matching `PrimaryDisplayIndex = 0`. Higher indices map to secondary, tertiary displays in the order Windows reports them. This is the simplest stable-within-session mapping and is consistent with how Phase 2 already uses `Screen.PrimaryScreen`.

The known instability (index order may change across reboots) is documented in the proposal as a risk and deferred. For Phase 3, in-session stability is sufficient.

## Risks / Trade-offs

| Risk | Mitigation |
|------|-----------|
| `Screen.AllScreens` order is not stable across reboots or driver updates — a suffix routing entry like `"_lyrics" → 1` may address a different physical projector after a reboot | Document clearly in code comments; defer stable identity (EDID/device name) to a future phase. Operator must verify display assignment after any hardware change. |
| `displayIndex` out of bounds — manifest references display index 2 but only 2 screens exist (indices 0 and 1) | `CreateWindowForDisplay()` throws `ArgumentOutOfRangeException` if `displayIndex >= Screen.AllScreens.Length`; harness catches and surfaces a clear error message rather than crashing silently |
| `Task.WhenAll` masks individual `Load()` exceptions — if two displays fail, only one exception propagates | Phase 2 pre-buffer already converts timeout into a graceful return (not an exception). True exceptions (file not found, VLC crash) should propagate. Use `Task.WhenAll` with exception aggregation awareness; log individual failures before re-throwing |

## Migration Plan

No schema changes, no file migrations. All changes are additive or backward-compatible:
- `SongScanner.Scan()` optional parameter — existing callers unaffected
- `VideoEngine.LoadAll()` — new method; `Load()` unchanged
- `DisplayManager` — new class; no existing code removed until `PrimaryDisplayIndex` constants are deleted from `SongScanner` and `MainWindow` (done as part of this phase's tasks)

## Open Questions

| # | Question | Resolution Deadline |
|---|----------|---------------------|
| 1 | Should `CreateWindowForDisplay()` call `window.Show()` internally, or leave that to the caller? | Resolve during task implementation — lean toward calling `Show()` internally to keep the caller simple, consistent with Phase 2's `CreatePrimaryDisplayWindow()` pattern |
| 2 | Does `Task.WhenAll` need to be `WhenAll` with `AggregateException` unwrapping, or is the default behaviour acceptable for Phase 3? | Resolve during `LoadAll()` implementation — inspect behaviour in the harness with a bad file path |
