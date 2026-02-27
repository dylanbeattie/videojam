## 1. DisplayManager — New Class

- [x] 1.1 Create `VideoJam/Engine/DisplayManager.cs` as an `internal static class`; add `public const int PrimaryDisplayIndex = 0` with a Phase 3 note comment; add XML doc comment on the class
- [x] 1.2 Implement `ResolveDisplayIndex(string suffix, IReadOnlyDictionary<string, int> routing)` — look up `suffix` in `routing`; return the mapped value if found, otherwise return `PrimaryDisplayIndex`; add XML doc comment
- [x] 1.3 Implement `GetRequiredDisplayIndices(SongManifest manifest)` — return `manifest.VideoFiles.Select(v => v.DisplayIndex).Distinct()` as a read-only collection; add XML doc comment
- [x] 1.4 Implement `CreateWindowForDisplay(int displayIndex)` — validate `displayIndex < Screen.AllScreens.Length` (throw `ArgumentOutOfRangeException` with a descriptive message if not); retrieve `Screen.AllScreens[displayIndex]`; compute DPI scale as `screen.Bounds.Width / SystemParameters.PrimaryScreenWidth` for display 0, or derive per-monitor scale for secondary displays; compute DIP bounds; construct a `VlcDisplayWindow`, call `SetBounds()`, call `Show()`, and return it; add XML doc comment including the stability caveat about `Screen.AllScreens` ordering
- [x] 1.5 Verify `DisplayManager` compiles with zero errors and zero warnings; confirm the class has no instance state (all members are `static`)

## 2. SongScanner — Display Routing Parameter

- [x] 2.1 Add `IReadOnlyDictionary<string, int>? displayRouting = null` as an optional second parameter to `SongScanner.Scan()`; update the XML doc comment to document the new parameter and its fallback behaviour
- [x] 2.2 Inside `Scan()`, replace the `DisplayIndex: PrimaryDisplayIndex` literal in the `VideoFileManifest` constructor with `DisplayIndex: DisplayManager.ResolveDisplayIndex(suffix, displayRouting ?? new Dictionary<string, int>())`
- [x] 2.3 Remove `private const int PrimaryDisplayIndex = 0` from `SongScanner.cs`; confirm no remaining references to the removed constant
- [x] 2.4 Verify all existing `SongScannerTests` still pass — the optional parameter must be backward-compatible with zero-argument call sites

## 3. VideoEngine — LoadAll

- [x] 3.1 Add `public async Task LoadAll(SongManifest manifest, IReadOnlyDictionary<int, VlcDisplayWindow> windows, CancellationToken cancellationToken = default)` to `VideoEngine`; implement by building a `Task[]` of `Load()` calls (one per `windows` entry) and returning `Task.WhenAll(tasks)`; add XML doc comment noting partial-failure behaviour (a timed-out `Load()` does not throw — `LoadAll` completes normally; an exception from any slot propagates)
- [x] 3.2 Verify `LoadAll` with an empty `windows` dictionary completes synchronously without error
- [x] 3.3 Add XML doc comments on `LoadAll`; confirm no regression in existing `VideoEngine` members

## 4. MainWindow Harness — Multi-Display Wiring

- [x] 4.1 Remove `private const int PrimaryDisplayIndex = 0` from `MainWindow.xaml.cs`; replace all usages with `DisplayManager.PrimaryDisplayIndex`
- [x] 4.2 Change the `_displayWindow` field from `VlcDisplayWindow?` to `Dictionary<int, VlcDisplayWindow>` initialised as an empty dictionary; update `CloseDisplayWindow()` to iterate the dictionary, close each window, and clear the dictionary
- [x] 4.3 In `OnPlayClicked`, replace the single-display window creation block with: call `DisplayManager.GetRequiredDisplayIndices(manifestForVideo)` to get needed indices; call `DisplayManager.CreateWindowForDisplay(displayIndex)` for each; populate `_displayWindow` dictionary
- [x] 4.4 Replace the single `await _videoEngine.Load(...)` call with `await _videoEngine.LoadAll(manifestForVideo, _displayWindow)`
- [x] 4.5 Pass an empty routing dictionary to `SongScanner.Scan()` for now (real routing comes from the `.show` file in Phase 4): `SongScanner.Scan(folder, displayRouting: new Dictionary<string, int>())`
- [x] 4.6 Verify `OnPlaybackEnded` and `OnStopClicked` both reach the updated `CloseDisplayWindow()` and close all display windows correctly
- [x] 4.7 Build the full solution with zero errors and zero warnings; run all 22 existing tests — all must pass

## 5. Unit Tests

- [x] 5.1 Create `VideoJam.Tests/DisplayManagerTests.cs`; add tests covering all `ResolveDisplayIndex` scenarios from the spec: suffix found in routing, suffix absent, empty routing, empty suffix with no entry
- [x] 5.2 Add tests for `GetRequiredDisplayIndices`: manifest with duplicate display indices returns distinct set; manifest with no video files returns empty collection
- [x] 5.3 Add tests to `SongScannerTests.cs` covering the three new routing scenarios from the spec: suffix matches routing, suffix absent from routing falls back to primary, no routing argument falls back to primary
- [x] 5.4 Run full test suite; confirm all tests pass (target: ≥ 28 tests)

## 6. Manual Smoke Tests

- [ ] 6.1 Connect a second display; launch the app; load a folder containing two MP4 files with different suffixes; configure routing so each suffix maps to a different display index; press Play — verify two `VlcDisplayWindow` instances appear, one on each physical display, each covering its screen edge-to-edge
- [ ] 6.2 Verify A/V sync is preserved with two simultaneous video streams — both displays start video at the same moment audio begins; check Debug log for the `A/V dispatch Δt` value
- [ ] 6.3 Verify graceful degradation: deliberately point one video path at a missing file; confirm audio plays, the valid display shows video, and the invalid display shows fallback black — no crash, no audio interruption
