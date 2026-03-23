# VideoJam — Technical Specification

**Version:** 2.1
**Date:** 2026-03-23
**Status:** Released
**Covers:** Defect fixes (mixer, multi-display) + dockable video window model

---

## Table of Contents

1. [System Overview](#1-system-overview)
2. [Defect #2: Mixer Controls Non-Functional](#2-defect-2-mixer-controls-non-functional)
3. [Defect #1: Multi-Display Video Regression](#3-defect-1-multi-display-video-regression)
4. [Dockable Video Window Model](#4-dockable-video-window-model)
5. [Data Models](#5-data-models)
6. [Show File Schema Migration](#6-show-file-schema-migration)
7. [Components Removed](#7-components-removed)
8. [Error Handling and Resilience](#8-error-handling-and-resilience)
9. [Security Considerations](#9-security-considerations)
10. [Performance Targets](#10-performance-targets)
11. [Testing Strategy](#11-testing-strategy)
12. [Dependencies](#12-dependencies)

---

## 1. System Overview

VideoJam is a WPF desktop application (.NET 10, Windows-only) that synchronises playback of multiple audio stems and video files for live musical performance. The operator loads a pre-built setlist, arranges video windows across connected displays, sets mix levels, and controls playback with two buttons.

### 1.1 Architecture Summary

```
┌─────────────────────────────────────────────────────────────┐
│  WPF UI Layer                                               │
│  ┌──────────────┐  ┌──────────────────────────────────────┐ │
│  │ MainWindow   │  │ VlcDisplayWindow (one per video file)│ │
│  │ (operator UI)│  │ - draggable, resizable, maximisable  │ │
│  │ - setlist    │  │ - fallback PNG between songs          │ │
│  │ - mixer      │  │ - LibVLC renders into HWND            │ │
│  │ - toolbar    │  └──────────────────────────────────────┘ │
│  └──────┬───────┘                                           │
│         │                                                   │
│  ┌──────▼───────────────────────────────────────────────┐   │
│  │ MainViewModel (MVVM controller)                      │   │
│  └──────┬───────────────────────────────────────────────┘   │
│         │                                                   │
├─────────▼───────────────────────────────────────────────────┤
│  Engine Layer                                               │
│  ┌──────────────┐  ┌──────────┐  ┌───────────────────────┐ │
│  │PlaybackEngine│  │AudioEngine│  │ VideoEngine           │ │
│  │(state machine│  │(NAudio    │  │ (LibVLC multi-player  │ │
│  │ + conductor) │  │ pipeline) │  │  + window management) │ │
│  └──────────────┘  └──────────┘  └───────────────────────┘ │
│         │                                                   │
│  ┌──────▼───────┐                                           │
│  │SyncCoordinator│  ← A/V dispatch timing                  │
│  └──────────────┘                                           │
│                                                             │
├─────────────────────────────────────────────────────────────┤
│  Model + Services Layer                                     │
│  Show, SongEntry, ChannelSettings, SongManifest             │
│  ShowFileService, SongScanner, PathResolver                 │
└─────────────────────────────────────────────────────────────┘
```

### 1.2 Current State

The application is fully functional through Phase 7D. All defects from Phase 6 user testing have been resolved and the dockable video window model is implemented and verified.

| Item | Summary | Status |
|------|---------|--------|
| Defect #2 | Mixer controls non-functional — sliders/mute don't affect audio output | ✅ Resolved (Phase 7A) |
| Defect #1 | Multi-display video regression — only primary display renders video | ✅ Resolved (Phase 7C) |
| Feature | Replace fullscreen-per-display model with dockable video windows | ✅ Delivered (Phase 7C) |

### 1.3 Scope of This Specification

This document specifies only the **changes** required to bring the codebase from its current state to Requirements v2.0. Unchanged components (HotkeyService, SyncCoordinator, PathResolver, setlist panel, etc.) are not re-documented here. The v1.0 TECHNICAL_SPEC remains the reference for those areas.

---

## 2. Defect #2: Mixer Controls Non-Functional

### 2.1 Root Cause

The mixer UI is fully built and bound to the `ChannelSettings` model. Changes save correctly to the `.show` file. However, the audio pipeline never applies those saved values at playback time.

**Trace:**

1. **UI → Model (works):** `MainWindow.xaml` binds sliders to `ChannelSettingsViewModel.Level` and checkboxes to `ChannelSettingsViewModel.Muted`. Two-way binding updates the underlying `ChannelSettings` model and calls `MarkDirty()`.

2. **Model → Audio Pipeline (broken):** `AudioEngine.Load()` (lines 119–123) reads `settings.Level` once at construction time and creates a `VolumeSampleProvider` with that value. After `Load()` returns, the `VolumeSampleProvider` instances are sealed inside the pipeline with no mechanism to update them.

3. **Muted is never read:** `AudioEngine.Load()` contains no code that checks `settings.Muted`. The property exists in the model and UI but has zero effect on audio output.

4. **Timing gap:** `AudioEngine.Load()` is called from `PlaybackEngine.Cue()`, which fires when the song is *selected in the setlist*. The operator may adjust levels *after* selection but *before* pressing GO. Those adjustments are never applied.

### 2.2 Required Fix

#### Part A: Retain references to VolumeSampleProviders

`AudioEngine` must keep a dictionary mapping channel ID to its `VolumeSampleProvider` so levels can be re-applied later:

```csharp
// New field in AudioEngine
private readonly Dictionary<string, VolumeSampleProvider> _volumeProviders = [];
```

Populated during `Load()` (replacing the current fire-and-forget creation):

```csharp
var volumeProvider = new VolumeSampleProvider(resampled) { Volume = effectiveVolume };
_volumeProviders[channel.ChannelId] = volumeProvider;
sampleProviders.Add(volumeProvider);
```

Cleared in `Stop()`:

```csharp
_volumeProviders.Clear();
```

#### Part B: Apply Muted state during Load

In `AudioEngine.Load()`, compute an effective volume that accounts for the muted state:

```csharp
var settings = channelSettings.TryGetValue(channel.ChannelId, out var s) ? s : null;
var effectiveVolume = (settings?.Muted ?? false) ? 0f : (settings?.Level ?? DEFAULT_CHANNEL_LEVEL);
var volumeProvider = new VolumeSampleProvider(resampled) { Volume = effectiveVolume };
```

#### Part C: Re-apply levels at GO time

Add a public method to `AudioEngine`:

```csharp
/// <summary>
/// Re-applies volume levels from the supplied channel settings to the active pipeline.
/// Called immediately before playback starts so that any level/mute changes made
/// after Cue() are picked up.
/// </summary>
public void ApplyChannelSettings(IReadOnlyDictionary<string, ChannelSettings> channelSettings) {
    foreach (var (channelId, provider) in _volumeProviders) {
        var settings = channelSettings.TryGetValue(channelId, out var s) ? s : null;
        provider.Volume = (settings?.Muted ?? false) ? 0f : (settings?.Level ?? DEFAULT_CHANNEL_LEVEL);
    }
}
```

Call this from `PlaybackEngine.Go()` immediately before `SyncCoordinator.Start()`:

```csharp
_audioEngine!.ApplyChannelSettings(_show.Songs[_cuedSongIndex].Channels);
_syncCoordinator.Start(_audioEngine!, _videoEngine);
```

#### Part D: No runtime updates during playback

FR-005 states "Levels are locked during playback." The mixer UI already disables controls during playback. Applying settings at GO time is sufficient — no runtime update mechanism is needed.

### 2.3 Impact Assessment

| Component | Change |
|-----------|--------|
| `AudioEngine` | Add `_volumeProviders` dictionary; populate during `Load()`; add `ApplyChannelSettings()` method; clear in `Stop()` |
| `PlaybackEngine.Go()` | Add one line: call `ApplyChannelSettings()` before sync start |
| All other components | No changes |

**Risk:** Low. `VolumeSampleProvider.Volume` is a simple float property. NAudio reads it on each callback cycle. Single-writer (UI thread) / single-reader (audio thread) access is safe.

---

## 3. Defect #1: Multi-Display Video Regression

### 3.1 Root Cause

When no routing configuration exists in the `.show` file (which is the default for newly created shows), all video files are routed to display index 0.

**Trace:**

1. `PlaybackEngine.Cue()` calls `BuildRouting(song)` → merges `Show.GlobalDisplayRouting` (empty `{}`) with `SongEntry.DisplayRoutingOverrides` (empty `{}`) → result: empty dictionary.

2. `SongScanner.Scan()` calls `DisplayManager.ResolveDisplayIndex(suffix, routing)` for each video file. With an empty routing dictionary, every suffix falls through to the default: `PRIMARY_DISPLAY_INDEX` (0).

3. Both video files get `DisplayIndex = 0` in their `VideoFileManifest`.

4. `EnsureDisplayWindows()` gets required display indices `{0}` — only one window is created.

5. `VideoEngine.LoadAll()` iterates over `_displayWindows` (key 0 only). `Load()` uses `FirstOrDefault()` to find a video file targeting display 0 — it finds the first one and ignores the second.

**Result:** Only one video file plays, only on the primary display. The second file is never loaded.

### 3.2 Why This Defect Is Superseded

This defect exists because of the suffix-based routing model. The v2.0 requirements replace this model entirely with **dockable video windows** — one window per video file, positioned freely by the operator. The routing system, `DisplayManager.ResolveDisplayIndex()`, `DisplayIndex` on `VideoFileManifest`, and the display-index-keyed window dictionary all become obsolete.

**The fix for Defect #1 is the dockable window implementation itself** (Section 4). No interim patch is planned.

### 3.3 Interim Workaround (If Needed)

If a working multi-display build is needed before the dockable window work is complete, a minimal fix would change `SongScanner` to auto-assign sequential display indices when routing is empty (video file 0 → display 0, video file 1 → display 1, etc.). This is explicitly a throwaway fix.

---

## 4. Dockable Video Window Model

This is the major architectural change in v2.0. It replaces the current "one borderless fullscreen window per display index" model with "one freely positionable window per video file."

### 4.1 Design Principles

1. **One window per video file** — if a song has two MP4 files, two windows appear
2. **Operator positions windows** — drag, resize, maximise on any connected display
3. **Position persistence** — window layout saved to `.show` file and restored on load
4. **Fallback PNG in all windows** — between songs, every video window shows the show's fallback image
5. **Windows reused across songs** — windows persist across song transitions; count adjusts to match current song's video files

### 4.2 Window Identity: Slot Index

**Current model:** Windows keyed by display index (integer). A video file's identity is its filename suffix mapped through a routing table to a display index.

**New model:** Windows keyed by **slot index** (0, 1, 2, ...) corresponding to the position of the video file in the song's video file list, sorted alphabetically by filename.

**Rationale:** Different songs have different video filenames. The operator arranges windows once (e.g. "window 0 on the audience screen, window 1 on the stage monitor") and that layout works across songs. Using a stable slot index (alphabetical sort order) rather than filename means the layout doesn't break when song folders use different naming conventions. Songs with the same number of video files share the same window slots.

### 4.3 VlcDisplayWindow Changes

The window changes from borderless/fullscreen to a standard resizable WPF window:

| Property | Current | New |
|----------|---------|-----|
| `WindowStyle` | `None` | `SingleBorderWindow` |
| `ResizeMode` | `NoResize` | `CanResize` |
| Title | `"VideoJam Display"` | `"VideoJam — Video {SlotIndex + 1}"` |
| Initial size | Covers entire display | 640×360 (16:9 default), or restored from saved layout |
| Drag | Not possible | Standard title bar drag |
| Maximise | Not possible | Standard maximise button |
| Close button | Not present | Present but **intercepted** — hides the window instead of closing |
| Background | Black | Black (no change) |
| Fallback PNG | `Image` element toggled by `ShowFallback()` | Same mechanism (no change) |
| HWND | Set in `OnLoaded` | Same mechanism (no change) |

#### Close Button Interception

The window must not be destroyable by the operator during a show. Clicking the close button hides the window instead:

```csharp
private bool _forceClose;

public void ForceClose() {
    _forceClose = true;
    Close();
}

protected override void OnClosing(CancelEventArgs e) {
    if (!_forceClose) {
        e.Cancel = true;
        Hide();
        return;
    }
    base.OnClosing(e);
}
```

`PlaybackEngine.Dispose()` calls `ForceClose()` for cleanup.

#### Layout Capture

The window exposes a method to capture its current position/size for persistence:

```csharp
public VideoWindowLayout GetLayout() => new() {
    Left = RestoreBounds.Left,
    Top = RestoreBounds.Top,
    Width = RestoreBounds.Width,
    Height = RestoreBounds.Height,
    IsMaximised = WindowState == WindowState.Maximized,
};
```

Note: `RestoreBounds` is used instead of `Left`/`Top`/`Width`/`Height` so that the non-maximised position is captured even when the window is currently maximised. This means restoring the layout will place the window at its pre-maximise position and then maximise it, matching operator expectations.

#### Layout Restore

```csharp
public void ApplyLayout(VideoWindowLayout layout) {
    Left = layout.Left;
    Top = layout.Top;
    Width = layout.Width;
    Height = layout.Height;
    if (layout.IsMaximised)
        WindowState = WindowState.Maximized;
}
```

### 4.4 PlaybackEngine Changes

#### Window Dictionary

```csharp
// Current
private readonly Dictionary<int, VlcDisplayWindow> _displayWindows = [];

// New
private readonly Dictionary<int, VlcDisplayWindow> _videoWindows = [];
```

The key changes from "display index" to "slot index" but the type remains `int`.

#### EnsureVideoWindows (replaces EnsureDisplayWindows)

```
EnsureVideoWindows(manifest, show):
    maxSlots = manifest.VideoFiles.Count

    // Create any missing windows
    for slotIndex in 0..<maxSlots:
        if slotIndex not in _videoWindows:
            window = new VlcDisplayWindow()
            window.SlotIndex = slotIndex  // for title bar display
            if show.VideoWindowLayouts has entry for slotIndex:
                window.ApplyLayout(show.VideoWindowLayouts[slotIndex])
            else:
                // Default: staggered cascade from (100, 100), offset 30px each
                window.Left = 100 + slotIndex * 30
                window.Top = 100 + slotIndex * 30
                window.Width = 640
                window.Height = 360
            if show.FallbackImagePath is not null:
                window.ShowFallback(loadedFallbackImage)
            window.Show()
            _videoWindows[slotIndex] = window
        else if window is hidden (operator closed it):
            window.Show()

    // Hide excess windows (from a previous song with more video files)
    for (slotIndex, window) in _videoWindows where slotIndex >= maxSlots:
        window.ShowFallback(loadedFallbackImage)
        // Keep alive — may be needed for a later song
```

#### Window Position Persistence

When the operator saves the show, `MainViewModel` captures window positions before calling `ShowFileService.Save()`:

```
CaptureWindowLayouts():
    show.VideoWindowLayouts.Clear()
    for (slotIndex, window) in playbackEngine.VideoWindows:
        show.VideoWindowLayouts[slotIndex] = window.GetLayout()
```

#### BuildRouting Removed

`PlaybackEngine.BuildRouting()` is deleted. `SongScanner.Scan()` no longer takes a routing dictionary.

### 4.5 VideoEngine Changes

`Load()` and `LoadAll()` parameter changes:

```csharp
// Current
public async Task Load(SongManifest manifest, int displayIndex, VlcDisplayWindow window, ...)

// New
public async Task Load(SongManifest manifest, int slotIndex, VlcDisplayWindow window, ...)
```

The internal lookup changes:

```csharp
// Current
var videoFile = manifest.VideoFiles.FirstOrDefault(v => v.DisplayIndex == displayIndex);

// New
var videoFile = manifest.VideoFiles.FirstOrDefault(v => v.SlotIndex == slotIndex);
```

All other logic (pre-buffer sequence, ActiveSlot registration, Play/Stop) is unchanged.

### 4.6 SongScanner Changes

`Scan()` no longer takes a routing dictionary. Video files are sorted alphabetically and assigned sequential slot indices:

```csharp
// Current
public static SongManifest Scan(DirectoryInfo folder, IReadOnlyDictionary<string, int> displayRouting)

// New
public static SongManifest Scan(DirectoryInfo folder)
```

Video file assignment:

```csharp
var videoFiles = folder.GetFiles("*.mp4")
    .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
    .Select((file, index) => new VideoFileManifest(file, SlotIndex: index, Suffix: ExtractSuffix(file)))
    .ToList();
```

### 4.7 Fallback Image Simplification

**Current:** `Show.FallbackImages` is `Dictionary<int, string>` (per-display). The UI is incomplete.

**New:** `Show.FallbackImagePath` is `string?` — one fallback image for the entire show.

The fallback image is loaded once when the show is opened. `PlaybackEngine` holds the loaded `BitmapImage` and passes it to all `VlcDisplayWindow.ShowFallback()` calls.

### 4.8 Single-Display Mode

When only one display is connected, the app operates normally. Video windows appear on the primary display alongside the MainWindow. The operator can maximise a video window to fill the primary display if desired, and use Alt-Tab or Ctrl+Tab to return to the operator UI. This satisfies FR-016 without any special-case code.

---

## 5. Data Models

### 5.1 Show Model (v2 Schema)

```csharp
public sealed class Show {
    public int Version { get; set; } = 2;
    public List<SongEntry> Songs { get; set; } = [];

    // NEW: Single fallback image (replaces per-display dictionary)
    public string? FallbackImagePath { get; set; }

    // NEW: Video window positions, keyed by slot index
    public Dictionary<int, VideoWindowLayout> VideoWindowLayouts { get; set; } = [];

    // REMOVED: GlobalDisplayRouting
    // REMOVED: FallbackImages (dictionary)
}
```

### 5.2 SongEntry (v2)

```csharp
public sealed class SongEntry {
    public string FolderPath { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, ChannelSettings> Channels { get; set; } = [];

    // REMOVED: DisplayRoutingOverrides
}
```

### 5.3 VideoWindowLayout (New)

```csharp
public sealed class VideoWindowLayout {
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; } = 640;
    public double Height { get; set; } = 360;
    public bool IsMaximised { get; set; }
}
```

### 5.4 VideoFileManifest (Modified)

```csharp
// DisplayIndex replaced with SlotIndex
public sealed record VideoFileManifest(
    FileInfo File,
    int SlotIndex,    // 0-based, assigned by alphabetical sort of filenames
    string Suffix);   // Retained for display labelling only
```

### 5.5 Show File Example (v2)

```json
{
  "version": 2,
  "fallbackImagePath": "images/band-poster.png",
  "videoWindowLayouts": {
    "0": { "left": 1920, "top": 0, "width": 1920, "height": 1080, "isMaximised": true },
    "1": { "left": 0, "top": 100, "width": 640, "height": 360, "isMaximised": false }
  },
  "songs": [
    {
      "folderPath": "songs/Opening Number",
      "name": "Opening Number",
      "channels": {
        "drums.wav": { "level": 0.8, "muted": false },
        "bass.wav": { "level": 0.7, "muted": false },
        "visuals.mp4:audio": { "level": 1.0, "muted": true }
      }
    }
  ]
}
```

### 5.6 Entity Relationships

```
Show (1) ──── (*) SongEntry
  │                  │
  │                  └── (*) ChannelSettings (keyed by channel ID)
  │
  ├── (0..1) FallbackImagePath
  │
  └── (*) VideoWindowLayout (keyed by slot index)
```

---

## 6. Show File Schema Migration

The `.show` file schema version bumps from 1 to 2. `ShowFileService` must handle both versions.

### 6.1 Migration Logic

```
LoadShow(path):
    json = read and parse file
    version = json.version

    if version == 1:
        // Migrate v1 → v2:
        // 1. If FallbackImages dict has any entries, take the first value as FallbackImagePath
        // 2. Remove GlobalDisplayRouting
        // 3. Remove FallbackImages dict
        // 4. Remove DisplayRoutingOverrides from each SongEntry
        // 5. Add empty VideoWindowLayouts dict
        // 6. Set version = 2

    if version == 2:
        deserialise normally

    return show
```

### 6.2 Compatibility

- **v1 → v2:** Automatic silent migration on load. Next save writes v2 format.
- **v2 → v1:** Not supported. A v2 file opened in an older build will fail validation. This is acceptable — the operator should update the app.
- **Unknown version:** Rejected with a clear error message ("This show file was created with a newer version of VideoJam").

---

## 7. Components Removed

| Component / Field | Reason |
|-------------------|--------|
| `DisplayManager.ResolveDisplayIndex()` | Routing model removed |
| `DisplayManager.GetRequiredDisplayIndices()` | Replaced by slot count from manifest |
| `DisplayManager.CreateWindowForDisplay()` | Windows no longer pinned to displays |
| `Show.GlobalDisplayRouting` | Routing model removed |
| `Show.FallbackImages` (dictionary) | Replaced by single `FallbackImagePath` |
| `SongEntry.DisplayRoutingOverrides` | Routing model removed |
| `VideoFileManifest.DisplayIndex` | Replaced by `SlotIndex` |
| `PlaybackEngine.BuildRouting()` | Routing model removed |
| `SongScanner.Scan()` routing parameter | No routing to apply |
| Display routing UI (spec + viewmodels) | Routing model removed |

`DisplayManager` as a class may be retained if display enumeration is needed elsewhere, or removed entirely if no other code references it.

---

## 8. Error Handling and Resilience

### 8.1 Audio Pipeline Errors

- **File load failure:** Individual channel skipped with warning log; remaining channels continue. (No change from current behaviour.)
- **WASAPI device unavailable:** `AudioEngine.Load()` throws; `PlaybackEngine.Cue()` catches and transitions to Idle.

### 8.2 Video Pipeline Errors

- **Pre-buffer timeout:** Individual slot skipped; window remains on fallback image. Other slots proceed. (No change.)
- **LibVLC codec failure:** Same as timeout — graceful per-slot fallback. (No change.)

### 8.3 Show File Errors

- **v1 file loaded:** Silently migrated to v2. Next save writes v2.
- **Unknown version:** Error dialog: "This show file requires a newer version of VideoJam."
- **Corrupt JSON:** `ShowFileService.ValidateDocument()` rejects; error dialog shown.
- **Missing song folder:** Logged as warning. Song appears in setlist but cannot be cued.

### 8.4 Window Management

- **Operator closes a video window:** Window is hidden, not destroyed. Reappears on next song cue.
- **Display disconnected mid-show:** Windows on the lost display are off-screen. No automatic recovery. Operator must reposition. (Matches Assumption #5 in REQUIREMENTS.md.)

---

## 9. Security Considerations

No changes from v1.0. VideoJam is a standalone offline desktop application with no network connectivity, no user accounts, and no personal data. The low-level keyboard hook (`WH_KEYBOARD_LL`) is the only elevated API used and is standard Windows practice for global hotkeys.

---

## 10. Performance Targets

| Metric | Target | How Achieved |
|--------|--------|--------------|
| A/V sync | ≤10ms start alignment, maintained throughout | `SyncCoordinator` dispatches audio then video in <1ms; NAudio mixes all stems in a single WASAPI callback cycle |
| Audio glitches | Zero during playback | WASAPI shared mode with 50ms buffer; all stems pre-loaded before GO |
| Video frame drops | Zero during playback | LibVLC hardware-accelerated decode; pre-buffer ensures decoder is warm |
| Control latency | Imperceptible (<50ms) | Low-level keyboard hook fires synchronously; playback start is non-blocking |
| App startup | <30s to operational | Single-process WPF app; no heavy initialisation beyond LibVLC library load |
| Cue latency | <3s per song | Audio load is synchronous I/O (~100ms); video pre-buffer has 2s timeout per file |

---

## 11. Testing Strategy

### 11.1 Unit Tests (xUnit)

All new and modified engine/model code requires unit tests:

| Area | Tests |
|------|-------|
| `AudioEngine.ApplyChannelSettings()` | Level applied correctly; muted sets volume to 0; unmuted restores level; unknown channel ID ignored; empty dict is safe |
| `SongScanner` (no routing) | Video files assigned sequential slot indices in alphabetical order; routing parameter removed from signature |
| `VideoWindowLayout` | Serialisation round-trip; default values (640×360, not maximised) |
| `ShowFileService` v1→v2 migration | v1 file loads; routing fields removed; fallback image migrated; version bumped to 2; v2 round-trip |
| `Show` model v2 | `FallbackImagePath` persists; `VideoWindowLayouts` serialise/deserialise with int keys |
| `SongEntry` v2 | `DisplayRoutingOverrides` removed; existing tests updated |
| `VlcDisplayWindow.GetLayout()` | Returns correct position/size; captures `RestoreBounds` when maximised |

### 11.2 Integration Tests (Manual)

| Scenario | Verification |
|----------|-------------|
| **Mixer levels** | Change slider from 1.0 to 0.5 → GO → verify channel is audibly quieter |
| **Mixer mute** | Mute a stem → GO → verify channel is silent |
| **Video unmute** | Unmute a video audio channel → GO → verify video audio plays alongside stems |
| **Multi-display video** | Two video files → two windows on two displays → both render correctly |
| **Window persistence** | Position windows → save → close → reopen → windows restore to saved positions |
| **Window maximise** | Maximise a video window → save → reopen → window restores maximised |
| **Fallback PNG** | Assign fallback → all video windows show it between songs |
| **v1 migration** | Open a v1 `.show` file → loads without error → save → verify v2 format |
| **Close button** | Click X on video window → window hides → cue next song → window reappears |
| **Single display** | Disconnect external displays → app operates on primary only |

### 11.3 Performance Verification

| Scenario | Target |
|----------|--------|
| 4-stem + 2-video song cue time | <3 seconds |
| GO to audible audio | <50ms perceived |
| A/V sync drift over 5-minute song | <10ms (visual frame-counter test) |

---

## 12. Dependencies

No new dependencies are introduced. All changes use existing libraries:

| Dependency | Purpose in This Change |
|------------|----------------------|
| NAudio `VolumeSampleProvider` | Mixer fix — runtime volume updates via `.Volume` property |
| WPF `Window` | Dockable windows — standard `SingleBorderWindow` style, `RestoreBounds` for layout capture |
| System.Text.Json | Show file v1→v2 migration |
| LibVLCSharp `MediaPlayer` | No changes — slot-index keying is transparent to LibVLC |

---

## 13. Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-02-19 | Technical Architect (AI-assisted) | Initial draft |
| 2.0 | 2026-03-20 | Technical Architect (AI-assisted) | Mixer fix (Defect #2); multi-display regression analysis (Defect #1, superseded by dockable model); dockable video window model replacing display-index routing; show file v1→v2 migration; fallback image simplification |
