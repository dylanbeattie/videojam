## MODIFIED Requirements

### Requirement: Browsing a fallback image immediately updates the playback engine
When the operator selects a fallback image via the Browse button, the selected image SHALL be immediately loaded into `PlaybackEngine` and displayed in all currently-open video windows. The update SHALL NOT wait for the next `UpdateShow()` call.

`MainViewModel.ExecuteBrowseFallbackImage()` SHALL, after setting `_loadedShow.FallbackImagePath`, call `PlaybackEngine.SetFallbackImage(absolutePath)` with the absolute path of the selected file.

`PlaybackEngine.SetFallbackImage(string? absolutePath)` SHALL:
- Load the image from `absolutePath` into `_fallbackImage` (using the same BitmapImage construction as `LoadFallbackImage()`)
- Call `ShowFallback(_fallbackImage)` on every window in `_videoWindows` via the UI dispatcher
- Set `_fallbackImage = null` and log a warning if loading fails, leaving windows showing solid black

#### Scenario: Browsing a new fallback image updates open video windows immediately
- **WHEN** the operator selects a PNG file via the Browse button while one or more video windows are open
- **THEN** all open video windows display the new fallback image without requiring a song cue or application restart

#### Scenario: Browsing a fallback image before any windows are open stores the image for later
- **WHEN** the operator selects a PNG file via the Browse button before any song has been cued (no windows open)
- **THEN** `_fallbackImage` is loaded into the engine so that the next `EnsureVideoWindows()` call will use the correct image

#### Scenario: Browsing a non-existent or unreadable path degrades gracefully
- **WHEN** `SetFallbackImage()` is called with a path that cannot be loaded
- **THEN** `_fallbackImage` is set to null, a warning is logged, and any open windows call `ShowFallback(null)` (solid black) — no exception is thrown

---

## MODIFIED Requirements

### Requirement: UpdateShow refreshes the fallback image in already-open windows
When `PlaybackEngine.UpdateShow(Show show)` is called and video windows are already open (from a prior song cue), those windows SHALL have their fallback image refreshed to match the new show's `FallbackImagePath`.

After `LoadFallbackImage()` completes inside `UpdateShow()`, `PlaybackEngine` SHALL call `ShowFallback(_fallbackImage)` on every window in `_videoWindows` via the UI dispatcher.

#### Scenario: Loading a different show refreshes the fallback in open windows
- **WHEN** the operator loads a second show file while video windows from the previous show are still visible
- **THEN** all open video windows immediately display the new show's fallback image (or solid black if none is set)

#### Scenario: UpdateShow with no open windows does not throw
- **WHEN** `UpdateShow()` is called before any song has been cued (empty `_videoWindows`)
- **THEN** no exception is thrown and `_fallbackImage` is loaded ready for the first `EnsureVideoWindows()` call
