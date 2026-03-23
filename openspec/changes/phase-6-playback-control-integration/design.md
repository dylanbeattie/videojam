## Context

Phases 1–5 have produced a fully-implemented operator UI and all engine subsystems: `AudioEngine` (NAudio multi-stem pipeline), `VideoEngine` (LibVLC multi-display), `SyncCoordinator` (sub-1ms A/V dispatch), `DisplayManager` (multi-monitor enumeration and VlcDisplayWindow management), and `MainViewModel` (MVVM shell with show file operations and mixer panel). What is missing is the conductor — a component that sequences these engines for a live performance and connects them to real operator input.

Phase 6 introduces two new components:
1. **`PlaybackEngine`** — a state machine orchestrating all engine subsystems per-song
2. **`HotkeyService`** — a global keyboard hook delivering Button A / Button B events

It also wires `MainViewModel` to `PlaybackEngine` so the existing UI reflects live state, and adjusts Display 0 visibility so the operator UI disappears during playback.

## Goals / Non-Goals

**Goals:**
- A complete end-to-end performance workflow driven by two keys
- Correct state machine transitions with no invalid states reachable
- Display 0 takeover and restoration around each song
- Auto-advance to the next song at natural end-of-content
- Pre-loading of audio/video pipelines on cue, before GO is pressed
- Key bindings configurable without recompilation

**Non-Goals:**
- Graceful recovery from file-not-found or corrupted audio (Phase 7)
- Structured logging to disk (Phase 7)
- Display disconnect recovery (Phase 7)
- Pause-and-resume mid-song (the spec calls Escape "pause then rewind" — actual in-place resume is not required; Escape twice = full rewind to `Cued`)

## Decisions

### Decision 1: PlaybackEngine owns all engine subsystems

**Choice:** `PlaybackEngine` holds direct references to `AudioEngine`, `VideoEngine`, `SyncCoordinator`, and `DisplayManager`, and creates fresh `AudioEngine`/`VideoEngine` instances per song.

**Alternatives considered:**
- *Single long-lived AudioEngine/VideoEngine per app session* — cannot cleanly support Stop→Reload→Go without leaking NAudio/LibVLC state; the existing `Stop()` + `Dispose()` contracts are designed for per-song lifetime.
- *Event-driven pipeline (engines emit events, PlaybackEngine reacts)* — adds unnecessary indirection; the state machine is the right home for sequencing logic.

**Rationale:** Per-song instances match the engine contracts as specced. `PlaybackEngine.Cue()` disposes any existing engines before creating new ones, keeping resource management predictable.

---

### Decision 2: PlaybackState as a simple enum, state guarded by PlaybackEngine

**Choice:** `PlaybackState { Idle, Cued, Playing, Paused }` is defined in `VideoJam/Model/AppState.cs`. `PlaybackEngine` is the single writer; `MainViewModel` exposes it read-only, bound from `PlaybackEngine.StateChanged`.

**Alternatives considered:**
- *ViewModel owns state* — creates dual-source-of-truth; engine state and UI state can diverge.
- *Reactive property chain* — complexity not warranted by this size of state machine.

**Rationale:** Single writer removes the class of bugs where the UI thinks the app is Idle but the engine is still playing.

---

### Decision 3: HotkeyService uses WH_KEYBOARD_LL on the UI thread's message loop

**Choice:** Install the low-level keyboard hook via `SetWindowsHookEx(WH_KEYBOARD_LL, …)` with `hMod = IntPtr.Zero` and `dwThreadId = 0` (system-wide), called from the WPF UI thread. Events are marshalled to `Application.Current.Dispatcher` before `PlaybackEngine` is called.

**Alternatives considered:**
- *KeyDown on MainWindow* — only captures input when MainWindow has focus. During playback the operator UI is hidden, so this would never fire.
- *Raw Input API* — more complex, no advantage for two-key use case.
- *Dedicated hook thread with its own message pump* — technically cleaner but requires inter-thread marshalling for every keypress; WPF UI thread already has a message loop, so no pump is needed if installed there.

**Rationale:** Installing on the UI thread is the simplest correct approach. The hook callback must be fast (no blocking calls) — it raises an event and returns immediately, which is trivially achievable.

**Risk:** If a third-party accessibility or keyboard remapping tool installs its own `WH_KEYBOARD_LL` hook and does not pass events down the chain, VideoJam's hook will not receive them. This is documented in the operator guide as a known limitation; the workaround is to configure those tools to pass through.

---

### Decision 4: Cue() is synchronous/async and pre-loads pipelines

**Choice:** `PlaybackEngine.Cue(int songIndex)` is `async Task`. It:
1. Disposes any existing `AudioEngine`/`VideoEngine` (previous song's resources).
2. Scans the song folder (`SongScanner.Scan()`).
3. Creates a new `AudioEngine`, calls `AudioEngine.Load()`.
4. Calls `VideoEngine.LoadAll()` for all displays concurrently.
5. Transitions state to `Cued`.

If scanning or loading fails, the state remains `Idle` (no partial-Cued state).

**Rationale:** Pre-loading on cue means GO press → playback with no perceivable delay. The LoadAll() concurrency is already specced in VideoEngine.

---

### Decision 5: MainWindow visibility managed by PlaybackEngine callbacks, not the ViewModel

**Choice:** `PlaybackEngine` holds a `MainWindow` reference (injected at construction) and calls `Hide()`/`Show()` directly from the engine's event handlers, which are always invoked on the dispatcher.

**Alternatives considered:**
- *ViewModel property `IsMainWindowVisible` bound to Window.Visibility* — requires the window to remain in the WPF visual tree while hidden; fine, but the window may intercept input while hidden unless `IsHitTestVisible` is also managed.
- *Callback/delegate injected at construction* — cleaner decoupling, not needed at this scale.

**Rationale:** Direct `Hide()`/`Show()` is the idiomatic WPF approach; the window is invisible and passes no input when hidden. `PlaybackEngine` is constructed in `App.xaml.cs` after `MainWindow` is created, so the reference is available.

---

### Decision 6: appsettings.json for key binding configuration

**Choice:** A `appsettings.json` file in the application directory contains `"HotkeySettings": { "ButtonA": "Space", "ButtonB": "Escape" }`. `HotkeyService` reads this at construction via `System.Text.Json`. If the file is absent or the keys are missing, defaults are used silently.

**Rationale:** No additional NuGet packages needed (`System.Text.Json` is already a dependency). The file is human-editable without recompilation and can be included in the Phase 8 release zip with documentation.

## Risks / Trade-offs

| Risk | Mitigation |
|------|-----------|
| `WH_KEYBOARD_LL` hook silently not firing (e.g. third-party intercept, or hook installed off UI thread) | Log a `Debug` message immediately when the hook is installed; add a Phase 6 manual test step that verifies Space triggers GO |
| `PlaybackEngine.Cue()` called while a previous Cue is still async-loading | Guard with a `CancellationToken` per-cue; if Cue is called again, cancel the previous load task |
| Display 0 `VlcDisplayWindow.Topmost = true` may sit above `MainWindow` when MainWindow is restored | Confirm `MainWindow` restoration is followed by `MainWindow.Activate()` to bring it to the foreground |
| `PlaybackEnded` event (from `AudioEngine`) fires on the WASAPI callback thread and must be marshalled to the dispatcher | `AudioEngine` already specifies UI-thread dispatch for `PlaybackEnded`; confirm implementation matches spec before Phase 6 integration |
| Auto-advance cues next song immediately — if scanning takes >500ms, the operator sees a brief loading state | `PlaybackEngine` sets state to `Idle` while loading (shows a loading indicator in `StatusText`), only transitions to `Cued` once ready |

## Open Questions

None blocking implementation. The following are noted for Phase 7:
- Should `PlaybackEngine` expose pause/resume at some future point, or is Stop+Rewind the permanent UX?
- Is there a requirement for a "jump to song" action via hotkey (e.g. numeric keys)?
