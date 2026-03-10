## ADDED Requirements

### Requirement: Global display routing configuration panel
A display routing section SHALL be accessible from the main UI (e.g., a collapsible panel below the mixer, or a dedicated tab). It SHALL display the global suffix → display index mapping defined in `Show.GlobalDisplayRouting`. The operator SHALL be able to add, edit, and remove suffix → display index entries.

Each row in the routing table SHALL show:
- A text field for the file suffix (e.g., `_display2`)
- A numeric field for the display index (e.g., `1`)
- A remove button

An "Add Mapping" button SHALL append a new blank row.

Changes to the routing table SHALL set `HasUnsavedChanges` to `true`.

#### Scenario: Routing table shows existing mappings
- **WHEN** a show is loaded with two global routing entries
- **THEN** the routing table SHALL display two rows with the correct suffix and display index values

#### Scenario: Add mapping appends a new row
- **WHEN** the operator clicks "Add Mapping"
- **THEN** a new blank row SHALL be appended to the routing table and `HasUnsavedChanges` SHALL be `true`

#### Scenario: Edit routing entry updates model
- **WHEN** the operator changes a suffix value in the routing table
- **THEN** the corresponding `Show.GlobalDisplayRouting` entry SHALL be updated and `HasUnsavedChanges` SHALL be `true`

#### Scenario: Remove routing entry deletes from model
- **WHEN** the operator clicks the remove button on a routing row
- **THEN** the entry SHALL be removed from `Show.GlobalDisplayRouting` and `HasUnsavedChanges` SHALL be `true`

---

### Requirement: Per-song display routing overrides
Each song row in the setlist SHALL provide access to per-song routing overrides (e.g., via a small "⚙" button that opens an inline edit area or compact dialog). The override UI SHALL mirror the global routing table structure (suffix → display index rows) but apply to `SongEntry.DisplayRoutingOverrides` for that specific song.

#### Scenario: Per-song override opens for correct song
- **WHEN** the operator opens overrides for song "Outro"
- **THEN** the UI SHALL display and allow editing of `SongEntry.DisplayRoutingOverrides` for "Outro" only

#### Scenario: Per-song override change marks show dirty
- **WHEN** the operator adds a per-song routing override
- **THEN** `HasUnsavedChanges` SHALL be `true`

---

### Requirement: Fallback image assignment per display
The display routing section SHALL also expose a per-display fallback image picker. For each detected display (enumerated via `System.Windows.Forms.Screen.AllScreens` or equivalent), a row SHALL show:
- Display index and name (e.g., `Display 0 — \\.\DISPLAY1`)
- The currently assigned fallback PNG path (or "(none)" if unset)
- A "Browse…" button that opens a file picker filtered to `*.png`

Selecting a PNG SHALL update `Show.FallbackImages[displayIndex]` and set `HasUnsavedChanges` to `true`.

#### Scenario: Fallback image browse assigns path
- **WHEN** the operator clicks "Browse…" for Display 1 and selects a PNG
- **THEN** `Show.FallbackImages[1]` SHALL be updated and `HasUnsavedChanges` SHALL be `true`

#### Scenario: Fallback image picker cancelled is a no-op
- **WHEN** the operator clicks "Browse…" and cancels the file picker
- **THEN** `Show.FallbackImages` SHALL be unchanged

#### Scenario: Unassigned display shows "(none)"
- **WHEN** no fallback image has been assigned for a display
- **THEN** the fallback path field SHALL display `(none)`
