# QA Review — Phase 1 Foundation & Audio Engine

**Branch:** `feat/phase-1-foundation-audio-engine`
**Reviewed by:** QA Engineer
**Date:** 2026-02-25
**Status:** ⛔ Changes Required — 2 blocking defects must be resolved before merge to `main`

---

## Overview

The Phase 1 implementation is well-structured and the `SongScanner` / `ChannelSettings` work is approved without reservation. Two defects in `AudioEngine.cs` are blocking and must be addressed. A third known limitation is documented for awareness.

---

## Defect 1 — `InvalidCastException` and Resource Leak for AIFF and MP4 Files

**File:** `VideoJam/Engine/AudioEngine.cs`
**Severity:** 🔴 High — Blocking
**Affects:** Any song folder containing `.aiff` or `.mp4` files

### Problem

In `Load()`, every reader returned by `CreateReader` is cast to `IDisposable` and added to `_readers`:

```csharp
ISampleProvider reader = CreateReader(channel.File);
_readers.Add((IDisposable) reader);   // ← unsafe cast
```

For `.wav` and `.mp3`, `CreateReader` returns an `AudioFileReader`, which implements both `ISampleProvider` and `IDisposable`. The cast is safe.

For `.aiff` and `.mp4`, `CreateReader` calls `.ToSampleProvider()` on the underlying `AiffFileReader` or `MediaFoundationReader`. The NAudio `.ToSampleProvider()` extension returns a lightweight wrapper type (`WaveToSampleProvider`, `Pcm16BitToSampleProvider`, etc.) that does **not** implement `IDisposable`. The cast throws `InvalidCastException` at runtime.

This produces two compounding failures:
1. `Load()` throws, and the song cannot be loaded.
2. The underlying `AiffFileReader` / `MediaFoundationReader` — created inline and immediately discarded — is never added to `_readers` and its file handle is never closed.

### Required Fix

Introduce a **private nested class** `AudioReader` inside `AudioEngine` that implements both `ISampleProvider` and `IDisposable`. It holds two references:

- `_owner` — the `IDisposable` responsible for the underlying resource (the raw file reader)
- `_source` — the `ISampleProvider` used for sample output (may be a wrapper, or the same object as `_owner`)

It delegates:
- `WaveFormat` and `Read()` → to `_source`
- `Dispose()` → to `_owner`

`CreateReader` should be updated to return `AudioReader` rather than bare `ISampleProvider`. For each file type:

| Extension | `_owner` | `_source` |
|-----------|----------|-----------|
| `.wav`, `.mp3` | `AudioFileReader` | same `AudioFileReader` |
| `.aiff` | `AiffFileReader` | `aiffReader.ToSampleProvider()` |
| `.mp4` | `MediaFoundationReader` | `mfReader.ToSampleProvider()` |

In `Load()`, because `AudioReader` is statically known to be `IDisposable`, the unsafe cast is eliminated entirely:

```csharp
AudioReader reader = CreateReader(channel.File);
_readers.Add(reader);   // safe — AudioReader : IDisposable
```

### Acceptance Criteria

- Loading a folder containing an `.aiff` file does not throw.
- Loading a folder containing an `.mp4` file does not throw.
- After `Stop()`, all underlying file readers are disposed (file handles released).
- The unsafe cast `(IDisposable) reader` no longer exists anywhere in the file.

---

## Defect 2 — Mono Audio Files Cause `MixingSampleProvider` to Throw

**File:** `VideoJam/Engine/AudioEngine.cs`
**Severity:** 🔴 High — Blocking
**Affects:** Any song folder containing a mono audio stem

### Problem

`EnsureMixFormat` is intended to bring all sources into the common mix format (44,100 Hz, stereo, float):

```csharp
if (fmt.SampleRate == MixFormat.SampleRate && fmt.Channels == MixFormat.Channels)
    return source;

return new WdlResamplingSampleProvider(source, MixFormat.SampleRate);
```

`WdlResamplingSampleProvider` is a **sample rate converter**. It does not change channel count. If a stem is mono at 44,100 Hz, the guard condition fails (channel count differs), a resampler is created, and the output is still mono.

`MixingSampleProvider` validates that all inputs share the same format and throws `ArgumentException` if a provider with a different channel count is added. Any song folder with a mono stem will fail to load.

### Required Fix

`EnsureMixFormat` must handle sample rate conversion and channel count conversion as two **separate, sequential steps**:

1. **Resample** — if `source.WaveFormat.SampleRate` differs from `MixFormat.SampleRate`, wrap in `WdlResamplingSampleProvider`. This preserves channel count.
2. **Upmix** — if the result (after any resampling) has 1 channel and `MixFormat.Channels` is 2, wrap in `MonoToStereoSampleProvider`.

NAudio provides `MonoToStereoSampleProvider` for exactly this purpose.

The method should also guard against a stereo source being fed into a mono mix format — though this is not the expected configuration for this application, a meaningful exception is preferable to a silent corruption.

### Acceptance Criteria

- Loading a folder containing a mono `.wav` stem does not throw.
- The mono stem is audible and correctly upmixed to stereo in the output.
- Loading a folder containing stereo stems continues to work as before.
- `EnsureMixFormat` no longer uses `WdlResamplingSampleProvider` as a proxy for channel count correction.

---

## Known Limitation 3 — `Muted` Property Not Applied (Deferred, Phase 1 Accepted)

**File:** `VideoJam/Engine/AudioEngine.cs`
**Severity:** 🟡 Medium — Not blocking for Phase 1
**Affects:** MP4 video audio channels during manual testing

`Load()` reads `ChannelSettings.Level` but does not apply `ChannelSettings.Muted`. The `MainWindow` harness correctly sets `Muted = true` for `VideoAudio` channels, but these will be audible during Phase 1 manual testing because the mute has no effect.

This is acknowledged in the code via a comment and is acceptable as a Phase 1 deferral, **provided** the Developer is aware that manual audio verification sessions will include video audio in the mix. If this causes confusion during testing, a temporary workaround of setting `Level = 0` for muted channels in `MainWindow` would be acceptable until Phase 2 implements the full mute logic.

No code change required at this time. To be addressed in a later phase.

---

## Test Coverage Note

Once Defects 1 and 2 are resolved, at minimum one integration-level test should be added per supported format (`.wav`, `.mp3`, `.aiff`, `.mp4`) to confirm that `AudioEngine.Load()` constructs the pipeline without throwing. The audio device does not need to play; `Load()` can be verified structurally. This would have caught Defect 1 immediately.

This is a recommendation for the Developer's consideration — not a blocking requirement for this review cycle.

---

## Summary

| # | Severity | File | Issue |
|---|----------|------|-------|
| 1 | 🔴 Blocking | `AudioEngine.cs` | `InvalidCastException` + resource leak for AIFF/MP4; fix via `AudioReader` wrapper class |
| 2 | 🔴 Blocking | `AudioEngine.cs` | Mono files not upmixed; `MixingSampleProvider` throws; fix via `MonoToStereoSampleProvider` |
| 3 | 🟡 Deferred | `AudioEngine.cs` | `Muted` property not applied; video audio audible in Phase 1 testing; accepted deferral |

Please address Defects 1 and 2 and return for a second review before merging.
