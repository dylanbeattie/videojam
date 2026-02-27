# Phase 2 Review Findings — Required Fixes

> Prepared by QA following review of `phase-2-video-engine-av-sync`.
> Three items are **CRITICAL** (must fix before archive), four are **WARNING** (should fix),
> two are **SUGGESTION** (nice to have).
>
> A subsequent pass identified **magic numbers** across the full codebase — see the
> dedicated section at the end of this file.

---

## CRITICAL

### 1. Resource leak on natural-end replay path

**File:** `VideoJam/UI/MainWindow.xaml.cs` — `OnPlaybackEnded` and `OnPlayClicked`

When playback ends naturally, `OnPlaybackEnded` calls `_videoEngine?.Stop()` but **never
disposes** the `VideoEngine` (and therefore never disposes its `LibVLC` native instance).
It also neither unsubscribes from nor disposes `_audioEngine`. The Play button is then
re-enabled, and when the user presses Play again, `OnPlayClicked` silently overwrites both
fields with fresh instances. The previous `VideoEngine._libVlc` and `AudioEngine` are
abandoned without disposal — native VLC threads and WASAPI handles are leaked.

`CleanupAll()` handles this correctly, but `OnPlaybackEnded` does not call it (rightly so,
as `CleanupAll()` also clears `_manifest`, which would prevent replay).

**Required fix:** At the top of `OnPlayClicked`, explicitly dispose any stale engines before
constructing new ones. Something along these lines:

```csharp
// Dispose stale engines from a previous natural-end cycle, if any.
if (_audioEngine is not null) {
    _audioEngine.PlaybackEnded -= OnPlaybackEnded;
    _audioEngine.Dispose();
    _audioEngine = null;
}
_videoEngine?.Dispose();
_videoEngine = null;
CloseDisplayWindow();
```

This preserves `_manifest` and `_videoFilePath` for replay while ensuring every engine
generation is cleaned up before the next one is created.

---

## WARNING

### 2. Pre-buffer timeout falls through to `ShowVideo()` and slot registration

**File:** `VideoJam/Engine/VideoEngine.cs` — `LoadAsync`, lines ~131–151

The spec (*"Pre-buffer times out"* scenario) states: *"log a warning and **return**"*.
The implementation logs the warning, but then unconditionally continues to
`player.Time = 0`, `window.ShowVideo()`, and `_slots.Add(new ActiveSlot(...))`.
A timed-out player has no confirmed decoded frame — calling `ShowVideo()` will display
whatever VLC happens to have rendered (likely nothing), yet the slot is still registered
for `Play()`.

The design doc does say "allow a degraded start", which is in tension with the spec.
A decision is needed:

- **Option A** — follow the spec: after logging the warning, dispose the player and
  `return` early (no slot added, window stays in fallback).
- **Option B** — follow the design: keep the current fall-through, but add an explicit
  comment stating this is intentional degraded-start behaviour, and update the spec
  scenario to match.

Either is acceptable; the current silent ambiguity is not.

---

### 3. Duplicate "A/V dispatch Δt" log entries on every playback

**Files:** `VideoJam/Engine/VideoEngine.cs:179`, `VideoJam/Engine/SyncCoordinator.cs:58`

`VideoEngine.Play()` emits:

```
A/V dispatch Δt: {DeltaMs:F2} ms (audio start → last video Play() return).
```

`SyncCoordinator.Start()` also emits a line using the same `AvDispatchLabel` constant.
Every playback therefore produces **two** Δt entries from two different classes,
measuring slightly different intervals under the same label. This is confusing in the log.

**Required fix:** Remove the Δt log from `VideoEngine.Play()` entirely. The single
authoritative measurement belongs to `SyncCoordinator`, which is the class the spec
assigns it to. `VideoEngine.Play()` may retain a simpler debug log (e.g.
`"Dispatched {n} MediaPlayer(s)."`) if useful.

---

### 4. DPI scale sourced from MainWindow's monitor, not the primary display

**File:** `VideoJam/UI/MainWindow.xaml.cs` — `CreatePrimaryDisplayWindow`, lines ~183–186

```csharp
var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
double dpiScaleX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
```

`this` is the `MainWindow` harness. If the harness window sits on a secondary monitor
at a different DPI than the primary display, `VlcDisplayWindow` will be sized and
positioned with the wrong scale factor — failing the *"Window covers primary display
at 150% DPI"* spec scenario.

**Required fix:** Obtain the DPI of `Screen.PrimaryScreen` directly rather than inferring
it from the harness window. One reliable approach under PerMonitorV2 is to use
`Graphics.FromHdc` against the primary screen's device context, or to query the WPF
per-monitor DPI source tied to `Screen.PrimaryScreen`'s bounds rather than `this`.

---

### 5. `LoadAsync` method name diverges from spec and task definitions

**File:** `VideoJam/Engine/VideoEngine.cs:88`

The spec (`video-engine/spec.md`) and tasks 3.3–3.5 specify `VideoEngine.Load(...)`.
The implementation provides `VideoEngine.LoadAsync(...)`. The renaming is reasonable
given the `await` inside, but it is an undocumented deviation.

**Required fix (choose one):**
- Update the spec and task descriptions to reflect `LoadAsync`; or
- Rename the method back to `Load` and make it return `Task` (the `Async` suffix on a
  method returning `Task` is conventional but not mandatory).

Either way, the spec and implementation should agree.

---

## SUGGESTION

### 6. `_slots` list is not thread-safe

**File:** `VideoJam/Engine/VideoEngine.cs:51`

`_slots` is a plain `List<ActiveSlot>`. `LoadAsync` appends to it from an async
continuation (potentially a thread-pool thread); `Stop()` and `Play()` iterate it.
Phase 2 is single-display so the race never materialises in practice, but Phase 3
multi-display loading will call `LoadAsync` concurrently and writes will conflict.

Replacing `List<T>` with a `lock`-guarded list, `ConcurrentBag<T>`, or an
`ImmutableList<T>` swap pattern now costs nothing and prevents a future defect.

---

### 7. No automated tests for Phase 2

All Phase 2 coverage is manual (tasks 2.4, 5.7, 5.8 — all three still incomplete).
`SyncCoordinator` is pure logic with no I/O and is trivially unit-testable.
`VideoEngine`'s pre-buffer and disposal ordering could be verified against a mock
`IMediaPlayer` abstraction.

Even a small number of unit tests for `SyncCoordinator.Start()` would give the
statelessness requirement and Δt-logging behaviour a permanent automated home that
does not depend on a human running a test set.

---

## What Is Fine — Do Not Change

The following were inspected and are correct; no action required:

- `VideoEngine.Dispose()` disposes `MediaPlayer` instances **before** `LibVLC` ✓
- `using var media = new Media(...)` then `player.Media = media` — correct LibVLCSharp
  ownership pattern; managed wrapper may be disposed while native refcount keeps the
  media alive ✓
- `player.Paused` event cleanup in the `finally` block ✓
- `window.Dispatcher.Invoke(...)` calls in `VideoEngine` — correct UI-thread marshalling ✓
- `SyncCoordinator` holds exactly one field; stateless between calls ✓
- `app.manifest` PerMonitorV2 declaration is correctly formed ✓
- `_disposed` guard and disposal ordering in `VideoEngine` ✓
- XML doc comments — thorough and accurate throughout ✓

---

---

## Magic Numbers — Full Codebase Audit

> A full scan of all non-generated source files was performed.
> Items marked **Extract** are clear violations. Items marked **Consider** are borderline
> but worth reviewing.

---

### Extract — `AudioEngine.cs:31` — sample rate and channel count

```csharp
private static readonly WaveFormat MixFormat = WaveFormat.CreateIeeeFloatWaveFormat(44_100, 2);
```

Both `44_100` (sample rate) and `2` (stereo channel count) are anonymous literals passed
directly into a factory-method call. The field comment explains them in prose, but prose
is not a compiler-enforced name. If either value is changed in the future, there is no
single constant to update — and no guarantee the comparisons further down in
`EnsureMixFormat` (see below) are updated to match.

**Suggested constants:**
```csharp
private const int MixSampleRate    = 44_100;
private const int MixChannelCount  = 2;

private static readonly WaveFormat MixFormat =
    WaveFormat.CreateIeeeFloatWaveFormat(MixSampleRate, MixChannelCount);
```

---

### Extract — `AudioEngine.cs:86` — default full-volume fallback

```csharp
float level = channelSettings.TryGetValue(channel.ChannelId, out ChannelSettings? settings)
    ? settings.Level
    : 1.0f;   // ← magic number
```

`1.0f` here means "full volume / unity gain". It also happens to match the default
initialiser for `ChannelSettings.Level` (see `ChannelSettings.cs:11`). That agreement is
coincidental — if one ever diverges from the other, the mismatch will be silent.

**Suggested constant:**
```csharp
private const float DefaultChannelLevel = 1.0f;
```

---

### Extract — `VideoEngine.cs:176` and `SyncCoordinator.cs:56` — `1000.0` duplicated

```csharp
// VideoEngine.cs:176
double deltaMs = (tEnd - audioStartTimestamp) * 1000.0 / Stopwatch.Frequency;

// SyncCoordinator.cs:56
double deltaMs = (tEnd - tStart) * 1000.0 / Stopwatch.Frequency;
```

The same ticks-to-milliseconds factor `1000.0` appears in two separate files. If the
calculation ever changes (e.g. to nanoseconds for a future precision requirement), it
must be updated in two places. Given the magic-number issue in `VideoEngine.Play()` is
also being removed per Warning #3 above, this duplication disappears — but for
completeness, either file that retains the calculation should use a named constant.

**Suggested constant (in whichever file retains the calculation):**
```csharp
private const double MillisecondsPerSecond = 1_000.0;
```

---

### Extract — `SongScanner.cs:41` and `MainWindow.xaml.cs:114,118,127` — primary display index `0`

The literal `0` meaning "primary display" appears in **four places** across two files:

| Location | Usage |
|---|---|
| `SongScanner.cs:41` | `DisplayIndex: 0` in `VideoFileManifest` constructor |
| `MainWindow.xaml.cs:114` | `v.DisplayIndex == 0` comparison |
| `MainWindow.xaml.cs:118` | `DisplayIndex: 0` in `VideoFileManifest` constructor |
| `MainWindow.xaml.cs:127` | `displayIndex: 0` in `LoadAsync` call |

None of these occurrences indicate *why* 0 is the primary display — that is knowledge
carried only in the developer's head and the Phase 3 backlog.

**Suggested constant (local to each file until `DisplayManager` is implemented in Phase 3,
at which point it should be promoted to a shared location there):**
```csharp
// Phase 3 note: promote to DisplayManager.PrimaryDisplayIndex when that class is implemented.
private const int PrimaryDisplayIndex = 0;
```

---

### Consider — `AudioEngine.cs:190–191` — mono and stereo channel counts in comparisons

```csharp
if (result.WaveFormat.Channels == 1 && MixFormat.Channels == 2)
    result = new MonoToStereoSampleProvider(result);

if (result.WaveFormat.Channels > MixFormat.Channels)
```

The `1` is an anonymous literal for "mono". The `2` is already implicit in
`MixFormat.Channels` — so that half becomes self-documenting once `MixChannelCount` is
extracted per the suggestion above (the comparison could become
`MixFormat.Channels == MixChannelCount`, though that is tautological and Bryan may
reasonably leave this half alone).

The `1` for mono, however, has no named representation anywhere. It is worth extracting:
```csharp
private const int MonoChannelCount = 1;
```

---

### Consider — `Show.cs:9` — schema version

```csharp
public int Version { get; set; } = 1;
```

The `1` is the current `.show` file schema version. When Phase 4 implements
`ShowFileService`, any migration logic will need to know what version numbers are valid.
An anonymous literal `1` in a property initialiser is easy to miss.

**Suggested constant:**
```csharp
private const int CurrentSchemaVersion = 1;

public int Version { get; set; } = CurrentSchemaVersion;
```

---

### Consider — `MainWindow.xaml` — harness window dimensions

```xml
Height="300" Width="480"
```

These are the harness window's fixed dimensions. As a temporary development harness
(explicitly noted as such in the code-behind), hard-coded XAML dimensions are tolerable.
No action required before archive, but worth a tidy when the harness is eventually
replaced by the Phase 5 operator UI.
