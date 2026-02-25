## 1. Solution Setup

- [x] 1.1 Create `VideoJam.sln` at the repository root and add `VideoJam/VideoJam.csproj` (WPF Application, .NET 10, win-x64) and `VideoJam.Tests/VideoJam.Tests.csproj` (xUnit Test Project, .NET 10)
- [x] 1.2 Create `Directory.Build.props` at the repository root pinning all package versions: NAudio 2.2.x, LibVLCSharp 3.x, VideoLAN.LibVLC.Windows 3.x, Microsoft.Extensions.Logging 8.x, xUnit 2.x, xunit.runner.visualstudio 2.x, Microsoft.NET.Test.Sdk
- [x] 1.3 Add NuGet package references to `VideoJam.csproj`: NAudio, LibVLCSharp, VideoLAN.LibVLC.Windows, Microsoft.Extensions.Logging (version-less — resolved from Directory.Build.props)
- [x] 1.4 Add NuGet package references to `VideoJam.Tests.csproj`: xUnit, xunit.runner.visualstudio, Microsoft.NET.Test.Sdk; add a project reference to `VideoJam`
- [x] 1.5 Configure `VideoJam.csproj` for win-x64 self-contained publish (`<RuntimeIdentifier>win-x64</RuntimeIdentifier>`, `<SelfContained>true</SelfContained>`, `<PublishSingleFile>false</PublishSingleFile>`)
- [x] 1.6 Confirm `dotnet build VideoJam.sln` succeeds with zero errors and zero warnings

## 2. Project Structure & Stub Files

- [x] 2.1 Create source folders inside `VideoJam/`: `Engine/`, `Model/`, `Services/`, `UI/`, `Input/`
- [x] 2.2 Add stub class files in `Engine/`: `PlaybackEngine.cs`, `SyncCoordinator.cs`, `AudioEngine.cs`, `VideoEngine.cs`, `DisplayManager.cs`
- [x] 2.3 Add stub class files in `Model/`: `SongManifest.cs` (runtime records + `AudioChannelType` enum), `Show.cs`, `SongEntry.cs`, `ChannelSettings.cs`, `AppState.cs`
- [x] 2.4 Add stub class files in `Services/`: `ShowFileService.cs`, `SongScanner.cs`, `PathResolver.cs`
- [x] 2.5 Add stub class files in `UI/`: `VlcDisplayWindow.xaml` + `VlcDisplayWindow.xaml.cs`
- [x] 2.6 Add stub class file in `Input/`: `HotkeyService.cs`
- [x] 2.7 Confirm `dotnet build VideoJam.sln` still passes after all stubs are added

## 3. Model Implementation

- [x] 3.1 Implement `AudioChannelType` enum (`Stem`, `VideoAudio`) in `Model/SongManifest.cs`
- [x] 3.2 Implement `AudioChannelManifest` record (`File: FileInfo`, `ChannelId`, `Type`) in `Model/SongManifest.cs`
- [x] 3.3 Implement `VideoFileManifest` record (`File: FileInfo`, `DisplayIndex`, `Suffix`) in `Model/SongManifest.cs`
- [x] 3.4 Implement `SongManifest` record (`SongName`, `Folder: DirectoryInfo`, `AudioChannels`, `VideoFiles`) in `Model/SongManifest.cs`
- [x] 3.5 Implement `ChannelSettings` class in `Model/ChannelSettings.cs`: `float Level { get; set; } = 1.0f`, `bool Muted { get; set; } = false`; add XML doc comments
- [x] 3.6 Implement `Show` class in `Model/Show.cs`: `int Version = 1`, `List<SongEntry> Songs`, `Dictionary<string, int> GlobalDisplayRouting`, `Dictionary<int, string> FallbackImages`; add XML doc comments
- [x] 3.7 Implement `SongEntry` class in `Model/SongEntry.cs`: `string FolderPath`, `string Name`, `Dictionary<string, int> DisplayRoutingOverrides`, `Dictionary<string, ChannelSettings> Channels` (initialised to empty dict in constructor); add XML doc comments

## 4. SongScanner Implementation

- [x] 4.1 Implement `SongScanner.Scan(DirectoryInfo folder)` returning `SongManifest`:
  - Set `SongName` to `folder.Name` and `Folder` to the `DirectoryInfo` passed in
  - Enumerate files via `folder.EnumerateFiles()` (non-recursive), yielding `FileInfo` instances
  - For `.wav` / `.mp3` / `.aiff` (case-insensitive, via `file.Extension`): add an `AudioChannelManifest` with `Type = Stem`, `File = fileInfo`, and `ChannelId = file.Name`
  - For `.mp4` (case-insensitive): add both a `VideoFileManifest` with `File = fileInfo` and suffix extracted from the last underscore-prefixed segment before the extension (or empty string if none), and an `AudioChannelManifest` with `Type = VideoAudio`, `File = fileInfo`, and `ChannelId = "{file.Name}:audio"`
  - All other extensions: silently ignored
- [x] 4.2 Add XML doc comments to `SongScanner` and its public `Scan` method

## 5. SongScanner Unit Tests

- [x] 5.1 Test: empty folder → manifest with zero AudioChannels and zero VideoFiles (no exception)
- [x] 5.2 Test: folder with `drums.wav`, `bass.mp3`, `keys.aiff` → three Stem channels with correct ChannelIds
- [x] 5.3 Test: folder with `show_lyrics.mp4` → one VideoFileManifest (Suffix = `"_lyrics"`) and one VideoAudio channel (ChannelId = `"show_lyrics.mp4:audio"`)
- [x] 5.4 Test: folder with `performance.mp4` (no suffix) → VideoFileManifest with empty Suffix
- [x] 5.5 Test: folder with `notes.txt`, `cover.png`, `readme.pdf` → empty manifest (all ignored)
- [x] 5.6 Test: folder with `DRUMS.WAV` and `Bass.Mp3` → both classified as Stem (case-insensitive extension matching)
- [x] 5.7 Test: `SongManifest.SongName` equals `folder.Name`; `SongManifest.Folder.FullName` equals the `DirectoryInfo.FullName` passed to `Scan`

## 6. ChannelSettings Unit Tests

- [x] 6.1 Test: `new ChannelSettings()` has `Level == 1.0f` and `Muted == false`
- [x] 6.2 Test: setting `Level = 0.5f` and reading it back returns `0.5f`
- [x] 6.3 Test: setting `Muted = true` and reading it back returns `true`

## 7. AudioEngine Implementation

- [x] 7.1 Implement `AudioEngine.Load(SongManifest manifest, Dictionary<string, ChannelSettings> channelSettings)`:
  - For each `AudioChannelManifest`, select the reader by `channel.File.Extension` (case-insensitive): `AudioFileReader` for .wav/.mp3, `AiffFileReader` for .aiff, `MediaFoundationReader` for .mp4; pass `channel.File.FullName` as the path argument to the reader constructor
  - Wrap each reader in a `VolumeSampleProvider` using `channelSettings[channelId].Level` (or `1.0f` if not present)
  - Resample each provider to 44100 Hz / 32-bit float / stereo if the source format differs (use `WdlResamplingSampleProvider` or `MediaFoundationResampler`)
  - Compose all providers into a single `MixingSampleProvider`
  - Initialise `WasapiOut` in shared mode with 50ms latency and call `WasapiOut.Init(mixingProvider)`
  - Store a `bool _stoppedExplicitly` flag, initialised to `false`
- [x] 7.2 Implement `AudioEngine.Play()` → `long`:
  - Call `WasapiOut.Play()`
  - Capture and return `Stopwatch.GetTimestamp()`
- [x] 7.3 Implement `AudioEngine.Stop()`:
  - Set `_stoppedExplicitly = true`
  - Call `WasapiOut.Stop()`
  - Dispose all readers and the `WasapiOut` instance
  - Null out all fields and reset state
  - Guard against being called before `Load()` or after a prior `Stop()` (no exception)
- [x] 7.4 Implement `AudioEngine.PlaybackEnded` event:
  - Subscribe to `WasapiOut.PlaybackStopped`
  - In the handler: if `_stoppedExplicitly` is false, marshal to the UI thread via `Application.Current.Dispatcher.InvokeAsync` and raise `PlaybackEnded`
- [x] 7.5 Implement `AudioEngine.Dispose()` — calls `Stop()` if not already stopped
- [x] 7.6 Add XML doc comments to all public members of `AudioEngine`

## 8. Phase 1 Integration Harness

- [x] 8.1 Implement `MainWindow.xaml` with a simple layout:
  - A **Load** button (opens `OpenFolderDialog` — native WPF, .NET 8+)
  - A **Play** button (disabled until a folder is loaded)
  - A **Stop** button
  - A status `TextBlock` showing current state (`Idle` / `Loaded` / `Playing` / `Stopped`)
- [x] 8.2 Wire `MainWindow.xaml.cs` code-behind:
  - Load: wrap the folder browser's selected path string in `new DirectoryInfo(selectedPath)`, call `SongScanner.Scan(directoryInfo)` then `AudioEngine.Load()` with default `ChannelSettings`; update status; enable Play
  - Play: call `AudioEngine.Play()`; update status
  - Stop: call `AudioEngine.Stop()`; update status
  - Handle `AudioEngine.PlaybackEnded` to update status to `Stopped` and re-enable Load
- [ ] 8.3 Manually verify with the operator's known-good test set:
  - Three or more WAV stems play simultaneously with no audible phase offset
  - A stem with `Level = 0.5f` is audibly quieter than one at `1.0f`
  - Playback stops cleanly when all stems reach their end
  - No WASAPI errors or audio glitches
- [ ] 8.4 Manually verify MP4 audio extraction: load a song folder containing an `.mp4` file; confirm the audio track is audible through NAudio with no VLC involvement

## 9. Final Verification

- [x] 9.1 Run `dotnet test VideoJam.Tests` — all unit tests pass
- [x] 9.2 Run `dotnet build VideoJam.sln --configuration Release` — zero errors, zero warnings
- [x] 9.3 Confirm `dotnet publish` with win-x64 self-contained parameters produces a valid output folder containing `VideoJam.exe`
- [x] 9.4 Create git branch `feat/phase-1-foundation-audio-engine` and commit all Phase 1 work with a clear commit message
