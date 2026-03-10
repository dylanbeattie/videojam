## ADDED Requirements

### Requirement: File menu with New, Open, Save, Save As
`MainWindow` SHALL include a WPF `Menu` with a "File" top-level item containing:
- **New** — creates a blank show
- **Open…** — opens a `.show` file
- **Save** (`Ctrl+S`) — saves the current show to its existing path
- **Save As…** — saves the current show to a new path

#### Scenario: File menu is visible
- **WHEN** the application window is open
- **THEN** a "File" menu SHALL be present in the menu bar with New, Open, Save, and Save As items

#### Scenario: Save is enabled only when a show is loaded
- **WHEN** no show is loaded (`LoadedShow` is `null`)
- **THEN** the Save and Save As menu items SHALL be disabled

---

### Requirement: New Show creates a blank show
Invoking File → New SHALL:
1. If `HasUnsavedChanges` is `true`, prompt the operator ("Save changes before creating a new show?") via `IDialogService.Confirm()`.
   - If confirmed: invoke Save first, then proceed.
   - If declined: proceed without saving (discard changes).
   - If the dialog is cancelled: abort — do not create a new show.
2. Create a blank `Show` with an empty `Songs` collection.
3. Set `LoadedShow` to the new show, clear `SelectedSong`, reset `HasUnsavedChanges` to `false`.

#### Scenario: New Show with unsaved changes prompts
- **WHEN** `HasUnsavedChanges` is `true` and the operator clicks File → New
- **THEN** a confirmation dialog SHALL be shown before the new show is created

#### Scenario: New Show cancelled aborts operation
- **WHEN** the unsaved-changes dialog is cancelled (not confirmed or declined)
- **THEN** no new show is created and the existing show remains loaded

#### Scenario: New Show without unsaved changes proceeds immediately
- **WHEN** `HasUnsavedChanges` is `false` and the operator clicks File → New
- **THEN** a blank show is created immediately without any dialog

---

### Requirement: Open Show loads from a .show file
Invoking File → Open SHALL:
1. If `HasUnsavedChanges` is `true`, apply the same save-prompt guard as New Show.
2. Open a file picker via `IDialogService.PickOpenFile()` filtered to `*.show`.
3. Pass the selected path to `ShowFileService.Load()`.
4. On success: set `LoadedShow`, populate `Songs`, set `HasUnsavedChanges` to `false`.
5. On failure: display an error message via `IDialogService` and leave the current show unchanged.

#### Scenario: Open loads a valid show file
- **WHEN** the operator selects a valid `.show` file
- **THEN** `LoadedShow` SHALL be populated from the file and `HasUnsavedChanges` SHALL be `false`

#### Scenario: Open with corrupted file shows error
- **WHEN** `ShowFileService.Load()` throws a `ShowFileException`
- **THEN** an error message SHALL be displayed and `LoadedShow` SHALL be unchanged

#### Scenario: Open cancelled is a no-op
- **WHEN** the operator clicks Cancel in the file picker
- **THEN** `LoadedShow` SHALL be unchanged

---

### Requirement: Save persists the current show
Invoking File → Save (or `Ctrl+S`) SHALL:
1. If the show has no file path yet (new unsaved show), behave as Save As.
2. Otherwise, pass the current `LoadedShow` and its path to `ShowFileService.Save()`.
3. On success: set `HasUnsavedChanges` to `false`.
4. On failure: display an error message via `IDialogService`.

#### Scenario: Save writes to existing path
- **WHEN** the show has been saved before and the operator presses `Ctrl+S`
- **THEN** `ShowFileService.Save()` SHALL be called with the existing path and `HasUnsavedChanges` SHALL become `false`

#### Scenario: Save on new show delegates to Save As
- **WHEN** the show has never been saved and the operator clicks File → Save
- **THEN** the Save As file picker SHALL open

---

### Requirement: Save As persists to a new path
Invoking File → Save As SHALL:
1. Open a save file picker via `IDialogService.PickSaveFile()` with `*.show` filter and `.show` default extension.
2. Pass the current `LoadedShow` and the chosen path to `ShowFileService.Save()`.
3. On success: update the show's stored path, set `HasUnsavedChanges` to `false`, update the window title.
4. On failure: display an error message via `IDialogService`.

#### Scenario: Save As updates window title
- **WHEN** the operator saves as "FestivalSet.show"
- **THEN** the window title SHALL update to `VideoJam — FestivalSet`

#### Scenario: Save As cancelled is a no-op
- **WHEN** the operator clicks Cancel in the save file picker
- **THEN** the show is not saved and `HasUnsavedChanges` is unchanged

---

### Requirement: Dirty state tracking
`MainViewModel.HasUnsavedChanges` SHALL be set to `true` whenever any of the following occur:
- A song is added, removed, or reordered
- Any `ChannelSettings.Level` or `ChannelSettings.Muted` value changes
- Any display routing override is added, modified, or removed
- A fallback image path is assigned or changed

`HasUnsavedChanges` SHALL be reset to `false` after a successful Save or Save As, or after a New Show or Open Show that discards the previous state.

#### Scenario: Level change marks dirty
- **WHEN** a channel level slider is moved
- **THEN** `HasUnsavedChanges` SHALL become `true`

#### Scenario: Successful save clears dirty
- **WHEN** `ShowFileService.Save()` completes without error
- **THEN** `HasUnsavedChanges` SHALL become `false`

---

### Requirement: Unsaved changes guard on application close
When the operator attempts to close `MainWindow` and `HasUnsavedChanges` is `true`, a confirmation dialog SHALL be shown ("You have unsaved changes. Save before closing?"). If the operator confirms, Save SHALL be invoked before closing. If declined, the application SHALL close without saving. If cancelled, the close SHALL be aborted.

#### Scenario: Close with unsaved changes prompts
- **WHEN** the operator closes the window and `HasUnsavedChanges` is `true`
- **THEN** a save-prompt dialog SHALL appear before the window closes

#### Scenario: Close without unsaved changes proceeds immediately
- **WHEN** `HasUnsavedChanges` is `false` and the operator closes the window
- **THEN** the application SHALL close without any dialog
