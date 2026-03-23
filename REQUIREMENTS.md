# VideoJam — Requirements Document

**Version:** 2.0
**Date:** 2026-03-20
**Status:** Draft
**Prepared by:** Requirements Analyst (AI-assisted)
**For:** Architecture & Planning Team

---

## 1. Executive Summary

VideoJam is a Windows desktop application for live musical performance that synchronises playback of multiple audio stems and video files across multiple displays. A musician operating a laptop on stage can load a pre-built setlist, set mix levels before the show, and then trigger each song with a single button press — after which all audio and video streams play in lockstep to the end of the track with no further interaction required. Video files play in dockable, repositionable windows that the operator arranges freely across connected displays.

---

## 2. Project Context

### 2.1 Background & Motivation

Live bands frequently perform with pre-recorded backing tracks ("stems") — isolated recordings of individual instruments — as well as synchronised video content for audience screens. Existing media players do not provide a unified solution for simultaneously managing multi-stem audio mixing, multi-display video routing, and tight A/V synchronisation in a live performance context. VideoJam is a purpose-built tool for this workflow.

### 2.2 Scope

**In Scope:**
- Synchronised playback of multiple audio stems per song
- Synchronised playback of video files in dockable, repositionable windows
- Mixing of all audio sources (stems and video-embedded audio) to a stereo output
- Setlist (show) creation, ordering, and persistence as `.show` files
- Single fallback PNG image per show, displayed in all video windows between songs
- Two-button performance control model (keyboard or presentation clicker)
- Support for multiple simultaneous video windows across connected displays
- Graceful degradation to single-display mode

**Out of Scope:**
- Live mixing or level changes during playback
- Sound engineering during performance
- Real-time effects processing or signal routing beyond stereo mix-down
- Streaming or networked playback
- MIDI or timecode integration

### 2.3 Stakeholders

| Role | Description | Interest |
|------|-------------|----------|
| Musician / Operator | Runs the laptop on stage | Simple, reliable performance control |
| Band Members | Perform alongside the backing tracks | Accurate lyrics/chords on stage display |
| Audience | View the external display(s) | Engaging visual experience in sync with music |

---

## 3. User Personas

### 3.1 Musician / Operator
- **Role:** Band member who also operates the laptop on stage
- **Goals:** Start each song reliably with a single button press; navigate the setlist quickly between songs; never have the tool require attention during a song
- **Pain Points:** Cannot afford to look away from the audience or their instrument for more than a glance; any failure during performance is publicly visible
- **Technical Proficiency:** Comfortable with computers but not a developer; expects consumer-grade simplicity for in-performance controls and tolerates moderate complexity in pre-show setup

### 3.2 Audience
- **Role:** Passive recipient of visual output on external display(s)
- **Goals:** See engaging, in-sync visuals throughout the performance
- **Pain Points:** Visible A/V drift or dead screens between songs
- **Technical Proficiency:** N/A

---

## 4. Functional Requirements

### 4.1 Core Features (MVP)

| ID | Feature | Description | Priority |
|----|---------|-------------|----------|
| FR-001 | Song folder structure | Each song is a folder named for the song. The folder contains all media files for that song. | Must Have |
| FR-002 | Audio stem playback | Play all audio files in a song folder simultaneously as independent stems. Supported formats: WAV, MP3, AIFF. | Must Have |
| FR-003 | Video playback | Play MP4/H.264 video files in dockable, maximisable windows. Each video file gets its own window. | Must Have |
| FR-004 | Universal audio mixing | All audio sources — standalone stem files and audio tracks embedded in video files — are treated as independent channels in a common mixer. Each channel has an independently configurable level and mute state. **Level and mute settings must affect actual audio output during playback.** | Must Have |
| FR-005 | Pre-show level setting | Stem levels and mute states can be configured before or between songs. Levels are locked during playback. | Must Have |
| FR-006 | Stereo mix output | All audio channels are mixed to stereo and output via the system audio device (headphone jack). | Must Have |
| FR-007 | Video windows — dockable and repositionable | Each video file plays in its own window. Windows can be dragged, resized, and maximised. The operator positions them on whichever display they choose. | Must Have |
| FR-008 | Video window position persistence | Video window positions and sizes are saved to the `.show` file and restored when the show is reopened. | Must Have |
| FR-009 | Fallback PNG image | A single per-show fallback PNG image is displayed in all video windows when no video is playing (between songs and before the first song). The audience never sees the Windows desktop. | Must Have |
| FR-010 | Playback — fire and forget | Once triggered, all streams play to the end of the song without further operator input. | Must Have |
| FR-011 | Synchronisation | All audio and video streams start simultaneously and remain within 10ms of each other throughout playback. | Must Have |
| FR-012 | Two-button control model | Full performance control via two buttons (keyboard keys or presentation clicker). See User Journeys for button behaviour. | Must Have |
| FR-013 | Song selection via UI | Operator can click any song in the setlist UI to cue it, bypassing sequential navigation. | Must Have |
| FR-014 | Show / setlist management | A show is an ordered collection of songs. Songs are added by selecting their folders. Order is set by drag-and-drop. | Must Have |
| FR-015 | Show file persistence | Shows are saved as `.show` files that can be opened, closed, and copied between machines. Multiple `.show` files can exist; the operator opens whichever is needed. | Must Have |
| FR-016 | Single-display mode | When only one display is connected, the app operates using the primary display only. Video windows remain on the primary display. | Must Have |
| FR-017 | Video audio unmuting | Video-embedded audio tracks are muted by default. Individual video audio tracks can be unmuted and mixed alongside audio stem files (e.g. for songs where all audio is baked into the video file). | Should Have |
| FR-018 | End-of-setlist behaviour | After the last song completes, all video windows revert to the fallback PNG. No automatic wrap-around. | Must Have |
| FR-019 | App UI always visible | The main application window (setlist, mixer controls) remains visible at all times during playback. It is not taken over or hidden by video content. The operator can use standard Windows window management (e.g. Alt-Tab) to bring video windows in front if desired. | Must Have |

### 4.2 User Journeys

#### Journey 1: Building a Show (Pre-Show Setup)

1. Operator launches VideoJam and creates a new show, or opens an existing `.show` file.
2. Operator adds songs by selecting song folders from the filesystem.
3. Operator drags songs into the desired running order.
4. Operator assigns a fallback PNG image for the show (e.g. a band poster for the audience screen).
5. Operator saves the show as a `.show` file.

#### Journey 2: Setting Levels (Pre-Show / Between Songs)

1. Operator selects a song in the setlist.
2. The app displays all audio channels for that song: standalone stem files and any audio tracks embedded in video files.
3. Operator adjusts level and mute state for each channel.
4. Operator saves the show.

#### Journey 3: Arranging Video Windows (Pre-Show Setup)

1. Operator connects external displays.
2. Operator opens or creates a show and triggers a test playback (or the app opens video windows in their last saved positions).
3. Operator drags and resizes video windows to the desired positions on connected displays (e.g. maximises one window on the audience screen, another on the stage monitor).
4. Window positions are saved to the `.show` file.

#### Journey 4: Running a Performance

1. Operator opens the `.show` file for tonight's set.
2. The app UI is visible on the laptop screen. Video windows on all displays show the fallback PNG.
3. The first song is cued (highlighted) by default.
4. Operator presses **Button A**: the app begins playback of all streams for the cued song. Video windows show their assigned video files. The app UI remains visible.
5. All streams play to completion. Video windows revert to the fallback PNG.
6. Operator clicks the next song in the setlist UI (or it is automatically advanced), then presses **Button A** to begin the next song.

#### Journey 5: Pausing and Recovering Mid-Song

1. During playback, operator presses **Button B**: all streams pause simultaneously.
2. Operator presses **Button B** again: all streams rewind to the beginning of the song. The app returns to the idle/cued state.
3. Operator presses **Button A** to restart the song from the beginning.

### 4.3 Integrations

| System | Type | Notes |
|--------|------|-------|
| Windows audio subsystem | OS API | Stereo output to default audio device (headphone jack) |
| Windows display subsystem | OS API | Enumerate connected displays; video windows can be positioned on any display |
| Filesystem | Local | Song folders and `.show` files read from local storage |

### 4.4 Data Requirements

#### Song Folder
A folder named for the song, containing:
- Zero or more audio stem files (WAV, MP3, AIFF)
- Zero or more video files (MP4/H.264)
- No required manifest file — the app discovers all media files in the folder

#### Show File (`.show`)
A persisted document containing:
- Ordered list of song folder references (paths)
- Fallback PNG assignment (one per show)
- Per-song, per-channel audio level and mute settings
- Video window positions and sizes

#### Video Window Configuration
- Window position (x, y), size (width, height), and maximised state
- Persisted per-show in the `.show` file
- Restored on show load

---

## 5. Non-Functional Requirements

| ID | Category | Requirement | Target / Metric |
|----|----------|-------------|-----------------|
| NFR-001 | Synchronisation | All audio and video streams must start and remain in sync | Within 10ms across all streams throughout playback |
| NFR-002 | Platform | Windows only | Windows (minimum version TBD — see Open Questions) |
| NFR-003 | Audio output | Stereo mix to system audio device | Headphone jack; no multi-channel interface required |
| NFR-004 | Reliability | No audible glitches or dropped video frames during live performance | Zero tolerance in performance context |
| NFR-005 | Startup | App must be ready for use quickly | Target: operational within 30 seconds of launch |
| NFR-006 | Display count | Support multiple simultaneous connected displays | Video windows can be placed on any connected display |
| NFR-007 | Portability | `.show` files must be copyable between Windows machines | Song folder paths must resolve correctly after copy — likely relative paths |
| NFR-008 | Control latency | Button presses must register immediately | No perceptible delay between button press and playback response |
| NFR-009 | UI completeness | No UI element may be present in a release unless its underlying functionality is fully wired up | Zero non-functional controls; if a feature isn't ready, its control must not be visible |

### 5.1 Compliance & Regulatory

No compliance or regulatory obligations identified. This is a standalone desktop tool with no network connectivity, user accounts, or personal data collection.

---

## 6. Technical Constraints

| Constraint | Detail | Reason |
|------------|--------|--------|
| Operating system | Windows only | Target users are on Windows; macOS/Linux not required |
| Audio output | Stereo via system audio device | No audio interface; headphone jack to PA |
| Input devices | Standard keyboard and/or Logitech presentation clicker | Operator uses two-button control during performance |
| Audio formats | WAV, MP3, AIFF | Common formats used in music production |
| Video formats | MP4 container, H.264 codec | Widely used; hardware-accelerated decode available on Windows |
| Image formats | PNG | Fallback display images |
| Show file format | `.show` (format TBD) | Custom project file; must be human-copyable |

---

## 7. Assumptions

1. All media files within a song folder begin at timecode 0 and represent the full duration of the song — no offset or trim is needed.
2. All stem files for a given song are the same duration; the song is considered complete when the longest stream finishes.
3. The operator has access to the filesystem to organise song folders before show time; no in-app media editing is required.
4. The laptop has at least one audio output (headphone jack) and at least one external display output.
5. Display hardware is connected and recognised by Windows before the app is launched; hot-plugging displays mid-show is not a supported workflow.
6. Video files are encoded at standard frame rates (24, 25, 30, or 60 fps); sub-frame A/V sync accuracy is not required.
7. The 10ms sync requirement applies to stream start alignment and ongoing drift; occasional single-frame (~16ms) video presentation jitter is acceptable if audio streams remain tightly aligned.
8. No internet connection is required; the app operates entirely offline.
9. The operator is responsible for PA-level volume control via the mixing desk; VideoJam only controls the relative levels of stems within its own mix.
10. Video window positions are managed by the operator during pre-show setup; the app does not automatically assign windows to displays.

---

## 8. Open Questions & Risks

| # | Question / Risk | Owner | Priority |
|---|-----------------|-------|----------|
| 1 | What is the minimum supported Windows version? (Windows 10 22H2? Windows 11?) | Architect | High |
| 2 | How are `.show` file paths stored — relative to the `.show` file, or absolute? Relative paths are required for portability across machines, but constrain folder layout. | Architect | High |
| 3 | How is the app distributed and installed? (Installer, portable `.exe`, winget, etc.) | Product Owner | Medium |
| 4 | What happens if a display is disconnected mid-performance? Should video windows redistribute or alert the operator? | Product Owner | Medium |
| 5 | Should there be a visual sync indicator or diagnostic mode for setup/testing? (e.g. a test tone + frame counter to verify streams are aligned) | Product Owner | Medium |
| 6 | Is there a requirement to handle songs with stems of different durations? (e.g. a short ambient intro stem that ends before the song does) | Product Owner | Low |
| 7 | How should video windows behave when a show is first created and no saved positions exist? Should they tile, stack, or open at a default size? | Architect | Medium |

---

## 9. Known Defects (as of v2.0)

The following issues have been identified in the current build and must be resolved:

| # | Defect | Relates To | Severity |
|---|--------|------------|----------|
| 1 | **Multi-display video regression:** Only the primary display receives video. The second video file never renders on the secondary display. This was previously working. | FR-003, FR-007 | Critical |
| 2 | **Mixer controls non-functional:** Volume sliders and mute buttons are present in the UI and their state is saved to the `.show` file, but they have no effect on audio output during playback. | FR-004, FR-005, NFR-009 | High |

---

## 10. Glossary

| Term | Definition |
|------|------------|
| Stem | An isolated audio recording of a single instrument or vocal part, stored as a standalone audio file |
| Show | A named, ordered collection of songs with associated configuration, saved as a `.show` file |
| Song folder | A filesystem folder named for the song, containing all audio and video media files for that song |
| Video window | A dockable, maximisable window that displays video content or the fallback PNG; positioned freely by the operator |
| Fallback PNG | A static image shown in all video windows when no video is playing for the current song or state |
| Primary display | Display 1 — typically the laptop's built-in screen; hosts the main app UI |
| GO | Colloquial term for Button A — triggers playback of the cued song |
| Cued | The state of a song that is selected and ready to play on the next GO press |
| Fire and forget | The playback model where all streams are started simultaneously and play without further operator intervention |

---

## 11. Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-02-19 | Requirements Analyst (AI-assisted) | Initial draft |
| 2.0 | 2026-03-20 | Requirements Analyst (AI-assisted) | Replaced hard-coded display routing with dockable video window model (FR-007, FR-008 replaced); added UI completeness constraint (NFR-009); added known defects section; updated user journeys for new window model; simplified fallback PNG to one per show; added video window position persistence (FR-008); added FR-019 (app UI always visible) |
