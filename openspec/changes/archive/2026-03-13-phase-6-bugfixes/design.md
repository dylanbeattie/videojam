## Context

Phase 6 introduced `PlaybackEngine`, `HotkeyService`, and wired the full end-to-end playback flow. User testing revealed three blocking defects:

1. **Pre-buffer race**: `VideoEngine.Load()` calls `player.SetPause(true)` immediately after `player.Play()`. LibVLC processes `Play()` asynchronously; when two videos load concurrently the second player is often still in `Opening` state when `SetPause(true)` arrives. VLC silently ignores a pause request in `Opening` state, the `Paused` event never fires, the 2-second timeout triggers, and that display's slot is never registered — the performer video stays black for the entire song.

2. **Topmost window buries operator UI**: `VlcDisplayWindow` carries `Topmost="True"` in XAML, and the window is shown as soon as `EnsureDisplayWindows()` runs during `Cue()`. When the video display is on the same physical screen as `MainWindow`, the topmost window buries it at the moment the operator first clicks a song — before playback even begins. `PlaybackEngine.Go()` then calls `_mainWindow.Hide()`, removing the window entirely during the performing state.

3. **No transport controls**: The operator must use global hotkeys (Space/Escape) to control playback. There are no on-screen buttons, making one-handed or mouse-driven operation impossible.

## Goals / Non-Goals

**Goals:**
- Performer video pre-buffers and plays reliably when loaded concurrently with other videos
- `VlcDisplayWindow` never covers `MainWindow` involuntarily; Z-order is managed by explicit `Activate()` calls
- `MainWindow` is always visible; operator can switch to it via Ctrl+Tab from any display window
- Playback toolbar (Go, Pause, Stop/Rewind) visible at all times in `MainWindow`

**Non-Goals:**
- Per-display `Topmost` configuration (e.g., topmost only on projector displays)
- Fixing any NAudio or SyncCoordinator behaviour
- Changes to the show file format or SongScanner
- Pause-and-resume mid-song (two-phase stop/rewind behaviour is unchanged)

## Decisions

### Decision 1: Gate `SetPause(true)` on the `Playing` event

**Choice:** Subscribe to both `Playing` and `Paused` events before calling `player.Play()`. In the `Playing` event handler (which fires on a VLC internal thread once the decoder pipeline is warm), call `player.SetPause(true)`. Await the `Paused` event to confirm the player is paused and ready at frame 0.

**Sequence:**
```
player.Playing += OnPlayerPlaying;   // step 1: set up
player.Paused  += OnPlayerPaused;    // step 1: set up
player.Play();                        // step 2: kick off

OnPlayerPlaying fires (VLC thread)
  → player.SetPause(true)            // step 3: now safe to pause

OnPlayerPaused fires (VLC thread)
  → prebufferTcs.TrySetResult(true)  // step 4: signal completion

await prebufferTcs + timeout guard   // step 5: back on caller's thread
player.Time = 0;                     // step 6: rewind to frame 0
window.ShowVideo();
```

**Alternatives considered:**
- *Call `SetPause(true)` with a delay (e.g. 50 ms)* — fragile; timing depends on hardware and file size.
- *Subscribe only to `Paused`; call `SetPause(true)` immediately* — current broken approach. Works when VLC is fast but fails for the second concurrent load.
- *Use `player.State` polling loop* — blocks a thread, not idiomatic async/await.

**Rationale:** The `Playing` event is LibVLC's authoritative signal that the decoder pipeline is warm and a pause request will be honoured. Subscribing to it is the correct, race-free gate.

---

### Decision 2: Remove `Topmost`, remove `_mainWindow.Hide()` / `Show()`

**Choice:**
- Remove `Topmost="True"` from `VlcDisplayWindow.xaml`.
- Remove `_mainWindow.Hide()` from `PlaybackEngine.Go()`.
- Remove the matching `_mainWindow.Show()` / `Activate()` calls from `StopAndRewind()` (phase 2) and `OnPlaybackEnded()`.
- On `Go()`: call `window.Activate()` on each `VlcDisplayWindow` to bring display windows to the front.
- On stop/end: call `_mainWindow.Activate()` to return focus to the operator UI.

**Alternatives considered:**
- *Keep `Topmost` but only set it on secondary displays* — requires identifying "which display is the operator's display", which is not in the current data model and risks incorrect heuristics.
- *Create `VlcDisplayWindow` without showing it until `Go()`* — solves the Cued-state burial issue but adds HWND lifecycle complexity (need `WindowInteropHelper.EnsureHandle()` before VLC can get the HWND) and means the fallback image doesn't show between Cue and Go.
- *Keep `Topmost`, add a `Topmost = false` toggle when `MainWindow` is activating* — Z-order fights; WPF does not guarantee atomic topmost state changes.

**Rationale:** Removing `Topmost` and `Hide()` is the minimal change with the least side-effect surface. The operator's display is their own — other apps covering the video during performance is a deployment concern, not an application responsibility at this phase. `Activate()` is sufficient to bring the intended window forward after state transitions.

---

### Decision 3: Ctrl+Tab in `VlcDisplayWindow` returns focus to `MainWindow`

**Choice:** Add a `KeyDown` handler to `VlcDisplayWindow.xaml.cs` that listens for `Ctrl+Tab`. When detected, call `Application.Current.MainWindow.Activate()`.

**Alternatives considered:**
- *Global hotkey in `HotkeyService`* — would require a new configurable key, adding config surface.
- *WPF `InputBinding` on the window* — `Ctrl+Tab` is a routed key gesture and works cleanly as an `InputBinding` with a `KeyBinding`.

**Rationale:** A direct `KeyDown` handler is the simplest, most discoverable approach. `VlcDisplayWindow.xaml.cs` already has an `OnLoaded` handler, so code-behind is established. This is view-lifecycle glue, not business logic.

---

### Decision 4: Toolbar in `MainWindow` above the main panel

**Choice:** Add a WPF `ToolBar` (inside a `ToolBarTray`) between the `Menu` and the main content area in `MainWindow.xaml`. Buttons: **▶ Go** (bound to `GoCommand`), **⏸ Pause** (bound to `StopAndRewindCommand`, visible when `Playing`), **⏮ Stop/Rewind** (bound to `StopAndRewindCommand`, visible when `Paused`). A loading indicator appears when `IsLoading` is true. All buttons are standard WPF `Button` elements inside the `ToolBar`; no third-party control needed.

**Pause vs Stop/Rewind labelling:** Because `StopAndRewindCommand` performs a different action depending on state (Playing → Paused vs Paused → Cued), the toolbar shows different button content per state using `Style.Triggers` bound to `PlaybackState`.

**Alternatives considered:**
- *Single "Stop" button that handles both phases* — less clear UX; operator doesn't know which phase they're in.
- *Floating always-on-top HUD window* — more complexity; not needed now that MainWindow is always visible.

**Rationale:** A single toolbar row above the content is the standard WPF pattern and requires no new ViewModel commands or infrastructure.

## Risks / Trade-offs

| Risk | Mitigation |
|------|-----------|
| `Playing` event fires but `SetPause(true)` is still too fast for VLC to accept | 2-second timeout still applies; worst case is pre-buffer timeout with a log warning, same graceful degradation as before |
| `Activate()` may not bring a window to the foreground if the OS blocks foreground-window switches (e.g., another app is fullscreen) | Document as known limitation; operator can Alt+Tab if `Activate()` is blocked by OS |
| Removing `Topmost` allows notifications / other apps to cover the video output during performance | Deployment guide: close all non-essential apps before a performance; OS-level "Do Not Disturb" is the correct mitigation |
| `Ctrl+Tab` is used by some OS-level accessibility tools | Binding is window-scoped in `VlcDisplayWindow`; if `VlcDisplayWindow` doesn't have focus (another app intercepted Ctrl+Tab), the binding simply doesn't fire |

## Open Questions

None blocking implementation.
