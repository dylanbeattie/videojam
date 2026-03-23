## Context

`PlaybackEngine` owns `_fallbackImage` (a frozen `BitmapImage?`) and `_videoWindows` (a `Dictionary<int, VlcDisplayWindow>`). The image is loaded in `LoadFallbackImage()`, called only from `UpdateShow()`. Video windows call `ShowFallback(_fallbackImage)` when created, when a song ends, and when playback stops.

`MainViewModel.ExecuteBrowseFallbackImage()` sets `_loadedShow.FallbackImagePath` directly on the in-memory `Show` object but never calls back into `PlaybackEngine`. Because `UpdateShow()` was already called (at show-open time), `_fallbackImage` in the engine is forever stale relative to what the operator just chose.

## Goals / Non-Goals

**Goals:**
- When the operator browses a new fallback image, the engine reloads it and immediately updates all open video windows
- When `UpdateShow()` is called and video windows already exist, those windows get the refreshed fallback image
- Thread safety: `_fallbackImage` and `_videoWindows` are only ever touched on the UI thread

**Non-Goals:**
- Creating video windows eagerly at show-open time (windows remain lazy — created on first Cue)
- Persisting or changing the fallback image model or schema
- Any change to `ShowFallback()` / `ShowVideo()` on `VlcDisplayWindow`

## Decisions

### Decision 1: New `SetFallbackImage(string? absolutePath)` method on PlaybackEngine

**Chosen:** Add a single public method that (a) calls the existing `LoadFallbackImage()` logic with the new path, (b) calls `ShowFallback(_fallbackImage)` on all currently-open `_videoWindows`.

**Why:** Keeps the fix minimal and surgical. `MainViewModel` already knows the absolute path at the moment the user picks the file (`path` variable in `ExecuteBrowseFallbackImage()`), so no path resolution is needed on the engine side — just pass it in directly.

**Alternative considered:** Re-calling `UpdateShow(_loadedShow)` from the ViewModel — rejected because `UpdateShow` does more than just reload the fallback (it rescans state), making the callsite semantics confusing and the behaviour harder to reason about.

### Decision 2: Pass absolute path directly; do not re-read from Show model

**Chosen:** `SetFallbackImage(string? absolutePath)` accepts the resolved absolute path as a parameter, rather than reading `_show.FallbackImagePath` again.

**Why:** At call time in `ExecuteBrowseFallbackImage()`, the absolute path is the local variable `path` (before it is relativised and stored in the model). Reading back from the model would require the caller to normalize the stored relative path first — that's unnecessary complexity when we have the absolute path in hand.

### Decision 3: UI-thread-only access — no locking required

**Chosen:** `SetFallbackImage()` is called from `MainViewModel` (UI thread). `_videoWindows` is only mutated from `EnsureVideoWindows()`, which runs inside `Cue()` — but `Cue()` is guarded by a state machine that prevents it from running concurrently with UI actions. No mutex needed.

**Why:** The existing codebase uses a UI-thread-affinity model throughout. Adding a lock would be inconsistent and introduce complexity without benefit.

### Decision 4: UpdateShow also refreshes open windows

**Chosen:** At the end of `UpdateShow()`, after `LoadFallbackImage()`, iterate `_videoWindows` and call `ShowFallback(_fallbackImage)` on each via `Dispatcher.Invoke`.

**Why:** Covers the case where the operator loads a different show while video windows are already open from a prior session. Without this, windows would keep showing the old show's fallback until the next `Cue()`.

## Risks / Trade-offs

**[Risk] `_fallbackImage` loaded on UI thread may block briefly for large PNGs** → Mitigation: `BitmapCacheOption.OnLoad` is already set; the image is fully decoded at load time and frozen. For a performance PNG (typically < 2 MB), this is imperceptible.

**[Risk] `SetFallbackImage()` called with a path that no longer exists** → Mitigation: `LoadFallbackImage()` already wraps the BitmapImage construction in a try/catch and logs a warning; `_fallbackImage` is set to null on failure, so windows will show solid black — the existing graceful-degradation behaviour.

## Migration Plan

No data migration. Two-file change. No schema changes.

## Open Questions

None.
