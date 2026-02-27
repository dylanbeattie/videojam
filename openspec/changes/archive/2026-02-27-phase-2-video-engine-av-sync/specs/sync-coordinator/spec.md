## ADDED Requirements

### Requirement: SyncCoordinator fires AudioEngine then VideoEngine in tight sequence

`SyncCoordinator.Start(AudioEngine audio, VideoEngine video)` SHALL:
1. Call `audio.Play()` and capture the returned `long` timestamp (`t_start`).
2. Immediately call `video.Play(t_start)` with no deliberate delay between the two calls.
3. Return after `video.Play()` returns.

The total time between `audio.Play()` returning and `video.Play()` being dispatched SHALL be under 1 ms on any supported machine (measured end-to-end, not including VLC's internal decode scheduling).

#### Scenario: Audio fires before video
- **WHEN** `SyncCoordinator.Start(audio, video)` is called
- **THEN** `audio.Play()` is called first and `video.Play(timestamp)` is called immediately after with the timestamp returned by `audio.Play()`

#### Scenario: Start completes without blocking
- **WHEN** `SyncCoordinator.Start()` is called
- **THEN** the method returns after `video.Play()` returns and does not block the calling thread waiting for playback to complete

---

### Requirement: SyncCoordinator logs the measured audio-to-video dispatch interval

After `video.Play(t_start)` returns, `SyncCoordinator` SHALL compute the interval between `t_start` and the timestamp captured immediately after `video.Play()` returns, and log it at `Debug` level with the label `"A/V dispatch Δt"` and the value in milliseconds.

#### Scenario: Δt is logged after Start
- **WHEN** `SyncCoordinator.Start()` completes successfully
- **THEN** a `Debug`-level log entry is written containing the measured interval in milliseconds

---

### Requirement: SyncCoordinator is stateless between invocations

`SyncCoordinator` SHALL hold no state between `Start()` calls. It SHALL NOT track which song is playing, maintain timers, or subscribe to events from `AudioEngine` or `VideoEngine`. It is safe to call `Start()` multiple times on the same instance.

#### Scenario: Start can be called for consecutive songs
- **WHEN** `SyncCoordinator.Start()` is called for a first song, that song ends, and `Start()` is called again for a second song
- **THEN** both calls complete correctly and independently with no state leaking between them
