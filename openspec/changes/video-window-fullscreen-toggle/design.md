## Context

`VlcDisplayWindow` is currently a freely resizable, titled window (`SingleBorderWindow`, `CanResize`). The operator positions windows across displays before the show. During a performance, double-clicking the window should instantly fill the display it is on — with no title bar or border — matching typical media player UX. Exiting fullscreen must restore the window exactly to where it was.

The window is entirely view-layer code-behind (no ViewModel involvement). All fullscreen logic belongs here. The `GetLayout()` / `ApplyLayout()` persistence contract must be unaffected.

## Goals / Non-Goals

**Goals:**
- Double-click anywhere in the window toggles fullscreen on/off
- Fullscreen covers the full physical display including the taskbar (true fullscreen, not WPF Maximized)
- Exiting fullscreen restores the exact pre-fullscreen `Left`, `Top`, `Width`, `Height`, and `WindowState`
- `Ctrl+Tab` continues to return focus to the operator UI while in fullscreen
- `GetLayout()` returns the windowed layout regardless of fullscreen state

**Non-Goals:**
- Persisting fullscreen state to the `.show` file
- F11 keyboard shortcut
- Fullscreen triggered from the operator UI (MainWindow)
- Any change to `GetLayout()` / `ApplyLayout()` behaviour

## Decisions

### Decision 1: True fullscreen via WindowStyle swap, not WPF Maximized

**Chosen:** Set `WindowStyle = None`, `ResizeMode = NoResize`, then `WindowState = Maximized`.

**Why:** `WindowState.Maximized` alone respects the taskbar — the window stops at the taskbar boundary. True fullscreen (covering the taskbar) requires removing the window chrome first. This is the standard WPF pattern for full-display coverage.

**Alternative considered:** `WindowState.Maximized` only — rejected because it leaves the taskbar visible, which is distracting during a performance.

**Restore sequence:** Must be `WindowState = Normal` → `WindowStyle = SingleBorderWindow` → `ResizeMode = CanResize` → restore `Left/Top/Width/Height` → restore prior `WindowState`. Setting `WindowStyle` before `WindowState = Normal` causes WPF to miscalculate the restore position.

### Decision 2: Store pre-fullscreen state in private fields, not VideoWindowLayout

**Chosen:** Store `_preFullscreenLeft`, `_preFullscreenTop`, `_preFullscreenWidth`, `_preFullscreenHeight`, and `_preFullscreenState` as private fields on the window.

**Why:** `VideoWindowLayout` is a persistence model; adding fullscreen-transient state to it would contaminate the save/load contract. Private fields are the simplest correct solution for transient view state.

**Alternative considered:** Calling `GetLayout()` at the moment of entering fullscreen and caching the result — also valid, but creates an implicit dependency between `GetLayout()` and fullscreen enter, which is not its intended purpose.

### Decision 3: MouseDoubleClick event handler in code-behind

**Chosen:** Override `OnMouseDoubleClick` in `VlcDisplayWindow.xaml.cs` and call `ToggleFullscreen()`.

**Why:** This is pure view-lifecycle glue — no business logic. The project convention explicitly permits code-behind for this category. A command binding in the ViewModel would be architecturally incorrect: the fullscreen state is a property of the physical window, not of the show or playback state.

### Decision 4: GetLayout() ignores fullscreen state

**Chosen:** `GetLayout()` uses `RestoreBounds` when `WindowState == Maximized`, and `Left/Top/Width/Height` otherwise. Fullscreen mode sets `WindowStyle = None` but also `WindowState = Maximized`, so `RestoreBounds` will correctly return the pre-fullscreen bounds.

**Why:** This behaviour already works correctly without any change — WPF's `RestoreBounds` tracks the last non-maximised bounds, which are set before entering fullscreen. No modification to `GetLayout()` is needed.

## Risks / Trade-offs

**[Risk] WPF layout artefact on fullscreen exit** → Mitigation: always set `WindowState = Normal` before restoring `WindowStyle`. The restore sequence in `ToggleFullscreen()` must follow this order without exception.

**[Risk] Double-click during playback could distract the operator** → Accepted. The feature is intentional. There is no state-gating required — the operator can toggle fullscreen at any time.

**[Risk] Window on a display that is later disconnected** → Already a known limitation (documented in TECHNICAL_SPEC §8). Fullscreen adds no new risk here.

**[Risk] `RestoreBounds` is `Rect.Empty` if the window was never moved from its initial position** → Mitigation: capture `Left/Top/Width/Height` directly into the private fields at the moment of entering fullscreen, rather than relying on `RestoreBounds` for the restore path. This is always safe.

## Migration Plan

No migration required. This is a new interaction behaviour on an existing window class. No `.show` file schema changes, no new dependencies, no data migration.

## Open Questions

None. The implementation is fully specified.
