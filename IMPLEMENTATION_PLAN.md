# VideoJam — Implementation Plan

**Version:** 1.0
**Date:** 2026-02-19
**Status:** Draft
**Prepared by:** Technical Architect (AI-assisted)

---

## Guiding Principles

1. **Build bottom-up.** The riskiest parts of this system are the audio pipeline and A/V synchronisation. These are built and validated first, before any UI exists. We do not build UI on top of an unvalidated engine.
2. **Each phase produces a testable artefact.** Every phase ends with something that can be run and verified — even if it is a console harness or a rough WPF prototype.
3. **Defer polish.** Error handling, logging, and UI refinement are addressed in later phases. Early phases use assertions and exceptions to surface problems fast.
4. **Maintain a known-good test set.** From Phase 2 onward, a folder of test media files (audio stems + MP4 videos) is the primary integration test tool. Keep it checked in or accessible throughout development.

---

## Phase Overview

| Phase | Name | Goal | Key Risk |
|-------|------|------|----------|
| [1](#phase-1-foundation--audio-engine) | Foundation & Audio Engine | Multiple stems play in sync to WASAPI output | WASAPI pipeline stability |
| [2](#phase-2-video-engine--av-synchronisation) | Video Engine & A/V Sync | Single video + audio stems start in sync | LibVLC HWND integration with WPF |
| [3](#phase-3-multi-display--display-management) | Multi-Display | Correct video on correct display; fallback PNG | DPI scaling; monitor enumeration edge cases |
| [4](#phase-4-show-model--persistence) | Show Model & Persistence | `.show` files save, load, and resolve paths | Path relativisation across machines |
| [5](#phase-5-operator-ui) | Operator UI | Full WPF interface: setlist, mixer, show management | Drag-and-drop; UI state binding |
| [6](#phase-6-playback-control--integration) | Playback Control & Integration | Full end-to-end performance workflow | State machine correctness; global hotkey hook |
| [7](#phase-7-hardening) | Hardening | Graceful error handling; logging; edge cases | Display disconnect audio isolation |
| [8](#phase-8-release-packaging) | Release Packaging | Self-contained portable zip; clean-machine test | Native VLC binary packaging |

---

## Phase 1: Foundation & Audio Engine

### Goal
Multiple audio stems from a folder (WAV, MP3, AIFF) play simultaneously through a single WASAPI output, mixed to stereo. All stems start at exactly the same time and play to completion. Per-stem volume can be set before playback.

This phase produces a **console application** or minimal WPF harness — no production UI required.

### Success Criteria
- [ ] Three or more WAV files from a test folder play simultaneously with no audible phasing or timing offset
- [ ] A stem with `volume = 0.5` plays at half the level of a stem with `volume = 1.0`
- [ ] Playback stops cleanly when all stems reach their end
- [ ] No WASAPI errors or audio glitches on a standard Windows 11 machine
- [ ] Unit tests pass for file classification logic

### Tasks

#### 1.1 Solution Setup
- Create a Visual Studio solution: `VideoJam.sln`
- Add project: `VideoJam` (WPF Application, .NET 10, win-x64)
- Add project: `VideoJam.Tests` (xUnit Test Project, .NET 10)
- Add NuGet packages to `VideoJam`:
  - `NAudio` (2.2.x)
  - `LibVLCSharp` (3.x) — added now, used in Phase 2
  - `VideoLAN.LibVLC.Windows` (3.x) — added now, used in Phase 2
  - `Microsoft.Extensions.Logging` (8.x)
- Add NuGet packages to `VideoJam.Tests`:
  - `xunit` (2.x)
  - `xunit.runner.visualstudio`
  - `Microsoft.NET.Test.Sdk`
- Pin all package versions in `Directory.Build.props`
- Add `Directory.Build.props` to solution root with shared version pinning
- Configure `VideoJam` for `win-x64` self-contained publish in project file

#### 1.2 Project Structure
- Create the folder structure defined in the Technical Spec (Section 3.1)
- Add empty stub files for all planned classes (compile-only, no implementation yet)
- Confirm the solution builds with zero errors and zero warnings

#### 1.3 `SongScanner` Implementation
- Implement `SongScanner.Scan(string folderPath)` returning a `SongManifest`
- File classification rules:
  - `.wav`, `.mp3`, `.aiff` (case-insensitive) → `AudioChannelType.Stem`
  - `.mp4` → `AudioChannelType.VideoAudio` (audio channel entry) + `VideoFileManifest` entry
  - All other files → ignored
- Extract suffix from video filenames (e.g. `_lyrics`, `_visuals`) for later routing use
- Channel ID: filename only (no path) for stems; `{filename}:audio` for video audio

#### 1.4 `SongScanner` Unit Tests
- Empty folder → empty manifest
- Folder with WAV, MP3, AIFF files → correct stem channels
- Folder with MP4 files → correct video entries + video audio channels
- Folder with `.txt`, `.png`, `.pdf` files → all ignored
- Filenames with no suffix → `VideoFileManifest` with empty suffix
- Case-insensitive extension matching (`.WAV`, `.Mp3`, etc.)

#### 1.5 `AudioEngine` — Core Pipeline
- Implement `AudioEngine.Load(SongManifest manifest, Dictionary<string, ChannelSettings> channelSettings)`
  - Create an `AudioFileReader` (WAV, MP3) or `AiffFileReader` (AIFF) or `MediaFoundationReader` (MP4 audio) per audio channel
  - Wrap each in a `VolumeSampleProvider` with level from `ChannelSettings` (or 1.0 default)
  - Compose into a `MixingSampleProvider`
  - Attach to a `WasapiOut` (shared mode, 50ms buffer) — do not start yet
- Implement `AudioEngine.Play()` — calls `WasapiOut.Play()`; returns `Stopwatch.GetTimestamp()`
- Implement `AudioEngine.Stop()` — calls `WasapiOut.Stop()`; disposes all readers and the WasapiOut; resets state
- Implement `AudioEngine.PlaybackEnded` event — raised when `WasapiOut.PlaybackStopped` fires after natural end (not after explicit Stop)
- Implement `AudioEngine.Dispose()` — ensures clean teardown

#### 1.6 Phase 1 Integration Harness
- Create a minimal WPF window (or console app) with:
  - A "Load" button that opens a folder picker and calls `SongScanner.Scan()` + `AudioEngine.Load()`
  - A "Play" button that calls `AudioEngine.Play()`
  - A "Stop" button that calls `AudioEngine.Stop()`
  - A label showing playback state
- Use this to manually verify multi-stem sync with the test set

#### 1.7 `ChannelSettings` Unit Tests
- Default construction: `level = 1.0`, `muted = false`
- Stem channel created from new song: inherits defaults
- Video audio channel created from new song: `muted = true`

### Dependencies
None — this is the foundation phase.

### Notes
- If `MediaFoundationReader` cannot read a specific MP4 audio format, investigate `FFmpegMediaFoundationReader` or `NAudio.Vorbis` as fallback. This is the most likely technical surprise in Phase 1.
- Do not implement mute logic in this phase — that is handled by setting `VolumeSampleProvider.Volume = 0` in Phase 5 based on channel settings.

---

## Phase 2: Video Engine & A/V Synchronisation

### Goal
A single video file plays on the primary display in a borderless full-screen window, starting in sync with the audio stems. The display shows a static PNG between songs.

### Success Criteria
- [ ] A borderless WPF window covers the primary display with no visible borders or taskbar
- [ ] A video file plays in the window with hardware-accelerated H.264 decode
- [ ] Audio starts, then VLC is signalled within 1ms; first video frame appears within one frame period of audio start
- [ ] VLC audio output is confirmed silent (all audio routed through NAudio)
- [ ] When no video is assigned, the window shows a fallback PNG
- [ ] Yanking the audio cable (simulated by disconnecting default audio device) does not crash the app

### Tasks

#### 2.1 `VlcDisplayWindow` — Basic Implementation
- Create `VlcDisplayWindow.xaml` — a borderless, topmost WPF `Window`
  - `WindowStyle="None"`, `ResizeMode="NoResize"`, `Topmost="True"`, `Background="Black"`
  - Contains an `<Image>` element (for fallback PNG) behind a host panel for the VLC surface
- Implement `VlcDisplayWindow.xaml.cs`:
  - On `Loaded`: capture the HWND via `new WindowInteropHelper(this).Handle`
  - Expose `IntPtr Hwnd { get; }` property
  - Expose `ShowFallback(BitmapImage image)` method — shows the Image element, hides VLC surface
  - Expose `ShowVideo()` method — hides Image element, shows VLC surface

#### 2.2 `VideoEngine` — Single Display
- Implement `VideoEngine` with constructor taking a list of `(VlcDisplayWindow, int displayIndex)` tuples
- `VideoEngine.Load(SongManifest manifest, DisplayRoute routing)`:
  - For each `VideoFileManifest` in the manifest, find the matching `VlcDisplayWindow` by display index
  - Open the video file in the corresponding `MediaPlayer`, passing the window's HWND
  - Configure MediaPlayer: `--no-audio`, `--no-osd`
  - Call `MediaPlayer.Play()` followed immediately by `MediaPlayer.Pause()` to pre-buffer — then seek back to 0
  - Mark the display as "has video" for this song
- `VideoEngine.Play(long audioStartTimestamp)`:
  - Call `MediaPlayer.Play()` on all active players sequentially (no delay between calls)
  - Record the elapsed time since `audioStartTimestamp` for diagnostic logging
- `VideoEngine.Stop()`: stop and reset all active MediaPlayers; tell all `VlcDisplayWindow` instances to show fallback PNG
- `VideoEngine.Dispose()`: dispose all LibVLC and MediaPlayer instances

#### 2.3 `SyncCoordinator` — Implementation
- `SyncCoordinator.Start(AudioEngine audio, VideoEngine video)`:
  1. Call `audio.Play()` → capture `t_start`
  2. Call `video.Play(t_start)`
  3. Log `Δt` between audio start and video signal dispatch
- This class contains no state after the start sequence completes

#### 2.4 Phase 2 Integration Harness
- Extend the Phase 1 harness to:
  - Allow selection of a video file in addition to an audio folder
  - Create one `VlcDisplayWindow` for the primary display
  - Call `VideoEngine.Load()` + `AudioEngine.Load()` on "Load"
  - Call `SyncCoordinator.Start()` on "Play"
- Verify with the known-good test set that audio and video start together

### Dependencies
- Phase 1 complete and passing

### Notes
- The pre-buffer trick (`Play()` → `Pause()` → seek to 0) is critical to eliminating the VLC startup delay from the sync path. Test this carefully — VLC's internal state after a pause-seek is not always deterministic. If it proves unreliable, fall back to opening the file and allowing a short warm-up delay before arming the GO button.
- WPF DPI virtualisation can cause the `VlcDisplayWindow` to position incorrectly on high-DPI displays. Add DPI-awareness manifest settings to the project file: `<ApplicationManifest>app.manifest</ApplicationManifest>` with `<dpiAware>True/PM</dpiAware>`.

---

## Phase 3: Multi-Display & Display Management

### Goal
Correct video files appear on correct displays based on filename suffix. Up to 4 displays are supported. Displays without an assigned video show the fallback PNG.

### Success Criteria
- [ ] All connected displays are detected and enumerated at startup
- [ ] A video with suffix `_visuals` appears on Display 2, not Display 1
- [ ] A display with no assigned video shows the fallback PNG throughout
- [ ] Single-display mode (one monitor connected) works correctly
- [ ] `VlcDisplayWindow` positions correctly on all displays, including mixed-DPI setups

### Tasks

#### 3.1 `DisplayManager` — Implementation
- Implement `DisplayManager.Initialise()`:
  - Enumerate `System.Windows.Forms.Screen.AllScreens`
  - Sort: primary screen first (index 0), then remaining in enumeration order
  - For each screen, retrieve per-monitor DPI scale factor
  - Create one `VlcDisplayWindow` per screen
  - Position each window using `screen.Bounds` divided by DPI scale factor
  - Show each window immediately (displaying black / fallback PNG)
- Expose `IReadOnlyList<(VlcDisplayWindow Window, Screen Screen, int Index)> Displays`
- Expose `int DisplayCount`

#### 3.2 Display Routing Logic
- Implement `DisplayRoute.Resolve(string videoFileSuffix, Dictionary<string, int> globalRouting, Dictionary<string, int> songOverrides)` → `int? displayIndex`
  - Checks song overrides first, then global routing
  - Returns `null` if no match (file is ignored)
- Unit tests:
  - Suffix in global routing → correct index returned
  - Suffix in song overrides → override takes precedence
  - Unknown suffix → null returned
  - Empty overrides dict → global routing used

#### 3.3 Fallback PNG Loading
- `DisplayManager.SetFallbackImage(int displayIndex, string absoluteImagePath)`:
  - Loads the PNG as a `BitmapImage`
  - Calls `VlcDisplayWindow.ShowFallback(image)` for the specified display
- Called during show load for each configured fallback image
- Missing fallback image → display shows solid black (not an error)

#### 3.4 `VideoEngine` — Multi-Display Extension
- Update `VideoEngine.Load()` to iterate all video files in the manifest and route each to its resolved display index
- Displays not assigned a video for the current song: remain in fallback PNG state

#### 3.5 Phase 3 Manual Verification
- Test with a song folder containing `_lyrics.mp4` (Display 0) and `_visuals.mp4` (Display 1)
- Verify correct routing
- Test with only one video file — the unassigned display should show its fallback PNG
- Test single-display mode (disconnect external monitor before launch)

### Dependencies
- Phase 2 complete and passing

---

## Phase 4: Show Model & Persistence

### Goal
`.show` files can be created, saved, and loaded. Song folder references survive being copied to a different machine (relative paths resolve correctly).

### Success Criteria
- [ ] A `Show` object serialises to valid JSON matching the spec format
- [ ] A saved `.show` file can be loaded back with all data intact
- [ ] A `.show` file copied to a new location resolves song paths correctly if the folder structure is preserved
- [ ] A `.show` file with missing required fields produces a descriptive error, not a crash
- [ ] Unit tests pass for all serialisation and path resolution scenarios

### Tasks

#### 4.1 Model Classes — Full Implementation
- Implement all model classes from Technical Spec Section 4.2:
  - `Show`, `SongEntry`, `ChannelSettings`
- Add `[JsonPropertyName]` attributes where property names differ from JSON convention
- Add a `version` field to `Show`; current value is `1`

#### 4.2 `ShowFileService` — Implementation
- `ShowFileService.Save(Show show, string filePath)`:
  - Serialises to UTF-8 JSON using `System.Text.Json`
  - Writes to file atomically (write to temp file, then rename)
- `ShowFileService.Load(string filePath)` → `Show`:
  - Reads and deserialises the JSON
  - Validates: `version` must be present and == 1; `songs` must be a list; `globalDisplayRouting` must be present
  - Throws `ShowFileException` (typed, not raw `Exception`) on validation failure
- Unit tests:
  - Round-trip: serialise then deserialise → identical object
  - Missing `version` field → `ShowFileException`
  - Missing `songs` field → `ShowFileException`
  - Valid minimal show (empty song list) → loads successfully
  - UTF-8 with BOM → loads successfully

#### 4.3 `PathResolver` — Implementation
- `PathResolver.MakeRelative(string absoluteTargetPath, string showFileDirectory)` → `string`
  - Computes the relative path from the `.show` file directory to the target
  - Uses `Path.GetRelativePath()`
- `PathResolver.Resolve(string relativePath, string showFileDirectory)` → `string`
  - Combines the show directory and relative path to produce an absolute path
  - Normalises separators and resolves `..` segments
- Unit tests:
  - Target in same directory as show file → single-component relative path
  - Target in subdirectory → forward-only relative path
  - Target in sibling directory → `../sibling/path` form
  - Round-trip: `Resolve(MakeRelative(abs, dir), dir) == abs`
  - Windows path separator normalisation

#### 4.4 Show + Path Integration
- Implement `ShowFileService.Save()` path handling:
  - Before serialising, convert all `SongEntry.FolderPath` values to relative paths using `PathResolver.MakeRelative()`
  - Convert all `FallbackImages` paths to relative
- Implement `ShowFileService.Load()` path handling:
  - After deserialising, store raw relative paths in the model (resolution happens at point of use via `PathResolver.Resolve()`)

#### 4.5 `SongEntry` Creation from Scan
- Implement `SongEntry.CreateFromScan(SongManifest manifest, string showFileDirectory)`:
  - `FolderPath`: relative path from show directory to song folder
  - `Name`: folder name
  - `Channels`: one `ChannelSettings` per audio channel in manifest (stem defaults: level=1.0, muted=false; video audio defaults: level=1.0, muted=true)
  - `DisplayRoutingOverrides`: empty dictionary

### Dependencies
- Phase 1 complete (for `SongScanner` and model classes)

### Notes
- Phase 4 can be developed in parallel with Phase 3 if two developers are available.

---

## Phase 5: Operator UI

### Goal
The full WPF operator interface is functional: setlist management, per-channel level/mute control, show file operations. Media playback is not yet wired to the UI — that happens in Phase 6.

### Success Criteria
- [ ] A new show can be created and songs added by selecting folders
- [ ] Songs can be reordered by drag-and-drop
- [ ] Per-channel level sliders and mute toggles are visible and update the model
- [ ] Show can be saved to a `.show` file and re-opened with all data restored
- [ ] Dirty state (unsaved changes asterisk) is tracked correctly
- [ ] Mixer panel controls are disabled when `PlaybackState` is `Playing` or `Paused`

### Tasks

#### 5.1 `MainWindow` — Layout
- Implement the two-panel layout from Technical Spec Section 11.1
- Use WPF data binding throughout (ViewModel pattern: `MainViewModel` with `INotifyPropertyChanged`)
- No code-behind logic beyond event wiring; all state in `MainViewModel`

#### 5.2 `MainViewModel`
- Properties: `LoadedShow`, `SelectedSong`, `PlaybackState`, `HasUnsavedChanges`, `StatusText`
- Commands: `AddSongCommand`, `RemoveSongCommand`, `SaveShowCommand`, `SaveAsShowCommand`, `OpenShowCommand`, `NewShowCommand`
- All WPF controls bound to ViewModel properties

#### 5.3 Setlist Panel
- `ListBox` or custom `ItemsControl` bound to `LoadedShow.Songs`
- Currently cued song highlighted via binding to `SelectedSong`
- Drag-and-drop reordering using WPF `AllowDrop` and `DragDrop` events (or an attached behaviour)
- Click to select a song — updates `SelectedSong`
- "Add Song" button → `FolderBrowserDialog` → `SongScanner.Scan()` → `SongEntry.CreateFromScan()` → append to `Songs`

#### 5.4 Mixer Panel
- Bound to `SelectedSong.Channels`
- Each channel rendered as a row: channel name, `Slider` (0.0–1.0) bound to `ChannelSettings.Level`, `CheckBox` bound to `ChannelSettings.Muted`
- `IsEnabled` on all controls bound to `PlaybackState != Playing && PlaybackState != Paused`
- Video audio channels visually distinguished (italic name, or a camera icon)

#### 5.5 Show File Operations
- File menu: New, Open, Save, Save As
- Unsaved changes tracked via `HasUnsavedChanges` flag
- On New / Open / Close: if `HasUnsavedChanges`, show a `MessageBox` prompt ("Save changes before continuing?")
- Window title: `VideoJam — {show name}{*}` where `*` appears when dirty

#### 5.6 Display Routing Configuration UI
- A simple settings panel (or dialog) showing the global suffix → display index mapping
- Per-song routing overrides: accessible via a button on each setlist item (opens a small dialog)
- In MVP, this can be a simple editable grid — not a polished design

#### 5.7 Fallback Image Assignment
- Per-display fallback image assignment: a button per display (when detected) opens a file picker for PNG selection

### Dependencies
- Phase 4 complete (for `ShowFileService`, `PathResolver`, `SongScanner`, models)

---

## Phase 6: Playback Control & Integration

### Goal
The full end-to-end performance workflow works. Button A starts playback; Button B pauses and rewinds; songs auto-advance; the operator UI hides during playback on Display 0.

### Success Criteria
- [ ] Pressing `Space` on the keyboard starts playback when a song is cued
- [ ] All audio stems and video streams start within 10ms (verified with known-good test set)
- [ ] Pressing `Escape` during playback pauses all streams simultaneously
- [ ] Pressing `Escape` again rewinds to the beginning
- [ ] After a song ends, the next song is automatically cued
- [ ] After the last song, the app returns to Idle with all displays showing fallback PNGs
- [ ] Operator UI disappears from Display 0 when playback begins; reappears when the song ends
- [ ] Clicking a song in the setlist (when not playing) cues it immediately

### Tasks

#### 6.1 `PlaybackEngine` — State Machine
- Implement the four-state state machine from Technical Spec Section 13
- `Cue(int songIndex)`: load audio and video pipelines; transition to `Cued`
- `Go()`: validate state is `Cued`; call `SyncCoordinator.Start()`; transition to `Playing`
- `StopAndRewind()`:
  - From `Playing` → `Paused` (pause audio + video)
  - From `Paused` → `Cued` (stop and reload pipelines; transition to `Cued`)
- `OnPlaybackEnded()` (called by `AudioEngine.PlaybackEnded` event):
  - Stop video engine
  - Advance cued song index; transition to `Cued` (or `Idle` if end of setlist)
  - Show operator UI on Display 0

#### 6.2 `HotkeyService` — Global Hook
- Register `WH_KEYBOARD_LL` hook via P/Invoke on `SetWindowsHookEx`
- Capture keydown events for the configured Button A and Button B keys
- Raise `ButtonAPressed` and `ButtonBPressed` events
- Unhook on dispose
- Default bindings: `Space` → Button A, `Escape` → Button B
- Make key bindings configurable via a simple app settings file (`appsettings.json` in the app directory)

#### 6.3 `MainViewModel` — Playback Integration
- Connect `HotkeyService` events to `PlaybackEngine` operations
- Update `PlaybackState` binding when engine state changes
- Disable setlist click-to-cue when `PlaybackState == Playing`

#### 6.4 Display 0 Takeover During Playback
- On `Go()`: hide `MainWindow`; show the Display 0 `VlcDisplayWindow` (or fallback PNG if no `_lyrics` video)
- On `OnPlaybackEnded()`: hide Display 0 `VlcDisplayWindow`; show `MainWindow`
- On `StopAndRewind()` (from Playing to Paused): keep Display 0 in video state (song is paused, not ended)
- On `StopAndRewind()` (from Paused to Cued): restore `MainWindow`; show fallback PNG on Display 0

#### 6.5 Pre-loading on Cue
- When `Cue(songIndex)` is called, immediately begin scanning the song folder and loading the audio/video pipelines
- If scanning has not completed by the time GO is pressed, disable the GO action and show a brief loading indicator
- In practice this should complete in well under a second on any reasonable machine

#### 6.6 Phase 6 End-to-End Test
- Using the known-good test set, run a complete mock performance:
  - Load a show with 3 songs
  - Press GO, let the first song play to completion
  - Verify auto-advance to song 2
  - Press GO, then pause with ESC, then rewind with ESC
  - Press GO to restart song 2
  - Let all songs complete; verify Idle state

### Dependencies
- Phases 1–5 all complete

---

## Phase 7: Hardening

### Goal
The app handles all anticipated failure conditions gracefully without crashing or stopping audio during performance.

### Success Criteria
- [ ] Disconnecting an HDMI cable mid-song does not interrupt audio (verified manually)
- [ ] A missing song folder produces a clear error in the UI at load time, not a crash
- [ ] A corrupt or unreadable audio file produces a warning in the mixer panel, not a crash
- [ ] A malformed `.show` file produces a descriptive error dialog
- [ ] Application logs are written to `%APPDATA%\VideoJam\logs\`
- [ ] The app prompts to save unsaved changes before closing

#### 7.1 Display Disconnect Isolation
- Subscribe to `SystemEvents.DisplaySettingsChanged`
- On event: check `Screen.AllScreens` against known displays
- If a display has been removed: call `VideoEngine.HandleDisplayDisconnect(displayIndex)` — stops and disposes the affected MediaPlayer silently
- Confirm via manual test that `AudioEngine` is not involved in this code path in any way

#### 7.2 File Validation at Cue Time
- In `Cue(songIndex)`:
  - Verify song folder exists and is readable
  - Attempt to open each audio file (catch exceptions from `AudioFileReader` / `MediaFoundationReader` construction)
  - For any failed file: add a warning to the mixer panel channel row; skip the channel (do not add to the mix)
  - If no audio channels load successfully: surface a clear error; do not allow GO

#### 7.3 Structured Logging
- Add `Microsoft.Extensions.Logging` with a rolling file sink to `%APPDATA%\VideoJam\logs\videojam.log`
- Log at `Information` level: song cued, GO pressed, playback started/ended, display disconnect events
- Log at `Warning` level: file not found, file unreadable, unrecognised format
- Log at `Error` level: WASAPI failure, VLC crash, unexpected exceptions
- Retain logs for 7 days; max 10MB per file

#### 7.4 Unhandled Exception Handler
- Register `Application.DispatcherUnhandledException` and `AppDomain.UnhandledException`
- On unhandled exception: log the full stack trace; show a simple error dialog with a reference to the log file; do not silently swallow

#### 7.5 Application Close Handling
- On `MainWindow.Closing`: if `PlaybackState == Playing` or `PlaybackState == Paused`, warn the operator ("Playback is active. Are you sure you want to quit?")
- If `HasUnsavedChanges`: prompt to save

### Dependencies
- Phase 6 complete

---

## Phase 8: Release Packaging

### Goal
A portable zip that a non-technical operator can unzip and run on any Windows 11 machine.

### Success Criteria
- [ ] `VideoJam.exe` runs on a clean Windows 11 machine with no pre-installed dependencies
- [ ] All VLC codec plugins are included in the zip
- [ ] The zip is under 150MB (VLC plugins are the main contributor to size)
- [ ] The operator's known-good test set plays correctly on the clean machine

### Tasks

#### 8.1 Publish Configuration
- Confirm `dotnet publish` command from Technical Spec Section 18.1 produces a complete output folder
- Verify `VideoLAN.LibVLC.Windows` NuGet package correctly copies native binaries and plugin folder to output
- Add a post-build script that zips the output folder to `VideoJam-{version}.zip`

#### 8.2 Version Numbering
- Add `<Version>` property to the project file
- Use semantic versioning: `1.0.0` for the first release
- Display version in the `MainWindow` title bar or About dialog

#### 8.3 Clean Machine Test
- Copy the zip to a Windows 11 machine that has never had .NET, VLC, or any VideoJam dependency installed
- Unzip and run
- Load a `.show` file and run through the full performance workflow with the known-good test set
- Verify no missing DLL errors, no codec failures, no display issues

#### 8.4 Optional: Simple `README.txt`
- Plain text file in the zip root explaining:
  - How to launch the app
  - Expected folder structure for song folders
  - How to set up a show
  - Button A / Button B key bindings

### Dependencies
- Phase 7 complete

---

## Dependency Map

```
Phase 1 (Audio Engine)
    └── Phase 2 (Video + Sync)
            └── Phase 3 (Multi-Display)
Phase 1 (Audio Engine)
    └── Phase 4 (Show Model & Persistence)  ← can run in parallel with Phase 3
            └── Phase 5 (Operator UI)
                    └── Phase 6 (Playback Integration)  ← requires Phases 1–5
                            └── Phase 7 (Hardening)
                                    └── Phase 8 (Release Packaging)
```

Phases 3 and 4 can be developed in parallel by two developers. All other phases are sequential.

---

## Risk Register

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|-----------|
| VLC pre-buffer trick is unreliable for sync | Medium | High | Test early in Phase 2; fallback: calibrate a fixed delay offset |
| WASAPI buffer underrun on slow/busy machine | Low | High | Increase buffer to 100ms if needed; test on minimum-spec hardware |
| LibVLC HWND integration fails with WPF DPI virtualisation | Medium | Medium | Set DPI-aware manifest early; test on HiDPI display in Phase 2 |
| Audio from MP4 via `MediaFoundationReader` fails for certain encodings | Low | Medium | Test in Phase 1 with representative video files; fallback: FFmpegMediaFoundationReader |
| VLC plugin folder not correctly copied by NuGet package | Low | High | Verify publish output in Phase 2; add explicit copy task to project file if needed |
| Multi-monitor window positioning wrong on mixed-DPI setups | Medium | Medium | Address in Phase 3; requires testing on real hardware with mixed-DPI monitors |
| Global keyboard hook conflicts with other software on operator's laptop | Low | Low | Make key bindings configurable (Phase 6); document workaround |

---

## Suggested Phase 1 Sprint Breakdown

For a single developer, Phase 1 can be completed in approximately 2–3 focused working days:

| Day | Tasks |
|-----|-------|
| Day 1 AM | 1.1 Solution setup, project structure, NuGet packages |
| Day 1 PM | 1.3 `SongScanner` implementation |
| Day 2 AM | 1.4 `SongScanner` unit tests (full coverage) |
| Day 2 PM | 1.5 `AudioEngine` core pipeline |
| Day 3 AM | 1.6 Integration harness; manual multi-stem test |
| Day 3 PM | 1.7 `ChannelSettings` unit tests; review and tidy |

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-02-19 | Technical Architect (AI-assisted) | Initial draft |
