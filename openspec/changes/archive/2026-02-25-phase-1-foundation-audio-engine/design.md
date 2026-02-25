## Context

VideoJam is a greenfield WPF/.NET 10 application. No code exists yet. This design covers Phase 1 only: the solution scaffold and the audio pipeline that will underpin every subsequent phase. Getting the audio engine right before anything else is built is the central architectural discipline of this project — a shaky foundation at this stage propagates into every phase that follows.

All architectural decisions documented in the Technical Specification (§2) are treated as settled. This document records the rationale for Phase 1-specific implementation choices that were not fully specified.

## Goals / Non-Goals

**Goals:**
- Establish a compilable, warning-free solution with the correct project/folder structure
- Produce a `SongScanner` that correctly classifies all file types a song folder may contain
- Produce an `AudioEngine` whose NAudio pipeline is correct, stable, and disposes cleanly
- Validate multi-stem WASAPI playback manually via an integration harness
- Validate `SongScanner` and `ChannelSettings` defaults with automated unit tests

**Non-Goals:**
- Video, VLC, or any media playback other than NAudio audio
- Operator UI (setlist, mixer, show file management)
- State machine, hotkey service, logging infrastructure
- Mute wiring (setting `VolumeSampleProvider.Volume = 0` for muted channels is a Phase 5 concern)
- Show file persistence (Phase 4)

## Decisions

### D1 — Single `WasapiOut` instance with `MixingSampleProvider`

**Decision:** All audio channels (stems + video audio tracks) are fed into one `MixingSampleProvider`, which is attached to a single `WasapiOut`. There is no per-channel output device and no secondary WASAPI instance.

**Rationale:** This is the only architecture that guarantees sample-accurate inter-stem synchronisation. With a single WASAPI callback driving all channels, they share one audio clock. Any design that starts multiple `WasapiOut` instances — even with careful timing — is subject to independent hardware clock drift. Given the core NFR (≤10ms sync), this is non-negotiable.

**Alternative considered:** Per-channel `WasapiOut` instances started in rapid succession. Rejected because even a 1ms gap between `Play()` calls becomes audible phasing on correlated material (e.g. parallel drum stems).

---

### D2 — `WasapiOut` in shared mode at 50ms buffer

**Decision:** WASAPI shared mode, 50ms latency buffer. Exclusive mode is explicitly excluded.

**Rationale:** Shared mode avoids locking the audio device, meaning system sounds, browser audio, and other Windows audio activity continue to function on the operator's laptop. The 50ms buffer is a conservative choice that prevents underruns on machines that may have background processes. For a fire-and-forget application where the operator presses GO and waits, 50ms of latency is imperceptible and the trade-off is clearly correct. If an operator's machine has a particularly busy CPU, the buffer can be raised to 100ms in app settings.

**Alternative considered:** WASAPI exclusive mode for lower latency. Rejected — the latency saved (typically 10–20ms) does not justify the risk of device-lock failures if another app is using audio.

---

### D3 — `MediaFoundationReader` for MP4 audio extraction

**Decision:** Audio tracks within `.mp4` files are decoded using NAudio's `MediaFoundationReader`, which delegates to the Windows Media Foundation codec pipeline.

**Rationale:** This is the simplest path — no additional native dependencies, no separate FFmpeg binary, and it works correctly for H.264/AAC MP4s (the dominant format for live performance video files). The operator will validate this with their actual video files during Phase 1 manual testing.

**Risk:** `MediaFoundationReader` will fail for MP4 files with unusual audio encodings (e.g. ALAC, Opus, certain AC-3 variants) that Windows Media Foundation does not support. If validation fails, the fallback is `FFmpegMediaFoundationReader` from `NAudio.Extras`, or pre-converting problem files to WAV.

**Alternative considered:** Always extracting audio to a WAV staging file via FFmpeg before loading. Rejected for Phase 1 — it introduces a native subprocess dependency and adds complexity before we know it's needed.

---

### D4 — `AudioFileReader` vs `AiffFileReader` selection

**Decision:** File reader is chosen by `channel.File.Extension` (case-insensitive) at `Load()` time:
- `.wav`, `.mp3` → `AudioFileReader` (NAudio's auto-detecting reader, handles both)
- `.aiff` → `AiffFileReader` (explicit, as `AudioFileReader` does not reliably detect AIFF)
- `.mp4` → `MediaFoundationReader`

The reader is constructed with `channel.File.FullName` as its path argument — never with a raw string derived from user input.

**Rationale:** `AudioFileReader` handles WAV and MP3 natively via the ACM decoder. AIFF requires `AiffFileReader` explicitly. Using `FileInfo.Extension` is consistent with the `FileInfo`-typed `AudioChannelManifest.File` property, mirrors the classification logic in `SongScanner`, and means the reader selection and the manifest share one source of truth about what the file is.

---

### D5 — `ChannelSettings` default values

**Decision:**
- Audio stems: `Level = 1.0f`, `Muted = false`
- Video audio channels: `Level = 1.0f`, `Muted = true`

**Rationale:** Stems are expected to be heard at full level by default. Video audio (the audio track extracted from a `.mp4` file) is muted by default because VideoJam's architecture routes video audio through NAudio — but the operator almost always wants the video to play silently while audio comes from dedicated stem tracks. Defaulting video audio to muted prevents an unexpected double-audio situation on first use.

---

### D6 — Phase 1 integration harness as a minimal WPF window (not a console app)

**Decision:** The Phase 1 harness is a minimal WPF window in `MainWindow.xaml`, not a separate console application.

**Rationale:** The final application is a WPF app. A WPF harness tests WASAPI behaviour in the same threading environment (STA thread, WPF dispatcher) that the production code will run in. A console app would require explicit STA threading setup and might mask WPF-specific threading issues. The harness XAML/code-behind is temporary and will be replaced by the full operator UI in Phase 5.

---

### D7 — Solution structure: `VideoJam/` subfolder for the WPF project

**Decision:** The WPF application project lives in `VideoJam/VideoJam.csproj`, not at the repo root. The solution file `VideoJam.sln` lives at the repo root. Tests live in `VideoJam.Tests/VideoJam.Tests.csproj`.

**Rationale:** This is the standard Visual Studio multi-project layout. It keeps project-specific assets (XAML, resources) cleanly separated from solution-level files (`.gitignore`, `Directory.Build.props`, docs).

## Risks / Trade-offs

| Risk | Likelihood | Mitigation |
|------|-----------|-----------|
| `MediaFoundationReader` fails to decode the operator's MP4 audio tracks | Medium | Test with real video files in Phase 1 harness. Fallback: `NAudio.Extras.FFmpegMediaFoundationReader` or pre-convert to WAV. |
| WASAPI shared-mode underruns on a busy machine | Low | Default 50ms buffer. Raise to 100ms via setting if needed. Profile the audio callback thread — no I/O, no locks. |
| `MixingSampleProvider` format mismatch (mismatched sample rates/channel counts) | Low-Medium | All sources must be resampled to a common format (44100 Hz, 32-bit float, stereo) before entering the mixer. Add an `ISampleProvider` resampling step (`WdlResamplingSampleProvider` or `NAudio`'s `MediaFoundationResampler`) wherever the source format differs from the mix format. Validate in the harness with mixed-format test files. |
| Harness code-behind leaks audio resources if Play/Stop called out of order | Low | `AudioEngine.Stop()` must be idempotent and safe to call in any state. Add a guard (`if (_wasapiOut == null) return;`). |

## Migration Plan

N/A — greenfield. No existing code, no deployment, no rollback needed.

## Open Questions

1. **Mix format:** Should the `MixingSampleProvider` target 44100 Hz or honour the system's WASAPI shared-mode format (which may be 48000 Hz on some devices)? For Phase 1 we target 44100 Hz and resample sources. If this causes quality issues, we can switch to the device's native rate.

2. **`PlaybackEnded` threading:** `WasapiOut.PlaybackStopped` fires on the WASAPI callback thread. `AudioEngine.PlaybackEnded` must marshal to the UI thread (via `Application.Current.Dispatcher`) before raising. Confirm this in Phase 1 testing.
