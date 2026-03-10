## Context

VideoJam's engine layer (audio, video, display, persistence) is fully built and unit-tested. There is no operator-facing UI — the application cannot be used at a live event. The WPF project skeleton (`MainWindow`, `App.xaml`) exists but contains only the default template. This design covers the full operator shell for Phase 5.

Phase 6 will wire the engine to the UI; in Phase 5 all playback commands are stubs (or absent) and `PlaybackState` is always `Idle`.

## Goals / Non-Goals

**Goals:**
- Deliver a functional WPF two-panel layout (`MainWindow`) driven entirely through `MainViewModel`
- Allow a show to be created, populated with songs, saved, and reopened
- Allow per-channel level and mute to be edited in the mixer panel
- Track dirty state and guard against accidental data loss
- Display routing configuration and fallback image assignment accessible from the UI

**Non-Goals:**
- Engine wiring — no calls to `PlaybackEngine`, `AudioEngine`, or `VideoEngine`
- Hotkey handling — `HotkeyService` is not used in Phase 5
- Polished theming or accessibility
- Unit-testing WPF views — ViewModel logic is tested; view code-behind is lifecycle glue only

## Decisions

### D1: Plain MVVM — no framework

**Decision:** Use hand-rolled `INotifyPropertyChanged` + `RelayCommand<T>` (a thin `ICommand` wrapper). No CommunityToolkit.Mvvm, Prism, or ReactiveUI.

**Rationale:** The project convention is already established in earlier phases. Adding a framework now would introduce a new NuGet dependency and require crew sign-off. The codebase is small enough that boilerplate is negligible.

**Alternatives considered:**
- _CommunityToolkit.Mvvm_ — excellent source-generator story but requires a new package and team buy-in.
- _ReactiveUI_ — powerful but overkill for a single-window app; steep learning curve.

---

### D2: `ObservableCollection<SongEntry>` for the setlist

**Decision:** `MainViewModel.Songs` exposes the `Show.Songs` list as an `ObservableCollection<SongEntry>`. Mutations (add, remove, reorder) happen on this collection directly.

**Rationale:** WPF `ListBox` and `ItemsControl` subscribe to `INotifyCollectionChanged`; any mutation automatically updates the view without requiring the ViewModel to manually refresh. Rebuilding the collection on each change would break selection state and dirty tracking.

---

### D3: Drag-and-drop via attached behaviour, not code-behind

**Decision:** Implement drag-and-drop reordering via a `DragDropBehaviour` attached property in `VideoJam/UI/Behaviours/`. The `MainWindow` code-behind stays lifecycle-only.

**Rationale:** The MVVM constraint forbids business logic in code-behind. Attached behaviours route drag events to a ViewModel command (`ReorderSongCommand`) that manipulates the `ObservableCollection` directly, preserving the separation.

**Risk noted in proposal:** Index-shift bugs when dropping adjacent items. Mitigation: the `ReorderSongCommand` uses `ObservableCollection.Move(fromIndex, toIndex)` (single atomic operation) rather than remove-then-insert.

---

### D4: Dialogs called from ViewModel via an `IDialogService` abstraction

**Decision:** All `MessageBox`, `OpenFileDialog`, and `FolderBrowserDialog` calls are invoked through an `IDialogService` interface injected into `MainViewModel`.

**Rationale:** Keeps the ViewModel testable — the dialog service can be replaced with a test double. The production implementation (`WpfDialogService`) lives in the UI layer and may call WPF APIs freely.

---

### D5: `HasUnsavedChanges` flag, not event sourcing

**Decision:** A `bool HasUnsavedChanges` property on `MainViewModel` is set to `true` on any model mutation and reset to `false` after a successful save.

**Rationale:** The show model is small; full event sourcing or a command pattern would be disproportionate. The flag approach matches what the technical spec describes.

---

### D6: Display routing UI as an inline settings section, not a separate window

**Decision:** Display routing configuration is shown in a collapsible/expandable section below the mixer panel, not a separate dialog window.

**Rationale:** Minimises window management complexity in Phase 5. A modal dialog for routing config would require additional WPF window ownership wiring that is better suited to Phase 6 when the main window lifecycle is fully established.

---

## Risks / Trade-offs

| Risk | Mitigation |
|------|-----------|
| Drag-and-drop index shift bugs | Use `ObservableCollection.Move()` as a single atomic operation; cover with ViewModel unit tests |
| `IDialogService` abstraction leaks WPF concerns | Keep the interface agnostic (`string[]` paths, `bool` confirmations); WPF specifics confined to `WpfDialogService` |
| Path relativisation before show is saved | Store absolute path temporarily; `ShowFileService.Save()` calls `PathResolver.MakeRelative()` at write time — already handled by Phase 4 |
| Songs collection cleared on `NewShow` with pending edits | Guard with `HasUnsavedChanges` check before any destructive command |
| Display routing UI complexity creep | Keep it to a simple editable grid (suffix string → display index integer); no validation UI in Phase 5 |

## Migration Plan

No data migration required. The `.show` file format is unchanged (Phase 4). The WPF project skeleton (`MainWindow.xaml`, `App.xaml`) is overwritten in-place with the new implementation.

## Open Questions

- *(none blocking Phase 5)* Phase 6 will clarify how `PlaybackState` changes propagate from the engine to `MainViewModel` — an event or a callback TBD.
