## Why

The fallback PNG image — intended to display in all video windows between songs and before the first song — never actually appears. Code review and tracing reveals two bugs that together prevent it from ever showing.

## What Changes

- **Bug 1 (primary):** When the operator browses and selects a fallback image via the Browse button, `MainViewModel` updates `_loadedShow.FallbackImagePath` but never notifies `PlaybackEngine`. The engine's `_fallbackImage` field remains `null` (set during the earlier `UpdateShow()` call, before the image was chosen). When the first song is cued, `EnsureVideoWindows()` calls `ShowFallback(null)` — the windows show solid black, not the fallback image. Fix: after `ExecuteBrowseFallbackImage()` sets the path, call a new `PlaybackEngine.SetFallbackImage(string? absolutePath)` method that reloads the image and immediately updates any already-open video windows.

- **Bug 2 (secondary):** Video windows are created lazily inside `Cue()` — they do not exist when a show is first opened. This means there is nowhere to display the fallback image at show-open time. Fix: when `UpdateShow()` is called on `PlaybackEngine` and video windows already exist from a prior session (e.g. the operator loads a different show mid-performance), those windows should have their fallback image refreshed immediately. New windows will correctly show the fallback at the moment `EnsureVideoWindows()` creates them.

## Capabilities

### New Capabilities
None.

### Modified Capabilities
- `show-file-operations`: The operator's "Browse fallback image" action must propagate the new image to the playback engine immediately, not only at the next `UpdateShow()` call.

## Impact

- **`VideoJam/Engine/PlaybackEngine.cs`** — add `SetFallbackImage(string? absolutePath)` public method; call `LoadFallbackImage()` then `ShowFallback()` on all open `_videoWindows`
- **`VideoJam/UI/ViewModels/MainViewModel.cs`** — after setting `_loadedShow.FallbackImagePath` in `ExecuteBrowseFallbackImage()`, call `_playbackEngine.SetFallbackImage(absolutePath)`
- No model, service, XAML, or schema changes required
- No new dependencies

## Primary Technical Risk

`SetFallbackImage()` may be called from the UI thread while `EnsureVideoWindows()` (called from `Cue()`, which runs on a background task) is also accessing `_videoWindows`. Access to `_videoWindows` and `_fallbackImage` must be marshalled to the UI thread or protected appropriately. Since `SetFallbackImage()` is only callable before playback starts (the Browse button is not accessible during playback), the practical risk is low — but the implementation must be thread-safe by construction.
