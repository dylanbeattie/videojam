# Code Review — `phase-5-operator-ui`

**Reviewer:** QA Engineer
**Date:** 2026-03-10
**Status:** ❌ Returned for changes

---

## Summary

| Dimension    | Status                                              |
|--------------|-----------------------------------------------------|
| Completeness | 46/46 tasks marked ✓ — however, one task appears to be in name only |
| Correctness  | Most requirements implemented; two meaningful spec divergences       |
| Coherence    | Design decisions broadly followed; minor redundancy and one fragility |

---

## CRITICAL — Must Fix Before Archive

### C1 — Task 7.4: Per-song routing override UI is absent from the XAML

The task reads: *"Add per-song override access via a small button (⚙) on each setlist row that shows/expands the song's `DisplayRoutingOverrides` in an inline area."* The spec likewise requires it.

Inspecting `MainWindow.xaml`, the setlist `ItemTemplate` contains exactly three columns — a 1-based index `TextBlock`, a name `TextBlock`, and a remove `Button` (✕). There is no ⚙ button, no inline expansion area, and no corresponding logic in `MainViewModel` for managing per-song override collections. The task has been ticked `[x]` but the feature is absent.

**Required action:** Implement the ⚙ button and an inline collapsible area bound to a `PerSongOverrideEntries` collection (mirroring the pattern already used for `GlobalRoutingEntries`), or reach an explicit agreement with the Captain that per-song overrides are deferred to Phase 6 and update the task and spec accordingly.

> **Captain's notes:**

---

## WARNING — Should Fix

### W1 — Unsaved-changes guard has no Cancel path

The specification is unambiguous: the guard must support three outcomes — *confirm* (save then proceed), *decline* (proceed without saving), and *cancel* (abort the operation entirely). This applies to New, Open, and Close.

The `IDialogService.Confirm()` method returns a `bool`, backed by a `MessageBoxButton.YesNo` dialog in `WpfDialogService`. The `ApplyUnsavedChangesGuard()` method interprets `false` (No) as "proceed without saving" — there is no mechanism for the operator to say *"actually, stop, I changed my mind."* This gap is most consequential in `OnWindowClosing`: once the No button is clicked, the application closes. The operator cannot abort.

The design doc acknowledges this: *"Uses a three-button MessageBox (Yes / No / Cancel) equivalent via a two-step dialog"* — but no two-step dialog was implemented.

**Required action:** Add a `bool? Confirm3(string message, string title)` overload to `IDialogService` (returning `true`/`false`/`null` for Yes/No/Cancel respectively), implement it with `MessageBoxButton.YesNoCancel` in `WpfDialogService`, and thread it through `ApplyUnsavedChangesGuard()` and `ConfirmClose()`.

> **Captain's notes:**

---

### W2 — `ExecuteAddSong` does not use `SongEntry.CreateFromScan()` as specified

The setlist-panel spec, step 3 of the Add Song workflow, reads: *"Create a `SongEntry` via `SongEntry.CreateFromScan()`."* The `MainViewModel.ExecuteAddSong()` method instead builds the entry inline, duplicating the channel-defaults logic that already lives in `CreateFromScan`. The comment in the code acknowledges the reason — there is no show file path yet when the show is unsaved — but the consequence is that `SongEntry.CreateFromScan()` is orphaned from its primary use case, and path-handling differs: `CreateFromScan` calls `PathResolver.MakeRelative()` at scan time, whereas the inline code stores the absolute path and defers relativisation to save time.

If a developer ever modifies `CreateFromScan` (e.g. to add a new channel default), the Add Song path will silently diverge.

**Required action:** Either use `CreateFromScan` with a clear strategy for the no-show-path case (e.g. passing an empty string and relying on the existing `NormalizeLoadedPaths` round-trip), or explicitly document the divergence in a comment and update the spec to reflect the actual design.

> **Captain's notes:**

---

### W3 — `ShowFileService` is not injectable; task 8.5 specified a mock

Task 8.5 reads: *"Unit test `MainViewModel` — New/Open/Save/SaveAs commands with a mock `IDialogService` and mock `ShowFileService`."* The `ShowFileService` is instantiated directly in `MainViewModel`'s constructor (`private readonly ShowFileService _showFileService = new();`) and cannot be substituted. The tests compensate by writing real files to temporary directories, which is pragmatic but means any failure in `ShowFileService` will surface as a `MainViewModel` test failure, conflating two units.

**Required action:** Either extract `IShowFileService` and inject it into `MainViewModel` (consistent with the `IDialogService` pattern already in place), or explicitly document the decision to use real file I/O in the tests as an accepted integration-style approach and update task 8.5's description accordingly.

> **Captain's notes:**

---

## SUGGESTION — Nice to Fix

### S1 — Double `MarkDirty()` calls for routing entries

In `ExecuteAddRoutingEntry`, the entry is constructed with `OnRoutingEntryChanged` as a callback *and* `OnRoutingEntryPropertyChanged` is subscribed to `PropertyChanged`. Both ultimately call `MarkDirty()`. A single property change therefore fires `MarkDirty()` twice. The setter guard makes this idempotent so there is no observable defect, but it is a redundancy. The same pattern applies to `FallbackImageEntryViewModel`, where `MarkDirty()` is called once from the constructor callback and once explicitly in `ExecuteBrowseFallbackImage`. Pick one path and remove the other.

> **Captain's notes:**

---

### W4 — Setlist index numbers are incorrect after drag-and-drop reorder

All songs in the setlist display the same index number after a `Move()` operation. The cause is the use of `ItemsControl.AlternationIndex` to generate sequential row numbers, which is the wrong tool for the job. `AlternationIndex` is designed for alternating visual styles (e.g. striped rows); WPF does not reliably recalculate it on items affected by `ObservableCollection.Move()`. After a drag-and-drop, the indices go stale and all rows show the same (incorrect) value.

A related symptom: when an item is dropped at position 0, its `AlternationIndex` wraps to `AlternationCount - 1` (9,999 with the current `AlternationCount="10000"` setting), and `AddOneConverter` produces a displayed index of 10,000.

**Required action:** Replace `AlternationIndex` with a mechanism that derives the index from the item's actual position in the collection. The standard approaches are: (a) wrap each `SongEntry` in a lightweight ViewModel that exposes a bindable `Index` property and is updated whenever the collection changes; or (b) use a multi-value converter that accepts both the item and the collection and calls `IndexOf` at bind time. Option (a) is cleaner for a collection that is already mutation-heavy.

> **Captain's notes:**
>
> Drag'n'drop has no target position indicator - it's error-prone with no indication where a song is being dropped. Also, when dragging an item to the top of the list it gets a weird 100000+ numeric prefix which is probably not correct.

---

### S2 — `DragDropBehaviour._dragSourceIndex` is a static field

`private static int _dragSourceIndex = -1;` is shared across all instances of the behaviour. For a single-window application this is safe, but it is a latent hazard: if a second `ListBox` with the behaviour attached were ever introduced, or if a drag were interrupted by an exception without resetting the field, state could leak between drag operations. An instance-level approach using the `DependencyObject` as the state carrier would be more robust.

> **Captain's notes:**
>
> Drag'n'drop has no target position indicator - it's error-prone with no indication where a song is being dropped. Also, when dragging an item to the top of the list it gets a weird 100000+ numeric prefix which is probably not correct.

---

### S3 — Magic number colour in `VideoAudioForegroundConverter`

`Color.FromRgb(0x80, 0x80, 0xA0)` is a hardcoded blue-grey with no named constant or resource key. A `static readonly` field named `VideoAudioColor` (or a XAML `SolidColorBrush` resource) would make the intent legible and the value straightforward to adjust during the MVP visual pass.

> **Captain's notes:**

---

## What Is Good

It would be remiss not to acknowledge the work that is well done:

- Clean MVVM discipline throughout — the code-behind is genuinely lifecycle-only, and the `RelayCommandAdapter` trick for the Ctrl+S binding is elegant
- `ChannelSettingsViewModel` as a binding wrapper that propagates changes back to the model is the correct pattern and is implemented correctly
- `FakeDialogService` test double is well-structured and covers all the observable call-tracking needed for the command tests
- `DragDropBehaviour` routes correctly to `ReorderSongCommand` and uses `ObservableCollection.Move()` as required — the adjacent-item edge case is covered in tests
- `NormalizeLoadedPaths` after load is a thoughtful addition that keeps the in-memory model consistently absolute
- `RelayCommand` and `RelayCommand<T>` are clean and well-tested

The foundations are solid. Resolve the items above and this is ready to ship.

---

*Returned by QA — 2026-03-10*
