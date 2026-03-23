## Why

Phases 1–5 have built a fully-functional operator UI and all underlying engine subsystems (audio, video, sync, display management, show persistence), but there is no code that ties them together for a live performance. The app currently cannot start playback, advance songs, or respond to button presses. Phase 6 wires everything into an end-to-end workflow so a musician can load a `.show` file, press a key, and run a full multi-song set.

## What Changes

- **New:** `PlaybackEngine` — a four-state state machine (`Idle → Cued → Playing → Paused`) that orchestrates `AudioEngine`, `VideoEngine`, `SyncCoordinator`, and `DisplayManager` for each song in the setlist
- **New:** `HotkeyService` — a global `WH_KEYBOARD_LL` low-level keyboard hook that captures Button A (Space) and Button B (Escape) regardless of window focus
- **Modified:** `MainViewModel` — wires `HotkeyService` events to `PlaybackEngine` operations and reflects engine state in all bound UI properties
- **Modified:** `operator-shell` — `MainWindow` is hidden when playback begins on Display 0 and restored when the song ends or is rewound; `StatusText` updated for new playback states
- **Modified:** `setlist-panel` — click-to-cue is routed through `PlaybackEngine.Cue()` rather than updating `SelectedSong` directly
- Key bindings are configurable via `appsettings.json` in the app directory; defaults are `Space` → Button A, `Escape` → Button B

## Non-Goals for This Phase

- Graceful error handling for missing files or corrupted audio (Phase 7)
- Structured logging infrastructure (Phase 7)
- Display disconnect recovery (Phase 7)
- Any UI polish beyond what is needed for correct behaviour

## Capabilities

### New Capabilities

- `playback-engine`: Four-state state machine (`Idle`, `Cued`, `Playing`, `Paused`) that orchestrates all engine subsystems for a live performance workflow, including pre-loading on cue, fire-and-forget playback, auto-advance, and display 0 takeover
- `hotkey-service`: Global low-level keyboard hook (`WH_KEYBOARD_LL`) that raises `ButtonAPressed` and `ButtonBPressed` events regardless of application focus; key bindings configurable via `appsettings.json`

### Modified Capabilities

- `operator-shell`: `MainWindow` visibility is managed by `PlaybackEngine` during playback (hidden on GO, restored on song end or rewind); `StatusText` extended with playback-state messages
- `setlist-panel`: Click-to-cue routes through `PlaybackEngine.Cue(songIndex)` rather than directly updating `SelectedSong`; drag-drop reordering now also disabled in `Paused` state

## Impact

- **New source files:** `VideoJam/Engine/PlaybackEngine.cs`, `VideoJam/Input/HotkeyService.cs`, `VideoJam/Input/HotkeySettings.cs`
- **Modified source files:** `VideoJam/UI/ViewModels/MainViewModel.cs`, `VideoJam/UI/MainWindow.xaml.cs`, `VideoJam/App.xaml.cs` (wiring)
- **New config file:** `appsettings.json` (key binding configuration, deployed to app directory)
- **No new NuGet packages** — `WH_KEYBOARD_LL` is P/Invoke against `user32.dll`
- **Primary technical risk:** Global keyboard hooks in WPF require a message loop on the hook thread; if `HotkeyService` is constructed off the UI thread or the hook is installed incorrectly, it will silently fail to capture keys — particularly on machines running third-party keyboard interceptors or accessibility software
