## Why

VideoJam is a live-performance desktop application. Before any UI, show management, or video playback can be built, the audio engine must exist and be proven stable — it is the last line of defence during a live show. Phase 1 establishes the solution foundation and delivers a validated audio pipeline that mixes multiple stems into a single WASAPI output with sample-accurate inter-stem synchronisation.

## What Changes

- **New**: `VideoJam.sln` solution with two projects: `VideoJam` (WPF, .NET 10, win-x64) and `VideoJam.Tests` (xUnit)
- **New**: `Directory.Build.props` at solution root pinning all third-party NuGet package versions
- **New**: Full source folder structure (`Engine/`, `Model/`, `Services/`, `UI/`, `Input/`) with stub files for all planned classes
- **New**: All runtime model types: `SongManifest`, `AudioChannelManifest`, `VideoFileManifest`, `AudioChannelType` enum
- **New**: All persisted model types: `Show`, `SongEntry`, `ChannelSettings` (with correct defaults)
- **New**: `SongScanner` — scans a folder and classifies files into audio stems and video audio channels
- **New**: `AudioEngine` — NAudio pipeline: loads stems into `VolumeSampleProvider`s, mixes via `MixingSampleProvider`, outputs via `WasapiOut` (shared, 50ms buffer); exposes `Play()`, `Stop()`, `PlaybackEnded`
- **New**: Phase 1 integration harness — minimal WPF window with Load/Play/Stop controls for manual multi-stem sync verification
- **New**: Unit tests for `SongScanner` (6 scenarios) and `ChannelSettings` defaults

## Capabilities

### New Capabilities

- `solution-foundation`: Solution scaffolding, project structure, NuGet version pinning, stub class files for all planned components
- `song-model`: Runtime records (`SongManifest`, `AudioChannelManifest`, `VideoFileManifest`) and persisted model classes (`Show`, `SongEntry`, `ChannelSettings`) with correct defaults
- `song-scanner`: File-system scan of a song folder, classification of audio stems vs. video audio channels, suffix extraction for video files, `SongManifest` production
- `audio-engine`: NAudio pipeline — multi-stem loading, per-stem volume control, single mixed WASAPI output, play/stop lifecycle, `PlaybackEnded` event on natural end

### Modified Capabilities

*(None — this is a greenfield phase.)*

## Impact

- **Projects created**: `VideoJam/VideoJam.csproj`, `VideoJam.Tests/VideoJam.Tests.csproj`, `VideoJam.sln`, `Directory.Build.props`
- **NuGet dependencies introduced**: NAudio 2.2.x, LibVLCSharp 3.x (added now, used Phase 2+), VideoLAN.LibVLC.Windows 3.x (added now, used Phase 2+), Microsoft.Extensions.Logging 8.x, xUnit 2.x (test project)
- **Primary technical risk**: `MediaFoundationReader` (NAudio's MP4 audio decoder) relies on Windows Media Foundation codecs. Certain MP4 encodings (AAC-LC with unusual sample rates, HEVC audio) may fail to decode on a stock Windows 11 machine. This must be validated in the Phase 1 integration harness using representative video files from the operator's test set. Fallback: `FFmpegMediaFoundationReader` or pre-conversion to WAV.
- **No breaking changes** — nothing exists yet.

## Non-goals for this Phase

- Video playback of any kind (VLC, WPF MediaElement, etc.)
- Show file save/load (`.show` JSON format)
- Operator UI (setlist panel, mixer panel, status bar)
- Display management or multi-monitor support
- Global hotkey service
- Playback state machine (`Idle` / `Cued` / `Playing` / `Paused`)
- Logging infrastructure (other than console output in the harness)
- Mute logic (set `VolumeSampleProvider.Volume = 0` — that wiring is deferred to Phase 5)
