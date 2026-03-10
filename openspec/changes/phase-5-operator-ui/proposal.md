## Why

The VideoJam engine layers (audio, video, display, persistence) are fully built and tested, but there is no graphical interface — the application cannot be operated by a human. Phase 5 delivers the complete WPF operator shell so a live-performance crew can manage shows, setlists, and channel levels before taking the stage.

## What Changes

- New `MainWindow` WPF shell with a two-panel layout (setlist left, mixer right)
- New `MainViewModel` driving all UI state via MVVM (no code-behind logic)
- Setlist panel: song list bound to the loaded show, drag-and-drop reordering, Add Song via folder picker
- Mixer panel: per-channel level sliders and mute toggles bound to `ChannelSettings`; controls locked during playback
- Show file operations via a File menu: New, Open, Save, Save As with unsaved-change guards
- Window title reflects dirty state (`VideoJam — ShowName*`)
- Display routing configuration panel: global suffix → display index mapping and per-song overrides
- Fallback image assignment: per-display PNG picker

## Capabilities

### New Capabilities

- `operator-shell`: `MainWindow` XAML layout and `MainViewModel` — the foundational shell that hosts all panels and owns top-level state (`LoadedShow`, `SelectedSong`, `PlaybackState`, `HasUnsavedChanges`, `StatusText`) and commands
- `setlist-panel`: Setlist `ListBox` bound to `Show.Songs`, click-to-cue selection, drag-and-drop reorder, and Add Song workflow via `SongScanner`
- `mixer-panel`: Per-channel mixer rows (name label, level `Slider` 0.0–1.0, mute `CheckBox`) bound to `SelectedSong.Channels`; visually distinguishes video audio channels; disabled during playback
- `show-file-operations`: File menu (New / Open / Save / Save As), `HasUnsavedChanges` dirty-state tracking, unsaved-changes `MessageBox` guard, and window title formatting
- `display-routing-ui`: Global suffix → display-index mapping editor and per-song override dialog; per-display fallback PNG file picker

### Modified Capabilities

*(none — no existing spec-level requirements are changing)*

## Impact

- **New files:** `VideoJam/UI/MainWindow.xaml`, `VideoJam/UI/MainWindow.xaml.cs`, `VideoJam/UI/ViewModels/MainViewModel.cs`, `VideoJam/UI/ViewModels/RelayCommand.cs`
- **Modified files:** `VideoJam/App.xaml.cs` (startup wiring), `VideoJam/UI/` (new sub-views / dialogs)
- **Consumed services:** `ShowFileService`, `SongScanner`, `PathResolver` (all from Phase 4)
- **Model dependencies:** `Show`, `SongEntry`, `ChannelSettings`, `AppState` (read/write)
- **No new NuGet packages required**

## Non-goals for this Phase

- Wiring playback commands to the engine (Space = GO, Escape = pause) — that is Phase 6
- Audio/video pipeline initialisation from the UI — Phase 6
- Polished visual design / theming — MVP layout only
- Accessibility (screen readers, high-contrast) — post-launch hardening

## Primary Technical Risk

**Drag-and-drop reordering in WPF** has no built-in `ListBox` support and requires careful use of `DragDrop` events or an attached behaviour to avoid subtle index-shift bugs when reordering adjacent items. The implementation must manipulate the `ObservableCollection<SongEntry>` directly (not re-create it) to preserve bindings and dirty-state tracking.
