# VideoJam — Requirements Document

**Version:** 1.0
**Date:** 2026-02-19
**Status:** Draft
**Prepared by:** Requirements Analyst (AI-assisted)
**For:** Architecture & Planning Team

---

## 1. Executive Summary

VideoJam is a Windows desktop application for live musical performance that synchronises playback of multiple audio stems and video files across multiple hardware displays. A musician operating a laptop on stage can load a pre-built setlist, set mix levels before the show, and then trigger each song with a single button press — after which all audio and video streams play in lockstep to the end of the track with no further interaction required.

---

## 2. Project Context

### 2.1 Background & Motivation

Live bands frequently perform with pre-recorded backing tracks ("stems") — isolated recordings of individual instruments — as well as synchronised video content for audience screens. Existing media players do not provide a unified solution for simultaneously managing multi-stem audio mixing, multi-display video routing, and tight A/V synchronisation in a live performance context. VideoJam is a purpose-built tool for this workflow.

### 2.2 Scope

**In Scope:**
- Synchronised playback of multiple audio stems per song
- Synchronised playback of video files routed to specific hardware displays
- Mixing of all audio sources (stems and video-embedded audio) to a stereo output
- Setlist (show) creation, ordering, and persistence as `.show` files
- Per-display fallback PNG images for between-song states
- Two-button performance control model (keyboard or presentation clicker)
- Support for up to 3–4 simultaneous hardware displays
- Graceful degradation to single-display mode

**Out of Scope:**
- Live mixing or level changes during playback
- Sound engineering during performance
- Real-time effects processing or signal routing beyond stereo mix-down
- Streaming or networked playback
- MIDI or timecode integration
- Tech stack selection (deferred — see Open Questions)

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
| FR-003 | Video playback | Play MP4/H.264 video files routed to specific hardware displays. | Must Have |
| FR-004 | Universal audio mixing | All audio sources — standalone stem files and audio tracks embedded in video files — are treated as independent channels in a common mixer. Each channel has an independently configurable level and mute state. | Must Have |
| FR-005 | Pre-show level setting | Stem levels and mute states can be configured before or between songs. Levels are locked during playback. | Must Have |
| FR-006 | Stereo mix output | All audio channels are mixed to stereo and output via the system audio device (headphone jack). | Must Have |
| FR-007 | Display routing — defaults | Video files are routed to displays by filename suffix. Default: `*_lyrics` → Display 1 (laptop), `*_visuals` → Display 2 (external). Additional suffixes (e.g. `*_stage`, `*_rear`) route to Display 3 and Display 4. | Must Have |
| FR-008 | Display routing — overrides | Global suffix-to-display mapping can be overridden on a per-song basis. | Must Have |
| FR-009 | Fallback PNG images | A per-show fallback PNG image is defined for each display. Shown when no video file is assigned to that display for the current song, and on all non-primary displays between songs. | Must Have |
| FR-010 | Playback — fire and forget | Once triggered, all streams play to the end of the song without further operator input. | Must Have |
| FR-011 | Synchronisation | All audio and video streams start simultaneously and remain within 10ms of each other throughout playback. | Must Have |
| FR-012 | Two-button control model | Full performance control via two buttons (keyboard keys or presentation clicker). See User Journeys for button behaviour. | Must Have |
| FR-013 | Song selection via UI | Operator can click any song in the setlist UI to cue it, bypassing sequential navigation. | Must Have |
| FR-014 | Show / setlist management | A show is an ordered collection of songs. Songs are added by selecting their folders. Order is set by drag-and-drop. | Must Have |
| FR-015 | Show file persistence | Shows are saved as `.show` files that can be opened, closed, and copied between machines. Multiple `.show` files can exist; the operator opens whichever is needed. | Must Have |
| FR-016 | Single-display mode | When only one display is connected, the app operates using the primary display only. Secondary display content is not rendered. | Must Have |
| FR-017 | Video audio unmuting | Video-embedded audio tracks are muted by default. Individual video audio tracks can be unmuted and mixed alongside audio stem files (e.g. for songs where all audio is baked into the video file). | Should Have |
| FR-018 | End-of-setlist behaviour | After the last song completes, all displays revert to their fallback PNG state. No automatic wrap-around. | Must Have |

### 4.2 User Journeys

#### Journey 1: Building a Show (Pre-Show Setup)

1. Operator launches VideoJam and creates a new show, or opens an existing `.show` file.
2. Operator adds songs by selecting song folders from the filesystem.
3. Operator drags songs into the desired running order.
4. For each display, operator assigns a fallback PNG image (e.g. a band poster for the audience screen).
5. Operator reviews the global display routing config (suffix → display mappings) and applies any per-song overrides needed.
6. Operator saves the show as a `.show` file.

#### Journey 2: Setting Levels (Pre-Show / Between Songs)

1. Operator selects a song in the setlist.
2. The app displays all audio channels for that song: standalone stem files and any audio tracks embedded in video files.
3. Operator adjusts level and mute state for each channel.
4. Operator saves the show.

#### Journey 3: Running a Performance

1. Operator opens the `.show` file for tonight's set.
2. The app UI is visible on the laptop screen. All non-primary displays show their fallback PNG.
3. The first song is cued (highlighted) by default.
4. Operator presses **Button A**: the app begins playback of all streams for the cued song. The laptop screen is taken over by the song's primary video (or fallback PNG if none). External displays show their assigned video files.
5. All streams play to completion. Displays revert to fallback PNG state. App UI returns to the laptop screen.
6. Operator clicks the next song in the setlist UI (or it is automatically advanced), then presses **Button A** to begin the next song.

#### Journey 4: Pausing and Recovering Mid-Song

1. During playback, operator presses **Button B**: all streams pause simultaneously.
2. Operator presses **Button B** again: all streams rewind to the beginning of the song. The app returns to the idle/cued state with the UI visible.
3. Operator presses **Button A** to restart the song from the beginning.

### 4.3 Integrations

| System | Type | Notes |
|--------|------|-------|
| Windows audio subsystem | OS API | Stereo output to default audio device (headphone jack) |
| Windows display subsystem | OS API | Enumerate and drive up to 3–4 hardware displays |
| Filesystem | Local | Song folders and `.show` files read from local storage |

### 4.4 Data Requirements

#### Song Folder
A folder named for the song, containing:
- Zero or more audio stem files (WAV, MP3, AIFF)
- Zero or more video files (MP4/H.264) with filename suffixes indicating their target display
- No required manifest file — the app infers roles from filenames and configuration

#### Show File (`.show`)
A persisted document containing:
- Ordered list of song folder references (paths)
- Global display routing configuration (suffix → display index mappings)
- Per-song display routing overrides
- Per-display fallback PNG assignments
- Per-song, per-channel audio level and mute settings

#### Display Routing Configuration
- Global: maps filename suffixes to display indices (e.g. `_lyrics → 1`, `_visuals → 2`)
- Per-song overrides: replaces or augments the global mapping for a specific song

---

## 5. Non-Functional Requirements

| ID | Category | Requirement | Target / Metric |
|----|----------|-------------|-----------------|
| NFR-001 | Synchronisation | All audio and video streams must start and remain in sync | Within 10ms across all streams throughout playback |
| NFR-002 | Platform | Windows only | Windows (minimum version TBD — see Open Questions) |
| NFR-003 | Audio output | Stereo mix to system audio device | Headphone jack; no multi-channel interface required |
| NFR-004 | Reliability | No audible glitches or dropped video frames during live performance | Zero tolerance in performance context |
| NFR-005 | Startup | App must be ready for use quickly | Target: operational within 30 seconds of launch |
| NFR-006 | Display count | Support up to 4 simultaneous hardware displays | 1–4 displays |
| NFR-007 | Portability | `.show` files must be copyable between Windows machines | Song folder paths must resolve correctly after copy — likely relative paths |
| NFR-008 | Control latency | Button presses must register immediately | No perceptible delay between button press and playback response |

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
| Tech stack | Not specified at requirements stage | See Open Questions |

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

---

## 8. Open Questions & Risks

| # | Question / Risk | Owner | Priority |
|---|-----------------|-------|----------|
| 1 | What is the minimum supported Windows version? (Windows 10 22H2? Windows 11?) | Architect | High |
| 2 | How are `.show` file paths stored — relative to the `.show` file, or absolute? Relative paths are required for portability across machines, but constrain folder layout. | Architect | High |
| 3 | Tech stack selection — language, framework, and media engine are deliberately deferred from requirements. This is the primary architectural decision. | Architect | High |
| 4 | How is the app distributed and installed? (Installer, portable `.exe`, winget, etc.) | Product Owner | Medium |
| 5 | What happens if a display is disconnected mid-performance? Should the app degrade gracefully or alert the operator? | Product Owner | Medium |
| 6 | Should there be a visual sync indicator or diagnostic mode for setup/testing? (e.g. a test tone + frame counter to verify streams are aligned) | Product Owner | Medium |
| 7 | Is there a requirement to handle songs with stems of different durations? (e.g. a short ambient intro stem that ends before the song does) | Product Owner | Low |
| 8 | Should the app support a "rehearsal mode" where the external display is simulated in a window on the laptop? | Product Owner | Low |

---

## 9. Glossary

| Term | Definition |
|------|------------|
| Stem | An isolated audio recording of a single instrument or vocal part, stored as a standalone audio file |
| Show | A named, ordered collection of songs with associated configuration, saved as a `.show` file |
| Song folder | A filesystem folder named for the song, containing all audio and video media files for that song |
| Display routing | The mapping of video files to specific hardware displays, determined by filename suffix |
| Fallback PNG | A static image shown on a display when no video is assigned to it for the current song or state |
| Primary display | Display 1 — typically the laptop's built-in screen; shows the app UI between songs and the `_lyrics` video during playback |
| GO | Colloquial term for Button A — triggers playback of the cued song, or resumes if paused |
| Cued | The state of a song that is selected and ready to play on the next GO press |
| Fire and forget | The playback model where all streams are started simultaneously and play without further operator intervention |

---

## 10. Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-02-19 | Requirements Analyst (AI-assisted) | Initial draft |
