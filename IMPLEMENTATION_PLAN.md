# VideoJam — Implementation Plan

**Version:** 2.1
**Date:** 2026-03-23
**Status:** Complete
**Covers:** Mixer fix, dockable video windows, show file migration

---

## Overview

This plan delivers three workstreams in four phases:

1. **Phase 7A** — Fix mixer controls (Defect #2)
2. **Phase 7B** — Model changes (show file v2 schema, fallback simplification)
3. **Phase 7C** — Dockable video windows (replaces display-index routing, fixes Defect #1)
4. **Phase 7D** — Integration testing and polish

Phases are sequential. Each phase has a clear "done" definition and can be verified independently.

---

## Phase 7A: Mixer Fix

**Goal:** Volume sliders and mute checkboxes affect actual audio output during playback.

**Relates to:** Defect #2, FR-004, FR-005, NFR-009

### Tasks

| # | Task | Component | Estimated Effort |
|---|------|-----------|-----------------|
| 7A-1 | Add `_volumeProviders` dictionary to `AudioEngine`; populate during `Load()`; clear in `Stop()` | `AudioEngine` | Small |
| 7A-2 | Apply `Muted` state during `Load()` — compute effective volume as `muted ? 0f : level` | `AudioEngine.Load()` | Small |
| 7A-3 | Add `ApplyChannelSettings()` method to `AudioEngine` | `AudioEngine` | Small |
| 7A-4 | Call `ApplyChannelSettings()` from `PlaybackEngine.Go()` before `SyncCoordinator.Start()` | `PlaybackEngine` | Small |
| 7A-5 | Unit tests for `ApplyChannelSettings()`: level applied, mute sets 0, unmute restores, unknown channel ignored, empty dict safe | `AudioEngine` tests | Medium |
| 7A-6 | Manual test: adjust slider → GO → verify audible difference | Manual | Small |
| 7A-7 | Manual test: mute a stem → GO → verify silence on that channel | Manual | Small |
| 7A-8 | Manual test: unmute a video audio channel → GO → verify audio plays | Manual | Small |

### Done Criteria

- [x] Changing a volume slider before pressing GO produces an audible difference in that channel's level
- [x] Muting a channel before pressing GO silences that channel during playback
- [x] Unmuting a video audio channel before pressing GO makes its audio audible
- [x] All existing unit tests pass
- [x] New unit tests for `ApplyChannelSettings()` pass

### Dependencies

None — this phase is self-contained.

### Risks

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| `VolumeSampleProvider.Volume` not thread-safe for concurrent read/write | Low (NAudio docs confirm single-writer is safe) | Write only on UI thread before playback starts; audio callback reads during playback — no concurrent writes |

---

## Phase 7B: Model Changes

**Goal:** Update the data model and show file schema from v1 to v2. Remove routing-related fields. Add dockable window layout persistence. Simplify fallback image to one-per-show.

**Relates to:** FR-008, FR-009

### Tasks

| # | Task | Component | Estimated Effort |
|---|------|-----------|-----------------|
| 7B-1 | Create `VideoWindowLayout` model class (Left, Top, Width, Height, IsMaximised) | `Model/` | Small |
| 7B-2 | Modify `Show` model: remove `GlobalDisplayRouting` and `FallbackImages`; add `FallbackImagePath` (string?) and `VideoWindowLayouts` (Dictionary<int, VideoWindowLayout>) ; bump version to 2 | `Model/Show.cs` | Small |
| 7B-3 | Modify `SongEntry` model: remove `DisplayRoutingOverrides` | `Model/SongEntry.cs` | Small |
| 7B-4 | Modify `VideoFileManifest` record: replace `DisplayIndex` with `SlotIndex` | `Model/SongManifest.cs` | Small |
| 7B-5 | Implement v1→v2 migration in `ShowFileService` (see TECHNICAL_SPEC §6) | `Services/ShowFileService.cs` | Medium |
| 7B-6 | Update `ShowFileService.ValidateDocument()` for v2 schema; reject unknown versions with clear message | `Services/ShowFileService.cs` | Small |
| 7B-7 | Update `SongScanner.Scan()`: remove routing parameter; assign sequential slot indices by alphabetical filename order | `Services/SongScanner.cs` | Small |
| 7B-8 | Update `SongEntry.CreateFromScan()`: remove `DisplayRoutingOverrides` initialisation | `Model/SongEntry.cs` | Small |
| 7B-9 | Update all callers of `SongScanner.Scan()` to remove routing argument | `PlaybackEngine`, `MainViewModel` | Small |
| 7B-10 | Update unit tests: `ShowFileService` round-trip with v2 schema; v1→v2 migration; `SongScanner` slot index assignment; `SongEntry.CreateFromScan()` without routing | Tests | Medium |
| 7B-11 | Manual test: open an existing v1 `.show` file → verify it loads → save → verify v2 JSON format | Manual | Small |

### Done Criteria

- [x] `Show` model has `FallbackImagePath`, `VideoWindowLayouts`, no `GlobalDisplayRouting`, no `FallbackImages`
- [x] `SongEntry` has no `DisplayRoutingOverrides`
- [x] `VideoFileManifest` has `SlotIndex` instead of `DisplayIndex`
- [x] `SongScanner.Scan()` takes no routing parameter
- [x] v1 `.show` files load and migrate silently to v2
- [x] v2 `.show` files round-trip through save/load correctly
- [x] All unit tests pass

### Dependencies

- Phase 7A should be complete first (to avoid merge conflicts in `PlaybackEngine`)
- This phase will temporarily break video playback (model changes without corresponding engine changes) — that's expected and resolved in Phase 7C

### Risks

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| Existing `.show` files fail to load after migration | Medium | Thorough unit tests for v1→v2 migration path; keep a v1 test fixture in the test project |
| Breaking callers of removed fields | Low | Compiler will catch all references to removed properties |

---

## Phase 7C: Dockable Video Windows

**Goal:** Video files play in freely positionable, resizable windows. Window positions persist to the `.show` file. Multi-display video works (resolves Defect #1).

**Relates to:** FR-003, FR-007, FR-008, FR-009, FR-016, FR-019, Defect #1

### Tasks

| # | Task | Component | Estimated Effort |
|---|------|-----------|-----------------|
| 7C-1 | Update `VlcDisplayWindow.xaml`: change `WindowStyle` to `SingleBorderWindow`, `ResizeMode` to `CanResize` | `UI/VlcDisplayWindow.xaml` | Small |
| 7C-2 | Add `SlotIndex` property to `VlcDisplayWindow`; update window title to `"VideoJam — Video {SlotIndex + 1}"` | `UI/VlcDisplayWindow.xaml.cs` | Small |
| 7C-3 | Implement close-button interception (`OnClosing` → Hide; `ForceClose()` for cleanup) | `UI/VlcDisplayWindow.xaml.cs` | Small |
| 7C-4 | Implement `GetLayout()` and `ApplyLayout()` methods on `VlcDisplayWindow` | `UI/VlcDisplayWindow.xaml.cs` | Small |
| 7C-5 | Replace `_displayWindows` with `_videoWindows` in `PlaybackEngine` | `Engine/PlaybackEngine.cs` | Small |
| 7C-6 | Implement `EnsureVideoWindows()` in `PlaybackEngine`: create/show/hide windows based on manifest video file count; apply saved layouts or default cascade positioning | `Engine/PlaybackEngine.cs` | Medium |
| 7C-7 | Add fallback image loading to `PlaybackEngine` or `MainViewModel`: load `Show.FallbackImagePath` as `BitmapImage` on show open; pass to `ShowFallback()` calls | `PlaybackEngine` / `MainViewModel` | Small |
| 7C-8 | Update `PlaybackEngine.Cue()`: remove `BuildRouting()` call; call `EnsureVideoWindows()` with slot-based logic; pass `_videoWindows` to `VideoEngine.LoadAll()` | `Engine/PlaybackEngine.cs` | Medium |
| 7C-9 | Update `PlaybackEngine.Go()`: iterate `_videoWindows` for `Activate()` calls | `Engine/PlaybackEngine.cs` | Small |
| 7C-10 | Update `PlaybackEngine.Dispose()`: call `ForceClose()` on all video windows | `Engine/PlaybackEngine.cs` | Small |
| 7C-11 | Remove `PlaybackEngine.BuildRouting()` method | `Engine/PlaybackEngine.cs` | Small |
| 7C-12 | Update `VideoEngine.Load()`: change parameter name from `displayIndex` to `slotIndex`; update manifest lookup to use `SlotIndex` | `Engine/VideoEngine.cs` | Small |
| 7C-13 | Add window layout capture to save flow: `MainViewModel` calls `GetLayout()` on all video windows before `ShowFileService.Save()`, writes to `Show.VideoWindowLayouts` | `UI/ViewModels/MainViewModel.cs` | Medium |
| 7C-14 | Add fallback image assignment UI: button or menu item in MainWindow to select a PNG file; store path in `Show.FallbackImagePath` | `UI/MainWindow.xaml`, `MainViewModel` | Medium |
| 7C-15 | Remove or gut `DisplayManager`: remove `ResolveDisplayIndex()`, `GetRequiredDisplayIndices()`, `CreateWindowForDisplay()`; retain class as empty shell or delete entirely | `Engine/DisplayManager.cs` | Small |
| 7C-16 | Remove display routing UI references (any routing-related ViewModels, XAML bindings, menu items) | Various UI files | Small |
| 7C-17 | Update unit tests for `PlaybackEngine` changes, `VideoEngine.Load()` parameter change, `DisplayManager` removal | Tests | Medium |
| 7C-18 | Manual test: two video files → two windows on two different displays | Manual | Small |
| 7C-19 | Manual test: drag/resize windows → save → close → reopen → verify positions restored | Manual | Small |
| 7C-20 | Manual test: maximise a window → save → reopen → verify maximised state restored | Manual | Small |
| 7C-21 | Manual test: click X on video window → window hides → next song cue → window reappears | Manual | Small |
| 7C-22 | Manual test: single display mode → video windows appear on primary display | Manual | Small |
| 7C-23 | Manual test: fallback PNG visible in all video windows between songs | Manual | Small |

### Done Criteria

- [x] Each video file plays in its own resizable, draggable window
- [x] Windows can be positioned on any connected display
- [x] Window positions and sizes persist to the `.show` file and restore on load
- [x] Maximised state persists and restores
- [x] Closing a video window hides it; it reappears on next cue
- [x] Fallback PNG shows in all video windows between songs and before the first song
- [x] Single-display mode works without errors
- [x] No remnants of display-index routing in the codebase
- [x] All unit tests pass
- [x] Defect #1 (multi-display regression) is resolved

### Dependencies

- Phase 7B must be complete (model changes are prerequisites)

### Risks

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| LibVLC rendering issues when window is resized during pre-buffer | Medium | Test resize during cue; if problematic, disable resize during Cued/Playing states |
| HWND changes when window is hidden/shown | Low | HWND is set in `OnLoaded` which fires on first show; subsequent Show/Hide should not change HWND. Verify with test. |
| Window position off-screen after display disconnect | Medium | Documented as known limitation (Assumption #5). No code mitigation required. |
| DPI scaling issues with saved window positions across machines | Medium | Use WPF device-independent pixels (DIPs) for all persisted coordinates; WPF handles DPI conversion |

---

## Phase 7D: Integration Testing and Polish

**Goal:** End-to-end verification of the complete workflow. Fix any issues discovered during testing.

**Relates to:** All requirements

### Tasks

| # | Task | Component | Estimated Effort |
|---|------|-----------|-----------------|
| 7D-1 | Full performance workflow test: create show → add songs → set levels → arrange windows → assign fallback PNG → save → close → reopen → perform full setlist with two-button control | Manual | Medium |
| 7D-2 | Cross-machine portability test: copy `.show` file and song folders to a different Windows machine → verify everything loads | Manual | Medium |
| 7D-3 | A/V sync verification: test with frame-counter video + audio click track → verify ≤10ms alignment | Manual | Small |
| 7D-4 | Stress test: 8-stem song + 2 video files → verify no glitches over full 5-minute playback | Manual | Small |
| 7D-5 | UI audit: verify no non-functional controls remain (NFR-009) — check for any remnants of routing UI, disabled-but-visible buttons, etc. | Manual | Small |
| 7D-6 | Fix any issues discovered in 7D-1 through 7D-5 | Various | Variable |
| 7D-7 | Update OpenSpec specs to reflect v2 changes (audio-engine, video-engine, display-manager, mixer-panel, show-file-service, song-scanner, song-model) | `openspec/specs/` | Medium |

### Done Criteria

- [x] Full performance workflow completes without errors
- [x] `.show` file portability verified across machines
- [x] A/V sync within 10ms specification
- [x] No non-functional UI controls visible
- [x] OpenSpec specs updated to reflect v2 architecture

### Dependencies

- All previous phases complete

---

## Dependency Map

```
Phase 7A (Mixer Fix)
    │
    ▼
Phase 7B (Model Changes)
    │
    ▼
Phase 7C (Dockable Windows)
    │
    ▼
Phase 7D (Integration & Polish)
```

All phases are sequential. Phase 7A can start immediately.

---

## Summary

| Phase | Scope | Key Deliverable |
|-------|-------|-----------------|
| 7A | Mixer fix | Volume/mute controls affect audio output |
| 7B | Model refactor | v2 show file schema, routing removed, slot-based video identity |
| 7C | Dockable windows | Freely positionable video windows with layout persistence |
| 7D | Testing & polish | Verified end-to-end workflow, spec updates |

**Total estimated tasks:** 50 (including manual verification)

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-02-19 | Technical Architect (AI-assisted) | Initial 6-phase plan |
| 2.0 | 2026-03-20 | Technical Architect (AI-assisted) | Phase 7A–7D plan for mixer fix, dockable windows, show file migration |
| 2.1 | 2026-03-23 | Technical Architect (AI-assisted) | All phases complete; all done criteria verified by user testing |
