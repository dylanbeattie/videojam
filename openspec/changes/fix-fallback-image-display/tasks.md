## 1. PlaybackEngine — SetFallbackImage method

- [x] 1.1 Add `public void SetFallbackImage(string? absolutePath)` to `PlaybackEngine`
- [x] 1.2 Implement: set `_show.FallbackImagePath` to the provided path, then call the existing `LoadFallbackImage()` logic to update `_fallbackImage`
- [x] 1.3 After loading, iterate `_videoWindows` and call `window.ShowFallback(_fallbackImage)` on each via `Dispatcher.Invoke`

## 2. PlaybackEngine — UpdateShow refresh

- [x] 2.1 In `UpdateShow()`, after the existing `LoadFallbackImage()` call, add a loop to call `ShowFallback(_fallbackImage)` on all windows in `_videoWindows` via `Dispatcher.Invoke`

## 3. MainViewModel — wire the call

- [x] 3.1 In `ExecuteBrowseFallbackImage()`, after setting `_loadedShow.FallbackImagePath`, call `_playbackEngine?.SetFallbackImage(path)` using the local `path` variable (the absolute path before relativisation)

## 4. Manual Verification

- [ ] 4.1 Open a show, set a fallback image, cue a song, stop — verify the fallback PNG is visible in the video window
- [ ] 4.2 With windows already open, browse a new fallback image — verify all windows update immediately without cueing
- [ ] 4.3 Browse a fallback image before cueing any song, then cue — verify the fallback shows correctly before video starts
- [ ] 4.4 Load a second show while windows are open — verify windows show the new show's fallback image
