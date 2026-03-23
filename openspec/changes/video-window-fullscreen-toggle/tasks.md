## 1. State and Private Fields

- [x] 1.1 Add `_isFullscreen` boolean field to `VlcDisplayWindow`
- [x] 1.2 Add `_preFullscreenLeft`, `_preFullscreenTop`, `_preFullscreenWidth`, `_preFullscreenHeight` (double) fields
- [x] 1.3 Add `_preFullscreenState` (WindowState) field

## 2. ToggleFullscreen Implementation

- [x] 2.1 Add `private void ToggleFullscreen()` method to `VlcDisplayWindow.xaml.cs`
- [x] 2.2 Implement enter-fullscreen path: capture pre-fullscreen state into fields, then set `WindowStyle = None`, `ResizeMode = NoResize`, `WindowState = Maximized`, set `_isFullscreen = true`
- [x] 2.3 Implement exit-fullscreen path: `WindowState = Normal`, then restore `WindowStyle = SingleBorderWindow` and `ResizeMode = CanResize`, then restore `Left/Top/Width/Height`, then restore `_preFullscreenState`, set `_isFullscreen = false`

## 3. Double-Click Handler

- [x] 3.1 Override `OnMouseDoubleClick` in `VlcDisplayWindow.xaml.cs` to call `ToggleFullscreen()` and mark event as handled

## 4. GetLayout Update

- [x] 4.1 Update `GetLayout()` to check `_isFullscreen`: if true, return a `VideoWindowLayout` built from the `_preFull*` fields (with `IsMaximised = _preFullscreenState == WindowState.Maximized`) instead of the current window bounds

## 5. Manual Verification

- [ ] 5.1 Double-click a video window → verify it goes fullscreen (no title bar, covers taskbar)
- [ ] 5.2 Double-click again → verify window restores to exact previous position and size
- [ ] 5.3 Resize/move window, double-click to fullscreen, exit → verify restored to new position
- [ ] 5.4 Maximise window (via title bar), then double-click to fullscreen, exit → verify restored to maximised state
- [ ] 5.5 While in fullscreen, press `Ctrl+Tab` → verify operator UI receives focus
- [ ] 5.6 Save show while video window is in fullscreen → reload show → verify layout matches pre-fullscreen position (not fullscreen dimensions)
- [ ] 5.7 Fullscreen on a secondary display → verify it fills the correct display
