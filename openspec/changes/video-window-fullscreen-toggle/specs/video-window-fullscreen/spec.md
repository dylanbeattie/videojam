## ADDED Requirements

### Requirement: VlcDisplayWindow toggles fullscreen on double-click
Double-clicking anywhere inside a `VlcDisplayWindow` SHALL toggle the window between its normal windowed state and a true fullscreen state. True fullscreen is defined as: `WindowStyle = None`, `ResizeMode = NoResize`, `WindowState = Maximized`. This covers the full physical display including the taskbar.

The window SHALL track whether it is currently in fullscreen mode via a private boolean flag (`_isFullscreen`).

#### Scenario: Double-click enters fullscreen from windowed state
- **WHEN** the operator double-clicks inside a `VlcDisplayWindow` that is in its normal windowed state
- **THEN** the window transitions to fullscreen: `WindowStyle` is `None`, `ResizeMode` is `NoResize`, `WindowState` is `Maximized`, covering the full display including the taskbar

#### Scenario: Double-click exits fullscreen and restores previous state
- **WHEN** the operator double-clicks inside a `VlcDisplayWindow` that is currently in fullscreen
- **THEN** the window restores its previous `Left`, `Top`, `Width`, `Height`, and `WindowState` exactly as they were before entering fullscreen

#### Scenario: Restored position matches pre-fullscreen position
- **WHEN** fullscreen is exited
- **THEN** `Left`, `Top`, `Width`, and `Height` match the values captured immediately before entering fullscreen

---

### Requirement: Pre-fullscreen state is captured on fullscreen entry
Immediately before entering fullscreen, `VlcDisplayWindow` SHALL capture the current `Left`, `Top`, `Width`, `Height`, and `WindowState` into private fields. These values SHALL be used verbatim when restoring the window on fullscreen exit.

#### Scenario: Pre-fullscreen bounds are captured before transition
- **WHEN** `ToggleFullscreen()` is called while the window is in windowed state
- **THEN** `_preFullscreenLeft`, `_preFullscreenTop`, `_preFullscreenWidth`, `_preFullscreenHeight`, and `_preFullscreenState` are set to the window's current values before any `WindowStyle` or `WindowState` changes are made

---

### Requirement: Fullscreen exit follows the correct WPF restore sequence
The fullscreen exit sequence SHALL be: set `WindowState = Normal`, then restore `WindowStyle = SingleBorderWindow` and `ResizeMode = CanResize`, then restore `Left`, `Top`, `Width`, `Height`, then restore prior `WindowState`. Deviating from this order is not permitted.

#### Scenario: WindowState is set to Normal before WindowStyle is restored
- **WHEN** exiting fullscreen
- **THEN** `WindowState` is set to `Normal` before `WindowStyle` is changed from `None` back to `SingleBorderWindow`

---

### Requirement: GetLayout() returns windowed layout regardless of fullscreen state
`GetLayout()` SHALL return the non-fullscreen layout in all cases. If the window is in fullscreen (`WindowStyle = None`, `WindowState = Maximized`), `GetLayout()` SHALL use the pre-fullscreen stored bounds rather than `RestoreBounds` or the current maximised dimensions.

#### Scenario: GetLayout during fullscreen returns pre-fullscreen bounds
- **WHEN** `GetLayout()` is called while the window is in fullscreen
- **THEN** the returned `VideoWindowLayout` reflects the pre-fullscreen `Left`, `Top`, `Width`, `Height`, and `IsMaximised` — not the fullscreen dimensions

#### Scenario: GetLayout when not in fullscreen is unchanged
- **WHEN** `GetLayout()` is called while the window is in its normal windowed state
- **THEN** behaviour is identical to the existing contract (uses `RestoreBounds` when `WindowState == Maximized`, otherwise uses current `Left/Top/Width/Height`)

---

### Requirement: Ctrl+Tab returns focus to the operator UI from fullscreen
Pressing `Ctrl+Tab` while a `VlcDisplayWindow` is in fullscreen mode SHALL activate `Application.Current.MainWindow`, returning keyboard focus to the operator UI. This behaviour is already implemented for windowed mode and SHALL continue to work in fullscreen.

#### Scenario: Ctrl+Tab activates MainWindow from fullscreen
- **WHEN** the `VlcDisplayWindow` is in fullscreen and the operator presses `Ctrl+Tab`
- **THEN** `Application.Current.MainWindow` is activated and receives keyboard focus
