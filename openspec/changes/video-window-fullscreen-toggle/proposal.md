## Why

During a live performance, the operator needs to be able to maximise a video window to fill an entire display — removing the title bar and border entirely — without needing the mouse or a second interaction. A double-click on any video window should toggle between windowed and fullscreen mode instantly.

## What Changes

- Double-clicking anywhere inside a `VlcDisplayWindow` toggles the window between its normal windowed state and a true fullscreen state (borderless, no title bar, covers the full display including the taskbar).
- Exiting fullscreen restores the window to its previous position, size, and `WindowStyle`.
- The fullscreen state is **not** persisted to the `.show` file — it is a transient presentation state, not a layout setting.
- `Ctrl+Tab` (return focus to operator UI) continues to work in fullscreen mode.

## Capabilities

### New Capabilities
- `video-window-fullscreen`: Double-click toggles `VlcDisplayWindow` between windowed and fullscreen. Includes enter/exit logic, state tracking, and restore-bounds behaviour.

### Modified Capabilities
- `video-engine`: `VlcDisplayWindow` gains a new interaction behaviour (double-click handler). The existing `GetLayout()` / `ApplyLayout()` contract is unchanged — layout capture continues to use the non-fullscreen restore bounds.

## Impact

- **`VideoJam/UI/VlcDisplayWindow.xaml`** — add `MouseDoubleClick` event handler binding
- **`VideoJam/UI/VlcDisplayWindow.xaml.cs`** — implement `ToggleFullscreen()`, track pre-fullscreen state, restore on exit
- **No engine, model, service, or test changes required** — this is pure view-layer behaviour
- **Non-goals for this phase**: persisting fullscreen state to the show file; keyboard shortcut (F11) for fullscreen; fullscreen triggered from the operator UI; any change to `GetLayout()` / `ApplyLayout()`

## Primary Technical Risk

WPF's `WindowState.Maximized` respects the taskbar; true fullscreen (covering the taskbar) requires setting `WindowStyle = None` and `ResizeMode = NoResize` before maximising. Restoring must reverse this sequence precisely — if `WindowStyle` is restored after `WindowState`, WPF may not reposition the window correctly. The implementation must set `WindowState = Normal` before restoring `WindowStyle` to avoid layout artefacts.
