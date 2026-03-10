using System.Collections.Generic;
using System.Linq;
using VideoJam.Model;
using VideoJam.UI;
using VideoJam.UI.ViewModels;

namespace VideoJam.Tests;

/// <summary>Unit tests for <see cref="MainViewModel"/>.</summary>
public sealed class MainViewModelTests : IDisposable {
	private readonly DirectoryInfo _tempDir;
	private readonly FakeDialogService _dialog;
	private readonly MainViewModel _vm;

	public MainViewModelTests() {
		_tempDir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
		_tempDir.Create();
		_dialog = new FakeDialogService();
		_vm = new MainViewModel(_dialog);
	}

	public void Dispose() => _tempDir.Delete(recursive: true);

	// ── HasUnsavedChanges via song mutations ──────────────────────────────────

	[Fact]
	public void AddSong_MarksShowDirty() {
		LoadBlankShow();
		var songDir = CreateSongDir("Song1", ["drums.wav"]);
		_dialog.FolderToReturn = songDir;

		_vm.AddSongCommand.Execute(null);

		Assert.True(_vm.HasUnsavedChanges);
	}

	[Fact]
	public void RemoveSong_MarksShowDirty() {
		LoadBlankShow();
		var entry = AddSongManually("TestSong");

		_vm.RemoveSongCommand.Execute(entry);

		Assert.True(_vm.HasUnsavedChanges);
	}

	[Fact]
	public void ReorderSong_MarksShowDirty() {
		LoadBlankShow();
		AddSongManually("A");
		AddSongManually("B");
		// Reset dirty flag that AddSong set
		ExecuteNewShow(); // clears, then re-add
		LoadBlankShow();
		AddSongManually("A");
		AddSongManually("B");
		// Now explicitly clear dirty by "simulating" a save (replace HasUnsavedChanges tracking
		// by just checking the reorder causes dirty)
		_vm.ReorderSongCommand.Execute((0, 1));

		Assert.True(_vm.HasUnsavedChanges);
	}

	[Fact]
	public void ChannelLevelChange_MarksShowDirty() {
		LoadBlankShow();
		var songDir = CreateSongDir("Song1", ["drums.wav"]);
		_dialog.FolderToReturn = songDir;
		_vm.AddSongCommand.Execute(null);
		_vm.SelectedSong = _vm.Songs.First();

		// Simulate level slider change via ChannelSettingsViewModel
		var channelVm = _vm.SelectedChannels.FirstOrDefault();
		Assert.NotNull(channelVm);

		// Reset dirty flag first (save)
		SaveCurrentShow();
		Assert.False(_vm.HasUnsavedChanges);

		channelVm!.Level = 0.5f;

		Assert.True(_vm.HasUnsavedChanges);
	}

	[Fact]
	public void ChannelMuteChange_MarksShowDirty() {
		LoadBlankShow();
		var songDir = CreateSongDir("Song1", ["keys.wav"]);
		_dialog.FolderToReturn = songDir;
		_vm.AddSongCommand.Execute(null);
		_vm.SelectedSong = _vm.Songs.First();

		var channelVm = _vm.SelectedChannels.FirstOrDefault();
		Assert.NotNull(channelVm);

		SaveCurrentShow();
		Assert.False(_vm.HasUnsavedChanges);

		channelVm!.Muted = !channelVm.Muted;

		Assert.True(_vm.HasUnsavedChanges);
	}

	// ── StatusText ────────────────────────────────────────────────────────────

	[Fact]
	public void StatusText_IsReady_WhenNoShowLoaded() {
		Assert.Equal("Ready", _vm.StatusText);
	}

	[Fact]
	public void StatusText_IsShowLoaded_WhenShowOpenButNoSongSelected() {
		LoadBlankShow();

		Assert.Equal("Show loaded — select a song to cue", _vm.StatusText);
	}

	[Fact]
	public void StatusText_IsCuedWithName_WhenSongSelected() {
		LoadBlankShow();
		var entry = AddSongManually("Opener");
		_vm.SelectedSong = entry;

		Assert.Equal("Cued: Opener", _vm.StatusText);
	}

	// ── WindowTitle ───────────────────────────────────────────────────────────

	[Fact]
	public void WindowTitle_IsNoShow_WhenNoShowLoaded() {
		Assert.Equal("VideoJam — (no show)", _vm.WindowTitle);
	}

	[Fact]
	public void WindowTitle_IsUnsaved_WhenShowNeverSaved() {
		LoadBlankShow();

		Assert.Equal("VideoJam — (unsaved)", _vm.WindowTitle);
	}

	[Fact]
	public void WindowTitle_ShowsFileName_WhenShowSaved() {
		LoadBlankShow();
		var showPath = Path.Combine(_tempDir.FullName, "MySet.show");
		_dialog.SavePathToReturn = showPath;
		_vm.SaveAsShowCommand.Execute(null);

		Assert.Equal("VideoJam — MySet", _vm.WindowTitle);
	}

	[Fact]
	public void WindowTitle_HasAsterisk_WhenDirty() {
		LoadBlankShow();
		var showPath = Path.Combine(_tempDir.FullName, "MySet.show");
		_dialog.SavePathToReturn = showPath;
		_vm.SaveAsShowCommand.Execute(null);
		Assert.False(_vm.HasUnsavedChanges); // baseline

		AddSongViaCommand("SomeSong");

		Assert.Contains("*", _vm.WindowTitle);
	}

	[Fact]
	public void WindowTitle_NoAsterisk_AfterSave() {
		LoadBlankShow();
		AddSongViaCommand("SomeSong");
		Assert.True(_vm.HasUnsavedChanges);

		SaveCurrentShow();

		Assert.DoesNotContain("*", _vm.WindowTitle);
	}

	// ── ReorderSongCommand ────────────────────────────────────────────────────

	[Fact]
	public void ReorderSong_MovesItemCorrectly() {
		LoadBlankShow();
		AddSongManually("A");
		AddSongManually("B");
		AddSongManually("C");

		_vm.ReorderSongCommand.Execute((0, 2));

		Assert.Equal("B", _vm.Songs[0].Name);
		Assert.Equal("C", _vm.Songs[1].Name);
		Assert.Equal("A", _vm.Songs[2].Name);
	}

	[Fact]
	public void ReorderSong_SameIndex_IsNoOp() {
		LoadBlankShow();
		AddSongManually("X");
		AddSongManually("Y");

		_vm.ReorderSongCommand.Execute((0, 0));

		Assert.Equal("X", _vm.Songs[0].Name);
		Assert.Equal("Y", _vm.Songs[1].Name);
	}

	[Fact]
	public void ReorderSong_AdjacentItems_NoIndexShiftBug() {
		LoadBlankShow();
		AddSongManually("First");
		AddSongManually("Second");

		_vm.ReorderSongCommand.Execute((0, 1));

		Assert.Equal("Second", _vm.Songs[0].Name);
		Assert.Equal("First", _vm.Songs[1].Name);
	}

	// ── NewShow command ───────────────────────────────────────────────────────

	[Fact]
	public void NewShow_ClearsSongs_AndResetsDirtyFlag() {
		LoadBlankShow();
		AddSongManually("ToBeCleared");

		_dialog.Confirm3Result = false; // decline save
		_vm.NewShowCommand.Execute(null);

		Assert.Empty(_vm.Songs);
		Assert.False(_vm.HasUnsavedChanges);
	}

	[Fact]
	public void NewShow_WithUnsavedChanges_ShowsGuardDialog() {
		LoadBlankShow();
		AddSongViaCommand("DirtyEntry");
		Assert.True(_vm.HasUnsavedChanges);

		_dialog.Confirm3Result = false; // decline save, proceed
		_vm.NewShowCommand.Execute(null);

		Assert.True(_dialog.Confirm3WasCalled);
	}

	[Fact]
	public void NewShow_WithoutUnsavedChanges_DoesNotShowGuardDialog() {
		ExecuteNewShow();
		_dialog.ResetCallTracking();

		_vm.NewShowCommand.Execute(null);

		Assert.False(_dialog.Confirm3WasCalled);
	}

	// ── Open command ──────────────────────────────────────────────────────────

	[Fact]
	public void OpenShow_LoadsShowFromFile() {
		// Save a show with one song to disk first.
		LoadBlankShow();
		AddSongManually("LoadMe");
		var showPath = Path.Combine(_tempDir.FullName, "TestOpen.show");
		_dialog.SavePathToReturn = showPath;
		_vm.SaveAsShowCommand.Execute(null);

		// Now open a fresh VM and open the same file.
		var vm2 = new MainViewModel(_dialog);
		_dialog.OpenPathToReturn = showPath;
		_dialog.Confirm3Result = false;
		vm2.OpenShowCommand.Execute(null);

		Assert.Single(vm2.Songs);
		Assert.Equal("LoadMe", vm2.Songs[0].Name);
		Assert.False(vm2.HasUnsavedChanges);
	}

	[Fact]
	public void OpenShow_Cancelled_LeavesCurrentShowUnchanged() {
		LoadBlankShow();
		AddSongManually("ExistingSong");
		_dialog.OpenPathToReturn = null; // simulate cancel

		_vm.OpenShowCommand.Execute(null);

		Assert.Single(_vm.Songs);
	}

	// ── Save command ──────────────────────────────────────────────────────────

	[Fact]
	public void SaveShow_ClearsDirtyFlag() {
		LoadBlankShow();
		AddSongViaCommand("Saved");
		Assert.True(_vm.HasUnsavedChanges);

		SaveCurrentShow();

		Assert.False(_vm.HasUnsavedChanges);
	}

	[Fact]
	public void SaveShow_WithNoPath_DelegatesToSaveAs() {
		LoadBlankShow();
		var path = Path.Combine(_tempDir.FullName, "Delegate.show");
		_dialog.SavePathToReturn = path;

		_vm.SaveShowCommand.Execute(null); // no existing path — should open Save As dialog

		Assert.True(_dialog.SavePickerWasCalled);
		Assert.False(_vm.HasUnsavedChanges);
	}

	// ── SaveAs command ────────────────────────────────────────────────────────

	[Fact]
	public void SaveAsShow_WritesFile_AndClearsDirtyFlag() {
		LoadBlankShow();
		AddSongManually("ToBeSaved");
		var path = Path.Combine(_tempDir.FullName, "SavedAs.show");
		_dialog.SavePathToReturn = path;

		_vm.SaveAsShowCommand.Execute(null);

		Assert.True(File.Exists(path));
		Assert.False(_vm.HasUnsavedChanges);
	}

	[Fact]
	public void SaveAsShow_Cancelled_DoesNotClearDirtyFlag() {
		LoadBlankShow();
		AddSongViaCommand("Unsaved");
		_dialog.SavePathToReturn = null; // simulate cancel

		_vm.SaveAsShowCommand.Execute(null);

		Assert.True(_vm.HasUnsavedChanges);
	}

	// ── Unsaved-changes guard ─────────────────────────────────────────────────

	[Fact]
	public void UnsavedChangesGuard_WhenConfirmed_SavesBeforeProceeding() {
		LoadBlankShow();
		AddSongViaCommand("Dirty");
		var savePath = Path.Combine(_tempDir.FullName, "AutoSaved.show");
		_dialog.SavePathToReturn = savePath;
		_dialog.Confirm3Result = true; // user clicks Yes

		// Trigger guard via NewShow
		_vm.NewShowCommand.Execute(null);

		Assert.True(File.Exists(savePath));
		Assert.False(_vm.HasUnsavedChanges);
	}

	[Fact]
	public void UnsavedChangesGuard_WhenDeclined_ProceedsWithoutSaving() {
		LoadBlankShow();
		AddSongViaCommand("Dirty");
		_dialog.Confirm3Result = false; // user clicks No

		_vm.NewShowCommand.Execute(null);

		// Proceeded — songs cleared
		Assert.Empty(_vm.Songs);
	}

	[Fact]
	public void UnsavedChangesGuard_WhenCancelled_AbortsOperation() {
		LoadBlankShow();
		AddSongViaCommand("Dirty");
		var songCountBefore = _vm.Songs.Count;
		_dialog.Confirm3Result = null; // user clicks Cancel

		_vm.NewShowCommand.Execute(null);

		// Aborted — show unchanged
		Assert.Equal(songCountBefore, _vm.Songs.Count);
		Assert.True(_vm.HasUnsavedChanges);
	}

	// ── Remove song ───────────────────────────────────────────────────────────

	[Fact]
	public void RemoveSong_WhenRemovedSongIsSelected_ClearsSelection() {
		LoadBlankShow();
		var entry = AddSongManually("ToRemove");
		_vm.SelectedSong = entry;

		_vm.RemoveSongCommand.Execute(entry);

		Assert.Null(_vm.SelectedSong);
	}

	[Fact]
	public void RemoveSong_WhenOtherSongIsSelected_PreservesSelection() {
		LoadBlankShow();
		var keep = AddSongManually("Keep");
		var remove = AddSongManually("Remove");
		_vm.SelectedSong = keep;

		_vm.RemoveSongCommand.Execute(remove);

		Assert.Same(keep, _vm.SelectedSong);
	}

	// ── Helpers ───────────────────────────────────────────────────────────────

	/// <summary>Creates a new blank show in the ViewModel without any guard dialogs.</summary>
	private void LoadBlankShow() {
		_dialog.Confirm3Result = false;
		_vm.NewShowCommand.Execute(null);
		_dialog.ResetCallTracking();
	}

	/// <summary>
	/// Adds a <see cref="SongEntry"/> directly to the ViewModel's Songs collection.
	/// Does NOT mark dirty — use <see cref="AddSongViaCommand"/> when dirty state is needed.
	/// </summary>
	private SongEntry AddSongManually(string name) {
		var entry = new SongEntry {
			Name = name,
			FolderPath = Path.Combine(_tempDir.FullName, name),
			Channels = new Dictionary<string, ChannelSettings> {
				["stem.wav"] = new ChannelSettings { Level = 1.0f, Muted = false },
			},
		};
		_vm.Songs.Add(entry);
		_vm.LoadedShow!.Songs.Add(entry);
		return entry;
	}

	/// <summary>
	/// Adds a song via <see cref="MainViewModel.AddSongCommand"/>, which marks the show dirty.
	/// Creates a real temp folder with an empty .wav stub so <see cref="SongScanner"/> accepts it.
	/// </summary>
	private SongEntry AddSongViaCommand(string name) {
		var songDir = CreateSongDir(name, [$"{name}.wav"]);
		_dialog.FolderToReturn = songDir;
		_vm.AddSongCommand.Execute(null);
		_dialog.FolderToReturn = null; // reset so future picks are clean
		return _vm.Songs.Last();
	}

	/// <summary>Saves the current show to a temp path.</summary>
	private void SaveCurrentShow() {
		var path = Path.Combine(_tempDir.FullName, $"{Guid.NewGuid():N}.show");
		_dialog.SavePathToReturn = path;
		_vm.SaveShowCommand.Execute(null);
	}

	/// <summary>Executes New Show with decline-guard so dirty flag is reset cleanly.</summary>
	private void ExecuteNewShow() {
		_dialog.Confirm3Result = false;
		_vm.NewShowCommand.Execute(null);
		_dialog.ResetCallTracking();
	}

	/// <summary>Creates a temp song directory with the specified audio files.</summary>
	private string CreateSongDir(string name, IEnumerable<string> fileNames) {
		var dir = Path.Combine(_tempDir.FullName, name);
		Directory.CreateDirectory(dir);
		foreach (var f in fileNames)
			File.WriteAllBytes(Path.Combine(dir, f), []);
		return dir;
	}
}

// ── Test double ───────────────────────────────────────────────────────────────

/// <summary>
/// Configurable test double for <see cref="IDialogService"/>.
/// All methods are pre-configured via public properties before the test exercise step.
/// </summary>
internal sealed class FakeDialogService : IDialogService {
	public string? FolderToReturn { get; set; }
	public string? OpenPathToReturn { get; set; }
	public string? SavePathToReturn { get; set; }

	/// <summary>
	/// Return value for the three-way <see cref="Confirm3"/> dialog.
	/// <see langword="true"/> = Yes, <see langword="false"/> = No, <see langword="null"/> = Cancel.
	/// </summary>
	public bool? Confirm3Result { get; set; } = false;

	public bool Confirm3WasCalled { get; private set; }
	public bool SavePickerWasCalled { get; private set; }

	public void ResetCallTracking() {
		Confirm3WasCalled = false;
		SavePickerWasCalled = false;
	}

	public string? PickFolder(string title) => FolderToReturn;
	public string? PickOpenFile(string title, string filter) => OpenPathToReturn;

	public string? PickSaveFile(string title, string filter, string defaultExtension) {
		SavePickerWasCalled = true;
		return SavePathToReturn;
	}

	// Two-way Confirm is no longer used by the ViewModel — kept to satisfy the interface.
	public bool Confirm(string message, string title) => Confirm3Result == true;

	public bool? Confirm3(string message, string title) {
		Confirm3WasCalled = true;
		return Confirm3Result;
	}

	public void ShowError(string message, string title) { /* no-op in tests */ }
}
