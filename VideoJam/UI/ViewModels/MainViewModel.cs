using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using VideoJam.Model;
using VideoJam.Services;

namespace VideoJam.UI.ViewModels;

/// <summary>
/// Root ViewModel for <see cref="MainWindow"/>.
/// Owns all operator-facing UI state and the show file operations.
/// No engine calls are made in Phase 5 — playback wiring is deferred to Phase 6.
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged {
	// ── Dependencies ──────────────────────────────────────────────────────────

	private readonly ShowFileService _showFileService = new();
	private readonly IDialogService _dialogService;

	// ── Backing fields ────────────────────────────────────────────────────────

	private Show? _loadedShow;
	private SongEntry? _selectedSong;
	private PlaybackState _playbackState = PlaybackState.Idle;
	private bool _hasUnsavedChanges;
	private string _statusText = StatusReady;
	private string? _showFilePath;

	// ── Status constants ──────────────────────────────────────────────────────

	private const string StatusReady = "Ready";
	private const string StatusShowLoaded = "Show loaded — select a song to cue";
	private const string AppTitle = "VideoJam";

	// ── Construction ──────────────────────────────────────────────────────────

	/// <summary>
	/// Initialises a new <see cref="MainViewModel"/>.
	/// </summary>
	/// <param name="dialogService">Dialog abstraction used for all user-facing prompts.</param>
	public MainViewModel(IDialogService dialogService) {
		_dialogService = dialogService;

		Songs = [];
		SelectedChannels = [];
		GlobalRoutingEntries = [];
		FallbackImageEntries = [];

		AddSongCommand = new RelayCommand(ExecuteAddSong, CanAddSong);
		RemoveSongCommand = new RelayCommand<SongEntry>(ExecuteRemoveSong, s => s is not null);
		ReorderSongCommand = new RelayCommand<(int from, int to)>(ExecuteReorderSong);

		NewShowCommand = new RelayCommand(ExecuteNewShow);
		OpenShowCommand = new RelayCommand(ExecuteOpenShow);
		SaveShowCommand = new RelayCommand(ExecuteSaveShow, () => _loadedShow is not null);
		SaveAsShowCommand = new RelayCommand(ExecuteSaveAsShow, () => _loadedShow is not null);

		AddRoutingEntryCommand = new RelayCommand(ExecuteAddRoutingEntry, () => _loadedShow is not null);
		RemoveRoutingEntryCommand = new RelayCommand<DisplayRoutingEntryViewModel>(ExecuteRemoveRoutingEntry);

		BrowseFallbackImageCommand = new RelayCommand<FallbackImageEntryViewModel>(ExecuteBrowseFallbackImage);

		RefreshFallbackDisplayEntries();
	}

	// ── Properties ────────────────────────────────────────────────────────────

	/// <summary>The ordered setlist, bound to the setlist <c>ListBox</c>.</summary>
	public ObservableCollection<SongEntry> Songs { get; }

	/// <summary>Per-channel mixer rows for the currently selected song.</summary>
	public ObservableCollection<ChannelSettingsViewModel> SelectedChannels { get; }

	/// <summary>Global display routing entries for the routing table UI.</summary>
	public ObservableCollection<DisplayRoutingEntryViewModel> GlobalRoutingEntries { get; }

	/// <summary>Per-display fallback image assignment entries.</summary>
	public ObservableCollection<FallbackImageEntryViewModel> FallbackImageEntries { get; }

	/// <summary>The currently loaded show, or <see langword="null"/> when no show is open.</summary>
	public Show? LoadedShow {
		get => _loadedShow;
		private set {
			if (_loadedShow == value) return;
			_loadedShow = value;
			OnPropertyChanged();
			OnPropertyChanged(nameof(WindowTitle));
			UpdateStatus();
		}
	}

	/// <summary>
	/// The currently selected / cued song.
	/// Two-way bound to the setlist <c>ListBox.SelectedItem</c>.
	/// </summary>
	public SongEntry? SelectedSong {
		get => _selectedSong;
		set {
			if (_selectedSong == value) return;
			_selectedSong = value;
			OnPropertyChanged();
			RebuildSelectedChannels();
			UpdateStatus();
		}
	}

	/// <summary>Current playback state. Defaults to <see cref="PlaybackState.Idle"/> in Phase 5.</summary>
	public PlaybackState PlaybackState {
		get => _playbackState;
		private set {
			if (_playbackState == value) return;
			_playbackState = value;
			OnPropertyChanged();
			OnPropertyChanged(nameof(IsMixerEnabled));
			OnPropertyChanged(nameof(IsSetlistInteractive));
		}
	}

	/// <summary>
	/// <see langword="true"/> when mixer controls (sliders, checkboxes) should be enabled.
	/// False during <see cref="PlaybackState.Playing"/> or <see cref="PlaybackState.Paused"/>.
	/// </summary>
	public bool IsMixerEnabled =>
		_playbackState is PlaybackState.Idle or PlaybackState.Cued;

	/// <summary>
	/// <see langword="true"/> when song selection in the setlist is allowed.
	/// False during <see cref="PlaybackState.Playing"/> or <see cref="PlaybackState.Paused"/>.
	/// </summary>
	public bool IsSetlistInteractive =>
		_playbackState is PlaybackState.Idle or PlaybackState.Cued;

	/// <summary><see langword="true"/> when the show has changes that have not been saved.</summary>
	public bool HasUnsavedChanges {
		get => _hasUnsavedChanges;
		private set {
			if (_hasUnsavedChanges == value) return;
			_hasUnsavedChanges = value;
			OnPropertyChanged();
			OnPropertyChanged(nameof(WindowTitle));
		}
	}

	/// <summary>Human-readable status message shown in the status bar.</summary>
	public string StatusText {
		get => _statusText;
		private set {
			if (_statusText == value) return;
			_statusText = value;
			OnPropertyChanged();
		}
	}

	/// <summary>
	/// WPF window title following the pattern <c>VideoJam — {show name}{*}</c>.
	/// Bound to <c>MainWindow.Title</c>.
	/// </summary>
	public string WindowTitle {
		get {
			if (_loadedShow is null) return $"{AppTitle} — (no show)";
			var name = string.IsNullOrWhiteSpace(_showFilePath)
				? "(unsaved)"
				: Path.GetFileNameWithoutExtension(_showFilePath);
			return $"{AppTitle} — {name}{(_hasUnsavedChanges ? "*" : string.Empty)}";
		}
	}

	// ── Commands ──────────────────────────────────────────────────────────────

	/// <summary>Adds a song by picking a folder via <see cref="IDialogService"/>.</summary>
	public RelayCommand AddSongCommand { get; }

	/// <summary>Removes a <see cref="SongEntry"/> from the setlist. Parameter: the entry to remove.</summary>
	public RelayCommand<SongEntry> RemoveSongCommand { get; }

	/// <summary>
	/// Reorders a song in the setlist. Parameter: a <c>(int from, int to)</c> tuple.
	/// Uses <see cref="ObservableCollection{T}.Move"/> to avoid re-creating the collection.
	/// </summary>
	public RelayCommand<(int from, int to)> ReorderSongCommand { get; }

	/// <summary>Creates a new blank show (with unsaved-changes guard).</summary>
	public RelayCommand NewShowCommand { get; }

	/// <summary>Opens an existing <c>.show</c> file (with unsaved-changes guard).</summary>
	public RelayCommand OpenShowCommand { get; }

	/// <summary>Saves the current show to its existing path (or delegates to Save As).</summary>
	public RelayCommand SaveShowCommand { get; }

	/// <summary>Saves the current show to a new path chosen by the operator.</summary>
	public RelayCommand SaveAsShowCommand { get; }

	/// <summary>Appends a blank entry to the global display routing table.</summary>
	public RelayCommand AddRoutingEntryCommand { get; }

	/// <summary>Removes a display routing entry. Parameter: the entry ViewModel to remove.</summary>
	public RelayCommand<DisplayRoutingEntryViewModel> RemoveRoutingEntryCommand { get; }

	/// <summary>Opens a PNG file picker for a fallback image entry. Parameter: the entry ViewModel.</summary>
	public RelayCommand<FallbackImageEntryViewModel> BrowseFallbackImageCommand { get; }

	// ── Public helpers ────────────────────────────────────────────────────────

	/// <summary>
	/// Called from <c>MainWindow.OnClosing</c>: applies the unsaved-changes guard and returns
	/// <see langword="true"/> if the close should be cancelled (i.e. user clicked Cancel).
	/// </summary>
	public bool ConfirmClose() {
		if (!_hasUnsavedChanges) return false;
		return !ApplyUnsavedChangesGuard();
	}

	// ── Command implementations ───────────────────────────────────────────────

	private bool CanAddSong() => _loadedShow is not null;

	private void ExecuteAddSong() {
		var folderPath = _dialogService.PickFolder("Select a song folder containing audio stems");
		if (folderPath is null) return;

		SongManifest manifest;
		try {
			manifest = SongScanner.Scan(new DirectoryInfo(folderPath));
		} catch (Exception ex) {
			_dialogService.ShowError(
				$"Failed to scan the selected folder:\n\n{ex.Message}",
				"Scan Error");
			return;
		}

		// Build channel settings from the manifest.
		var channels = manifest.AudioChannels
			.ToDictionary(
				ch => ch.ChannelId,
				ch => new ChannelSettings {
					Level = 1.0f,
					Muted = ch.Type == AudioChannelType.VideoAudio,
				});

		// Store the absolute folder path so ShowFileService.Save() can relativize it at
		// write time, regardless of whether a .show file path is known yet.
		var entry = new SongEntry {
			FolderPath = folderPath,
			Name = Path.GetFileName(folderPath),
			Channels = channels,
			DisplayRoutingOverrides = [],
		};

		_loadedShow!.Songs.Add(entry);
		Songs.Add(entry);
		MarkDirty();
	}

	private void ExecuteRemoveSong(SongEntry? song) {
		if (song is null) return;
		if (SelectedSong == song) SelectedSong = null;
		_loadedShow?.Songs.Remove(song);
		Songs.Remove(song);
		MarkDirty();
	}

	private void ExecuteReorderSong((int from, int to) args) {
		var (from, to) = args;
		if (from == to) return;
		if (from < 0 || to < 0 || from >= Songs.Count || to >= Songs.Count) return;

		Songs.Move(from, to);
		_loadedShow?.Songs.Clear();
		if (_loadedShow is not null) {
			foreach (var s in Songs)
				_loadedShow.Songs.Add(s);
		}
		MarkDirty();
	}

	private void ExecuteNewShow() {
		if (!ApplyUnsavedChangesGuard()) return;
		ApplyShow(new Show(), showFilePath: null);
	}

	private void ExecuteOpenShow() {
		if (!ApplyUnsavedChangesGuard()) return;

		var path = _dialogService.PickOpenFile(
			"Open Show File",
			"Show files|*.show|All files|*.*");
		if (path is null) return;

		Show show;
		try {
			show = _showFileService.Load(path);
		} catch (Exception ex) {
			_dialogService.ShowError(
				$"Failed to open the show file:\n\n{ex.Message}",
				"Open Error");
			return;
		}

		// Resolve relative paths to absolute so in-memory Show always has absolute paths.
		NormalizeLoadedPaths(show, Path.GetDirectoryName(path)!);
		ApplyShow(show, showFilePath: path);
	}

	private void ExecuteSaveShow() {
		if (_loadedShow is null) return;
		if (_showFilePath is null) {
			ExecuteSaveAsShow();
			return;
		}
		SaveTo(_showFilePath);
	}

	private void ExecuteSaveAsShow() {
		if (_loadedShow is null) return;
		var path = _dialogService.PickSaveFile(
			"Save Show File",
			"Show files|*.show|All files|*.*",
			"show");
		if (path is null) return;
		SaveTo(path);
	}

	private void SaveTo(string path) {
		// Sync in-memory routing changes back to the Show model before serialising.
		SyncRoutingToModel();

		try {
			_showFileService.Save(_loadedShow!, path);
		} catch (Exception ex) {
			_dialogService.ShowError(
				$"Failed to save the show file:\n\n{ex.Message}",
				"Save Error");
			return;
		}

		_showFilePath = path;
		HasUnsavedChanges = false;
		OnPropertyChanged(nameof(WindowTitle));
	}

	private void ExecuteAddRoutingEntry() {
		if (_loadedShow is null) return;
		var entry = new DisplayRoutingEntryViewModel(string.Empty, 0, OnRoutingEntryChanged);
		entry.PropertyChanged += OnRoutingEntryPropertyChanged;
		GlobalRoutingEntries.Add(entry);
		// Actual dictionary update happens in SyncRoutingToModel() at save time.
		MarkDirty();
	}

	private void ExecuteRemoveRoutingEntry(DisplayRoutingEntryViewModel? entry) {
		if (entry is null) return;
		entry.PropertyChanged -= OnRoutingEntryPropertyChanged;
		GlobalRoutingEntries.Remove(entry);
		_loadedShow?.GlobalDisplayRouting.Remove(entry.Suffix);
		MarkDirty();
	}

	private void ExecuteBrowseFallbackImage(FallbackImageEntryViewModel? entry) {
		if (entry is null) return;
		var path = _dialogService.PickOpenFile(
			$"Select fallback image for Display {entry.DisplayIndex}",
			"PNG images|*.png|All files|*.*");
		if (path is null) return;
		entry.ImagePath = path;
		if (_loadedShow is not null)
			_loadedShow.FallbackImages[entry.DisplayIndex] = path;
		MarkDirty();
	}

	// ── Unsaved-changes guard ─────────────────────────────────────────────────

	/// <summary>
	/// Applies the unsaved-changes guard:
	/// <list type="bullet">
	///   <item>If no unsaved changes exist, returns <see langword="true"/> (proceed).</item>
	///   <item>If the user confirms (Yes), saves first then returns <see langword="true"/>.</item>
	///   <item>If the user declines (No), discards changes and returns <see langword="true"/>.</item>
	///   <item>If there is no way to cancel (only when the dialog returns false), returns <see langword="false"/> (abort).</item>
	/// </list>
	/// Uses a three-button MessageBox (Yes / No / Cancel) equivalent via a two-step dialog.
	/// </summary>
	/// <returns><see langword="true"/> to proceed; <see langword="false"/> to abort.</returns>
	private bool ApplyUnsavedChangesGuard() {
		if (!_hasUnsavedChanges) return true;

		// Ask: Save changes?
		bool save = _dialogService.Confirm(
			"You have unsaved changes. Save before continuing?",
			"Unsaved Changes");

		if (save) {
			ExecuteSaveShow();
			// If save failed (still dirty), abort.
			if (_hasUnsavedChanges) return false;
		}
		return true;
	}

	// ── Show application helpers ──────────────────────────────────────────────

	private void ApplyShow(Show show, string? showFilePath) {
		_showFilePath = showFilePath;
		LoadedShow = show;

		Songs.Clear();
		foreach (var s in show.Songs)
			Songs.Add(s);

		SelectedSong = null;

		GlobalRoutingEntries.Clear();
		foreach (var kvp in show.GlobalDisplayRouting) {
			var entry = new DisplayRoutingEntryViewModel(kvp.Key, kvp.Value, OnRoutingEntryChanged);
			entry.PropertyChanged += OnRoutingEntryPropertyChanged;
			GlobalRoutingEntries.Add(entry);
		}

		RefreshFallbackDisplayEntries();

		HasUnsavedChanges = false;
		OnPropertyChanged(nameof(WindowTitle));
	}

	/// <summary>
	/// After loading a show from disk, converts relative <see cref="SongEntry.FolderPath"/>
	/// and <see cref="Show.FallbackImages"/> values to absolute paths so the in-memory model
	/// is always absolute and <see cref="ShowFileService"/> can safely re-relativize on save.
	/// </summary>
	private static void NormalizeLoadedPaths(Show show, string showDirectory) {
		foreach (var song in show.Songs) {
			if (!string.IsNullOrEmpty(song.FolderPath) && !Path.IsPathRooted(song.FolderPath))
				song.FolderPath = PathResolver.Resolve(song.FolderPath, showDirectory);
		}

		foreach (var key in show.FallbackImages.Keys.ToList()) {
			var val = show.FallbackImages[key];
			if (!string.IsNullOrEmpty(val) && !Path.IsPathRooted(val))
				show.FallbackImages[key] = PathResolver.Resolve(val, showDirectory);
		}
	}

	/// <summary>Rebuilds <see cref="SelectedChannels"/> from the newly selected song's channels.</summary>
	private void RebuildSelectedChannels() {
		SelectedChannels.Clear();
		if (_selectedSong is null) return;

		foreach (var kvp in _selectedSong.Channels) {
			SelectedChannels.Add(new ChannelSettingsViewModel(kvp.Key, kvp.Value, MarkDirty));
		}
	}

	/// <summary>
	/// Enumerates connected displays and rebuilds <see cref="FallbackImageEntries"/>.
	/// Existing image assignments from <see cref="LoadedShow"/> are preserved.
	/// </summary>
	private void RefreshFallbackDisplayEntries() {
		FallbackImageEntries.Clear();
		var screens = System.Windows.Forms.Screen.AllScreens;
		for (int i = 0; i < screens.Length; i++) {
			var existingPath = _loadedShow?.FallbackImages.TryGetValue(i, out var p) == true ? p : null;
			FallbackImageEntries.Add(new FallbackImageEntryViewModel(
				i,
				screens[i].DeviceName,
				existingPath,
				MarkDirty));
		}
	}

	/// <summary>
	/// Syncs the <see cref="GlobalRoutingEntries"/> observable collection back into
	/// <see cref="Show.GlobalDisplayRouting"/> before serialisation.
	/// </summary>
	private void SyncRoutingToModel() {
		if (_loadedShow is null) return;
		_loadedShow.GlobalDisplayRouting.Clear();
		foreach (var entry in GlobalRoutingEntries) {
			if (!string.IsNullOrWhiteSpace(entry.Suffix))
				_loadedShow.GlobalDisplayRouting[entry.Suffix] = entry.DisplayIndex;
		}
	}

	/// <summary>Sets <see cref="HasUnsavedChanges"/> to <see langword="true"/>.</summary>
	private void MarkDirty() => HasUnsavedChanges = true;

	private void UpdateStatus() {
		StatusText = (_loadedShow, _selectedSong) switch {
			(null, _) => StatusReady,
			(_, null) => StatusShowLoaded,
			var (_, song) => $"Cued: {song!.Name}",
		};
	}

	// ── Routing entry property watcher ────────────────────────────────────────

	private void OnRoutingEntryPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
		MarkDirty();

	private void OnRoutingEntryChanged() => MarkDirty();

	// ── INotifyPropertyChanged ────────────────────────────────────────────────

	/// <inheritdoc />
	public event PropertyChangedEventHandler? PropertyChanged;

	private void OnPropertyChanged([CallerMemberName] string? name = null) =>
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
