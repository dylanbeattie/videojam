## Why

Phase 2 hardcodes every video file to `DisplayIndex = 0`. A live performance rig typically has two or more projectors — lyrics on one, visuals on another — so the application cannot fulfil its core display-routing promise until physical displays are enumerated and video files are routed to the correct screen based on their filename suffix.

## What Changes

- **New** — `DisplayManager`: enumerates physical displays via `Screen`, owns the authoritative `PrimaryDisplayIndex` constant, resolves filename suffix → display index from a routing dictionary, and creates/positions a `VlcDisplayWindow` for each required display index
- **New** — `VideoEngine.LoadAll()`: a convenience method that calls `Load()` concurrently for all display slots in the manifest via `Task.WhenAll`, replacing the single `Load()` call pattern in the harness
- **Updated** — `SongScanner.Scan()`: gains an optional `IReadOnlyDictionary<string, int>? displayRouting` parameter; each MP4's `VideoFileManifest.DisplayIndex` is resolved from the routing dictionary by suffix, falling back to the primary display index if the suffix is absent from the routing table
- **Updated** — `MainWindow` harness: uses `DisplayManager` to enumerate required display windows, passes the routing config to `SongScanner`, and calls `VideoEngine.LoadAll()` instead of a single `Load()`
- **Retired** — `private const int PrimaryDisplayIndex = 0` in `SongScanner.cs` and `MainWindow.xaml.cs`; promoted to `DisplayManager.PrimaryDisplayIndex`

## Capabilities

### New Capabilities

- `display-manager`: Enumerates physical displays; resolves suffix → display index; creates and positions `VlcDisplayWindow` instances for each required display; owns the `PrimaryDisplayIndex` constant

### Modified Capabilities

- `song-scanner`: `Scan()` now accepts a display routing dictionary and resolves each MP4's `DisplayIndex` from it rather than hardcoding 0; behaviour is backward-compatible when no routing is provided

## Impact

- **VideoJam/Engine/DisplayManager.cs** — new file
- **VideoJam/Engine/VideoEngine.cs** — add `LoadAll()` method
- **VideoJam/Services/SongScanner.cs** — add `displayRouting` parameter to `Scan()`
- **VideoJam/UI/MainWindow.xaml.cs** — use `DisplayManager`, pass routing, call `LoadAll()`
- **No new NuGet packages** — `System.Windows.Forms` (already referenced for `Screen`) covers display enumeration

## Non-goals for this phase

- Per-song display routing overrides (`SongEntry.DisplayRoutingOverrides` — Phase 4)
- Fallback PNG assignment to specific displays (Phase 4)
- Hot-plug display detection / reconnection (out of scope for all phases)
- Show file persistence (`ShowFileService` — Phase 4)
- Operator UI (`MainWindow` remains a dev harness — Phase 5)

## Primary Technical Risk

`System.Windows.Forms.Screen` enumerates displays in an order that may not be stable across reboots or driver updates on some hardware. If `Screen` indices drift between sessions, suffix routing stored in the `.show` file will map to the wrong physical display. Phase 3 will document this limitation and defer a stable display-identity scheme (e.g. EDID-based naming) to a later phase.
