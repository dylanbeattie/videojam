## 1. Foundations

- [x] 1.1 Create `VideoJam/UI/ViewModels/RelayCommand.cs` — non-generic `RelayCommand` and generic `RelayCommand<T>` implementing `ICommand` with optional `canExecute` predicate
- [x] 1.2 Create `IDialogService` interface in `VideoJam/UI/` with `PickFolder`, `PickOpenFile`, `PickSaveFile`, and `Confirm` methods
- [x] 1.3 Create `WpfDialogService` production implementation using `FolderBrowserDialog`, `OpenFileDialog`, `SaveFileDialog`, and `MessageBox`
- [x] 1.4 Wire `MainViewModel` and `WpfDialogService` into `App.xaml.cs` startup — set `MainWindow.DataContext`

## 2. MainViewModel

- [x] 2.1 Create `VideoJam/UI/ViewModels/MainViewModel.cs` implementing `INotifyPropertyChanged`
- [x] 2.2 Add properties: `ObservableCollection<SongEntry> Songs`, `SongEntry? SelectedSong`, `PlaybackState PlaybackState`, `bool HasUnsavedChanges`, `string StatusText`, `Show? LoadedShow`
- [x] 2.3 Add commands: `AddSongCommand`, `RemoveSongCommand`, `ReorderSongCommand`
- [x] 2.4 Add commands: `NewShowCommand`, `OpenShowCommand`, `SaveShowCommand`, `SaveAsShowCommand`
- [x] 2.5 Implement `StatusText` auto-update logic (Ready / Show loaded / Cued: {name}) on `SelectedSong` and `LoadedShow` changes
- [x] 2.6 Implement window title property (`WindowTitle`) following `VideoJam — {name}{*}` pattern, bound from `MainWindow`

## 3. MainWindow Shell

- [x] 3.1 Replace default `MainWindow.xaml` with two-panel `Grid` layout: setlist column (left), mixer column (right), status bar row (bottom)
- [x] 3.2 Add WPF `Menu` with File menu items: New, Open, Save (`Ctrl+S`), Save As — bound to ViewModel commands
- [x] 3.3 Bind `Window.Title` to `MainViewModel.WindowTitle`
- [x] 3.4 Implement `Window.Closing` handler in code-behind that delegates to an `OnClosingCommand` / calls `ViewModel.ConfirmClose()` for unsaved-changes guard
- [x] 3.5 Add status bar `TextBlock` at the bottom bound to `MainViewModel.StatusText`

## 4. Setlist Panel

- [x] 4.1 Implement `ListBox` (or `ItemsControl`) in the setlist column bound to `MainViewModel.Songs`
- [x] 4.2 Style each song row to show 1-based index number and song name; highlight selected item
- [x] 4.3 Bind `ListBox.SelectedItem` to `MainViewModel.SelectedSong` (two-way)
- [x] 4.4 Disable song selection click when `PlaybackState` is `Playing` or `Paused`
- [x] 4.5 Create `VideoJam/UI/Behaviours/DragDropBehaviour.cs` attached behaviour that routes drag events to `MainViewModel.ReorderSongCommand` using `ObservableCollection.Move()`
- [x] 4.6 Attach `DragDropBehaviour` to the setlist `ListBox`; disable drag-drop when `PlaybackState` is `Playing`
- [x] 4.7 Add "Add Song" button bound to `MainViewModel.AddSongCommand`
- [x] 4.8 Add per-row remove action (context menu or inline button) bound to `MainViewModel.RemoveSongCommand`

## 5. Mixer Panel

- [x] 5.1 Implement mixer `ItemsControl` in the mixer column bound to `MainViewModel.SelectedSong.Channels`
- [x] 5.2 Style each channel row: channel name `TextBlock`, `Slider` (0.0–1.0, two-way bound to `ChannelSettings.Level`), `CheckBox` (two-way bound to `ChannelSettings.Muted`)
- [x] 5.3 Bind `IsEnabled` on all mixer controls to `PlaybackState != Playing && PlaybackState != Paused`
- [x] 5.4 Apply visual distinction to video audio channel rows (channel ID ends with `:audio`) — italic text or `[V]` prefix via a value converter or data trigger
- [x] 5.5 Show placeholder text when `SelectedSong` is `null`

## 6. Show File Operations

- [x] 6.1 Implement `NewShowCommand` — unsaved-changes guard, then create blank `Show`, reset state
- [x] 6.2 Implement `OpenShowCommand` — unsaved-changes guard, file picker, `ShowFileService.Load()`, populate `Songs`, error handling
- [x] 6.3 Implement `SaveShowCommand` — delegate to Save As if no path, else `ShowFileService.Save()`, clear dirty flag, error handling
- [x] 6.4 Implement `SaveAsShowCommand` — save file picker, `ShowFileService.Save()`, update path, clear dirty flag, error handling
- [x] 6.5 Implement `HasUnsavedChanges` mutation tracking — set to `true` in `AddSong`, `RemoveOrReorderSong`, level/mute changes, routing/fallback changes
- [x] 6.6 Implement unsaved-changes guard helper used by New, Open, and Close — confirm → save → proceed; decline → proceed without save; cancel → abort

## 7. Display Routing & Fallback Image UI

- [x] 7.1 Add display routing section to the UI (collapsible expander or tab) containing an `ItemsControl` bound to `Show.GlobalDisplayRouting`
- [x] 7.2 Style each routing row: suffix `TextBox`, display index `TextBox` (integer), remove button — all two-way bound, with changes setting `HasUnsavedChanges`
- [x] 7.3 Add "Add Mapping" button that appends a blank entry to `Show.GlobalDisplayRouting`
- [x] 7.4 Add per-song override access via a small button (⚙) on each setlist row that shows/expands the song's `DisplayRoutingOverrides` in an inline area
- [x] 7.5 Enumerate connected displays and render one fallback image row per display (index, name, current path or "(none)", "Browse…" button)
- [x] 7.6 Implement "Browse…" button to open a PNG file picker, update `Show.FallbackImages[index]`, and set `HasUnsavedChanges`

## 8. Testing

- [x] 8.1 Unit test `RelayCommand` — execute, canExecute enabled/disabled scenarios
- [x] 8.2 Unit test `MainViewModel` — `HasUnsavedChanges` set by song add/remove/reorder and channel mutation
- [x] 8.3 Unit test `MainViewModel` — `StatusText` and `WindowTitle` correct values across state transitions
- [x] 8.4 Unit test `MainViewModel.ReorderSongCommand` — correct `Move()` call including adjacent-item edge case and no-op on same-index drop
- [x] 8.5 Unit test `MainViewModel` — New/Open/Save/SaveAs commands with a mock `IDialogService` and mock `ShowFileService`
- [x] 8.6 Unit test unsaved-changes guard — confirm proceeds, decline proceeds without save, cancel aborts
