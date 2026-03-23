## REMOVED — DisplayManager

`DisplayManager` was removed in v2 of the VideoJam architecture.

### What it did (v1)

In v1, `DisplayManager` was responsible for:
- Exposing a `PrimaryDisplayIndex` constant (`0`)
- Resolving filename suffixes (e.g. `_lyrics`) to physical display indices via a routing dictionary
- Returning the distinct set of display indices required by a manifest
- Creating and sizing `VlcDisplayWindow` instances to cover a full physical screen

### What replaced it (v2)

The suffix-to-display-index routing model was abandoned entirely. Video files are now identified
by **slot index** — a zero-based integer assigned in alphabetical filename order by `SongScanner`.

Responsibilities that were in `DisplayManager` are now distributed as follows:

| v1 Responsibility | v2 Owner |
|---|---|
| Assign a display index to each video file | `SongScanner` assigns `SlotIndex` in alphabetical order |
| Create and show `VlcDisplayWindow` for each display | `PlaybackEngine.EnsureVideoWindows()` creates one window per slot |
| Size windows to fill a physical screen | Removed — windows are freely user-positioned (`SingleBorderWindow`, `CanResize`) |
| `PrimaryDisplayIndex` constant | Removed — no longer needed |

### Migration note

Any code that references `DisplayManager` has been removed. The class exists as an empty shell
with a tombstone comment. `DisplayManagerTests.cs` similarly contains only a tombstone comment.
