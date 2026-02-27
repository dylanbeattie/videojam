## ADDED Requirements

### Requirement: DisplayManager exposes the primary display index constant

`DisplayManager` SHALL expose a named constant `PrimaryDisplayIndex` with value `0` representing the primary physical display. This constant SHALL be the single authoritative definition of the primary display index in the codebase; all other occurrences SHALL be removed in favour of this constant.

#### Scenario: Primary display index is zero
- **WHEN** `DisplayManager.PrimaryDisplayIndex` is read
- **THEN** its value is `0`

---

### Requirement: DisplayManager resolves a suffix to a display index

`DisplayManager.ResolveDisplayIndex(string suffix, IReadOnlyDictionary<string, int> routing)` SHALL look up `suffix` in `routing` and return the mapped display index. If `suffix` is absent from `routing`, or if `routing` is empty, the method SHALL return `DisplayManager.PrimaryDisplayIndex`.

#### Scenario: Suffix found in routing
- **WHEN** `ResolveDisplayIndex("_lyrics", {"_lyrics": 1, "_visuals": 2})` is called
- **THEN** the return value is `1`

#### Scenario: Suffix not in routing returns primary index
- **WHEN** `ResolveDisplayIndex("_unknown", {"_lyrics": 1})` is called
- **THEN** the return value is `DisplayManager.PrimaryDisplayIndex`

#### Scenario: Empty routing returns primary index
- **WHEN** `ResolveDisplayIndex("_lyrics", {})` is called
- **THEN** the return value is `DisplayManager.PrimaryDisplayIndex`

#### Scenario: Empty suffix with no routing entry returns primary index
- **WHEN** `ResolveDisplayIndex("", {"_lyrics": 1})` is called
- **THEN** the return value is `DisplayManager.PrimaryDisplayIndex`

---

### Requirement: DisplayManager returns the distinct set of required display indices from a manifest

`DisplayManager.GetRequiredDisplayIndices(SongManifest manifest)` SHALL return the distinct set of `DisplayIndex` values present across all `VideoFileManifest` entries in `manifest.VideoFiles`. If `manifest.VideoFiles` is empty, the method SHALL return an empty set.

#### Scenario: Manifest with two display indices
- **WHEN** a manifest contains video files with `DisplayIndex` values `[0, 1, 1]`
- **THEN** `GetRequiredDisplayIndices` returns the set `{0, 1}` (no duplicates)

#### Scenario: Manifest with no video files
- **WHEN** a manifest contains no video files
- **THEN** `GetRequiredDisplayIndices` returns an empty collection

---

### Requirement: DisplayManager creates a correctly-positioned VlcDisplayWindow for a display index

`DisplayManager.CreateWindowForDisplay(int displayIndex)` SHALL:
- Retrieve `Screen.AllScreens[displayIndex]`.
- Compute the window's DIP bounds by dividing the screen's physical pixel bounds by the screen's DPI scale factor (derived from the ratio of physical bounds to `SystemParameters.PrimaryScreen*` for the primary screen, or equivalent per-monitor DPI derivation for secondary screens).
- Construct a `VlcDisplayWindow`, call `SetBounds()` with the computed DIP bounds, call `Show()`, and return the window.

If `displayIndex` is greater than or equal to `Screen.AllScreens.Length`, the method SHALL throw `ArgumentOutOfRangeException` with a message identifying the invalid index and the number of screens available.

#### Scenario: Valid display index produces a shown window
- **WHEN** `CreateWindowForDisplay(0)` is called on a machine with at least one display
- **THEN** a `VlcDisplayWindow` is returned that is visible and covers the primary display

#### Scenario: Display index out of range throws
- **WHEN** `CreateWindowForDisplay(5)` is called on a machine with fewer than 6 displays
- **THEN** an `ArgumentOutOfRangeException` is thrown
