## ADDED Requirements

### Requirement: VideoEngine loads all display slots concurrently

`VideoEngine.LoadAll(SongManifest manifest, IReadOnlyDictionary<int, VlcDisplayWindow> windows, CancellationToken cancellationToken)` SHALL call `Load()` concurrently for every `(displayIndex, window)` pair in `windows` and await all calls via `Task.WhenAll`. Each `Load()` call receives the full `manifest`, the pair's display index, and the pair's window.

If a `Load()` call times out during pre-buffering, it returns without adding a slot (per the existing pre-buffer timeout requirement) — `LoadAll()` SHALL still complete normally. If a `Load()` call throws an unhandled exception, `LoadAll()` SHALL propagate it to the caller.

#### Scenario: Two displays load concurrently
- **WHEN** `LoadAll` is called with a manifest containing video files for display indices 0 and 1, and `windows` contains entries for both indices
- **THEN** both `Load()` calls are dispatched concurrently and `LoadAll` completes after both have finished pre-buffering

#### Scenario: Pre-buffer timeout on one display does not prevent the other from loading
- **WHEN** `LoadAll` is called for two displays and the pre-buffer for display 1 times out
- **THEN** `LoadAll` completes normally, display 0 is in its pre-buffered state, and display 1 remains in fallback state

#### Scenario: Empty windows dictionary completes immediately
- **WHEN** `LoadAll` is called with an empty `windows` dictionary
- **THEN** `LoadAll` returns a completed task without error
