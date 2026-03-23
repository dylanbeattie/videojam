## MODIFIED Requirements

### Requirement: VlcDisplayWindow is a freely positionable, resizable window
A `VlcDisplayWindow` SHALL be a WPF `Window` with:
- `WindowStyle="SingleBorderWindow"` — standard title bar and border (default windowed state)
- `ResizeMode="CanResize"` — freely resizable by the operator (default windowed state)
- No `Topmost` property (default `false`) — must not obscure the operator UI

It SHALL have a black `Background` so unrendered areas do not show system chrome.

After the window is `Loaded`, it SHALL expose its Win32 HWND via `nint Hwnd`.

The window is keyed by `int SlotIndex` (zero-based). The title bar SHALL read `"VideoJam — Video {SlotIndex + 1}"`, set via `UpdateTitle()`.

The window SHALL support a **fullscreen mode** (see `video-window-fullscreen` capability) in which `WindowStyle`, `ResizeMode`, and `WindowState` temporarily differ from the above defaults. The HWND remains valid and unchanged during all fullscreen transitions.

#### Scenario: HWND is available after Loaded
- **WHEN** the `VlcDisplayWindow.Loaded` event has fired
- **THEN** `VlcDisplayWindow.Hwnd` is a non-zero, valid Win32 window handle

#### Scenario: Window title reflects slot index
- **WHEN** `SlotIndex` is set to `1` and `UpdateTitle()` is called
- **THEN** the window title is `"VideoJam — Video 2"`

#### Scenario: HWND is unchanged after entering and exiting fullscreen
- **WHEN** the operator double-clicks to enter fullscreen and then double-clicks again to exit
- **THEN** `VlcDisplayWindow.Hwnd` is the same non-zero value as before the transition
