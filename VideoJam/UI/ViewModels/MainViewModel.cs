using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using VideoJam.Engine;
using VideoJam.Input;
using VideoJam.Model;
using VideoJam.Services;

namespace VideoJam.UI.ViewModels;

/// <summary>
/// Root ViewModel for <see cref="MainWindow"/>.
/// Owns all operator-facing UI state, show file operations, and playback control.
/// </summary>
internal sealed class MainViewModel : INotifyPropertyChanged, IDisposable {
	// ── Dependencies ──────────────────────────────────────────────────────────

	private readonly ShowFileService _showFileService = new();
	private readonly IDialogService _dialogService;
	private readonly PlaybackEngine? _playbackEngine;
	private readonly HotkeyService? _hotkeyService;
	private HotkeyService? _subscribedHotkeyService;

	// ── Backing fields ────────────────────────────────────────────────────────

	private Show? _loadedShow;
	private SongEntry? _selectedSong;
	private SongRowViewModel? _selectedSongRow;
	private PlaybackState _playbackState = PlaybackState.Idle;
	private bool _isLoading;
	private bool _hasUnsavedChanges;
	private string _statusText = StatusReady;
	private string? _showFilePath;
	private bool _disposed;

	// ── Status constants ──────────────────────────────────────────────────────

	private const string StatusReady = "Ready";
	private const string StatusShowLoaded = "Show loaded — select a song to cue";
	private const string AppTitle = "VideoJam";

	// ── Construction ──────────────────────────────────────────────────────────

	/// <summary>
	/// Initialises a new <see cref="MainViewModel"/>.
	/// </summary>
	/// <param name="dialogService">Dialog abstraction used for all user-facing prompts.</param>
	/// <param name="playbackEngine">Playback engine; <see langword="null"/> until Phase 6 wiring is complete.</param>
	/// <param name="hotkeyService">Hotkey service; <see langword="null"/> until Phase 6 wiring is complete.</param>
	public MainViewModel(
		IDialogService dialogService,
		PlaybackEngine? playbackEngine = null,
		HotkeyService? hotkeyService = null) {
		_dialogService = dialogService;
		_playbackEngine = playbackEngine;
		_hotkeyService = hotkeyService;

		SongRows = [];
		SelectedChannels = [];

		AddSongCommand = new RelayCommand(ExecuteAddSong, CanAddSong);
		RemoveSongCommand = new RelayCommand<SongRowViewModel>(ExecuteRemoveSong, r => r is not null);
		ReorderSongCommand = new RelayCommand<(int from, int to)>(ExecuteReorderSong);

		NewShowCommand = new RelayCommand(ExecuteNewShow);
		OpenShowCommand = new RelayCommand(ExecuteOpenShow);
		SaveShowCommand = new RelayCommand(ExecuteSaveShow, () => _loadedShow is not null);
		SaveAsShowCommand = new RelayCommand(ExecuteSaveAsShow, () => _loadedShow is not null);

		BrowseFallbackImageCommand = new RelayCommand(ExecuteBrowseFallbackImage);

		GoCommand = new RelayCommand(ExecuteGo, CanGo);
		StopAndRewindCommand = new RelayCommand(ExecuteStopAndRewind, CanStopAndRewind);
		CueSongCommand = new RelayCommand<SongRowViewModel>(ExecuteCueSong, CanCueSong);

		if (_playbackEngine is not null)
			_playbackEngine.StateChanged += OnPlaybackStateChanged;

		if (_hotkeyService is not null) {
			_hotkeyService.ButtonAPressed += OnButtonAPressed;
			_hotkeyService.ButtonBPressed += OnButtonBPressed;
			_subscribedHotkeyService = _hotkeyService;
		}
	}

	// ── Properties ────────────────────────────────────────────────────────────

	/// <summary>
	/// The ordered setlist, bound to the setlist <c>ListBox</c>.
	/// Each row wraps a <see cref="SongEntry"/> and exposes a bindable <see cref="SongRowViewModel.DisplayIndex"/>
	/// that is kept current after every Add / Remove / Reorder so the displayed index is always correct.
	/// </summary>
	public ObservableCollection<SongRowViewModel> SongRows { get; }

	/// <summary>Per-channel mixer rows for the currently selected song.</summary>
	public ObservableCollection<ChannelSettingsViewModel> SelectedChannels { get; }

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
	/// The currently selected setlist row (wraps <see cref="SelectedSong"/>).
	/// Two-way bound to the setlist <c>ListBox.SelectedItem</c>.
	/// Setting this property also updates <see cref="SelectedSong"/>.
	/// </summary>
	public SongRowViewModel? SelectedSongRow {
		get => _selectedSongRow;
		set {
			if (_selectedSongRow == value) return;
			_selectedSongRow = value;
			_selectedSong = value?.Song;
			OnPropertyChanged();
			OnPropertyChanged(nameof(SelectedSong));
			RebuildSelectedChannels();
			UpdateStatus();
		}
	}

	/// <summary>
	/// The underlying <see cref="SongEntry"/> for the currently selected song, or <see langword="null"/>.
	/// Setting this property also synchronises <see cref="SelectedSongRow"/> to the matching row.
	/// </summary>
	public SongEntry? SelectedSong {
		get => _selectedSong;
		set {
			if (_selectedSong == value) return;
			_selectedSong = value;
			_selectedSongRow = value is null ? null : SongRows.FirstOrDefault(r => r.Song == value);
			OnPropertyChanged();
			OnPropertyChanged(nameof(SelectedSongRow));
			RebuildSelectedChannels();
			UpdateStatus();
		}
	}

	/// <summary>Current playback state. Updated via <see cref="OnPlaybackStateChanged"/>.</summary>
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
	/// <see langword="true"/> while a <see cref="PlaybackEngine.Cue"/> operation is in progress.
	/// </summary>
	public bool IsLoading {
		get => _isLoading;
		private set {
			if (_isLoading == value) return;
			_isLoading = value;
			OnPropertyChanged();
			UpdateStatus();
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

	/// <summary>
	/// Display-friendly filename for the show's fallback image, or <c>"(none)"</c> when not set.
	/// Bound to the fallback image label in the UI.
	/// </summary>
	public string FallbackImageDisplay {
		get {
			var path = _loadedShow?.FallbackImagePath;
			return string.IsNullOrEmpty(path) ? "(none)" : Path.GetFileName(path);
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

	/// <summary>Removes a <see cref="SongRowViewModel"/> from the setlist. Parameter: the row to remove.</summary>
	public RelayCommand<SongRowViewModel> RemoveSongCommand { get; }

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

	/// <summary>Opens a PNG file picker to set the show's fallback image.</summary>
	public RelayCommand BrowseFallbackImageCommand { get; }

	/// <summary>
	/// Starts playback of the cued song.
	/// Disabled when <see cref="PlaybackState"/> is not <see cref="PlaybackState.Cued"/>
	/// or while a cue operation is in progress.
	/// </summary>
	public RelayCommand GoCommand { get; }

	/// <summary>
	/// Stops/rewinds playback (two-phase: Playing → Paused → Cued).
	/// Disabled when <see cref="PlaybackState"/> is not Playing or Paused.
	/// </summary>
	public RelayCommand StopAndRewindCommand { get; }

	/// <summary>
	/// Cues the song corresponding to the clicked setlist row.
	/// Disabled during <see cref="PlaybackState.Playing"/> and <see cref="PlaybackState.Paused"/>.
	/// </summary>
	public RelayCommand<SongRowViewModel> CueSongCommand { get; }

	// ── Public helpers ────────────────────────────────────────────────────────

	/// <summary>
	/// Called from <c>MainWindow.OnClosing</c>: applies the unsaved-changes guard and returns
	/// <see langword="true"/> if the window close should be cancelled (user clicked Cancel).
	/// </summary>
	public bool ConfirmClose() {
		if (!_hasUnsavedChanges) return false;

		bool? result = _dialogService.Confirm3(
			"You have unsaved changes. Save before closing?",
			"Unsaved Changes");

		if (result is null) return true;   // Cancel → abort the close

		if (result == true) {
			ExecuteSaveShow();
			if (_hasUnsavedChanges) return true;  // Save failed or was itself cancelled → abort close
		}
		return false;  // No → allow the window to close without saving
	}

	/// <summary>
	/// Injects the <see cref="HotkeyService"/> after construction.
	/// Called from <c>App.xaml.cs</c> after the service is created on the UI thread.
	/// </summary>
	/// <param name="hotkeyService">The hotkey service to subscribe to.</param>
	public void SetHotkeyService(HotkeyService hotkeyService) {
		// Detach the previously subscribed service if any.
		if (_subscribedHotkeyService is not null) {
			_subscribedHotkeyService.ButtonAPressed -= OnButtonAPressed;
			_subscribedHotkeyService.ButtonBPressed -= OnButtonBPressed;
		}

		_subscribedHotkeyService = hotkeyService;
		hotkeyService.ButtonAPressed += OnButtonAPressed;
		hotkeyService.ButtonBPressed += OnButtonBPressed;
	}

	/// <inheritdoc />
	public void Dispose() {
		if (_disposed) return;
		_disposed = true;

		if (_playbackEngine is not null)
			_playbackEngine.StateChanged -= OnPlaybackStateChanged;

		if (_subscribedHotkeyService is not null) {
			_subscribedHotkeyService.ButtonAPressed -= OnButtonAPressed;
			_subscribedHotkeyService.ButtonBPressed -= OnButtonBPressed;
		}
	}

	// ── Playback command implementations ─────────────────────────────────────

	private bool CanGo() => _playbackState == PlaybackState.Cued && !_isLoading;

	private void ExecuteGo() {
		_playbackEngine?.Go();
	}

	private bool CanStopAndRewind() =>
		_playbackState is PlaybackState.Playing or PlaybackState.Paused;

	private void ExecuteStopAndRewind() {
		_playbackEngine?.StopAndRewind();
	}

	private bool CanCueSong(SongRowViewModel? row) =>
		row is not null &&
		_loadedShow is not null &&
		_playbackState is not PlaybackState.Playing and not PlaybackState.Paused;

	private void ExecuteCueSong(SongRowViewModel? row) {
		if (row is null || _playbackEngine is null || _loadedShow is null) return;

		var index = _loadedShow.Songs.IndexOf(row.Song);
		if (index < 0) return;

		IsLoading = true;
		_ = _playbackEngine.Cue(index).ContinueWith(
			_ => { IsLoading = false; UpdateStatus(); },
			System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
	}

	// ── Hotkey handlers ───────────────────────────────────────────────────────

	private void OnButtonAPressed(object? sender, EventArgs e) {
		if (GoCommand.CanExecute(null))
			GoCommand.Execute(null);
	}

	private void OnButtonBPressed(object? sender, EventArgs e) {
		if (StopAndRewindCommand.CanExecute(null))
			StopAndRewindCommand.Execute(null);
	}

	// ── PlaybackEngine event handler ──────────────────────────────────────────

	private void OnPlaybackStateChanged(object? sender, EventArgs e) {
		if (_playbackEngine is null) return;

		PlaybackState = _playbackEngine.State;

		// Sync SelectedSong to match the cued song index.
		var cuedIndex = _playbackEngine.CuedSongIndex;
		if (cuedIndex >= 0 && cuedIndex < (_loadedShow?.Songs.Count ?? 0)) {
			var song = _loadedShow!.Songs[cuedIndex];
			// Update backing fields directly to avoid double PropertyChanged chain.
			_selectedSong = song;
			_selectedSongRow = SongRows.FirstOrDefault(r => r.Song == song);
			OnPropertyChanged(nameof(SelectedSong));
			OnPropertyChanged(nameof(SelectedSongRow));
			RebuildSelectedChannels();
		} else if (_playbackEngine.State == PlaybackState.Idle) {
			_selectedSong = null;
			_selectedSongRow = null;
			OnPropertyChanged(nameof(SelectedSong));
			OnPropertyChanged(nameof(SelectedSongRow));
			RebuildSelectedChannels();
		}

		// IsLoading: false once we reach Cued or Idle (Cue has completed).
		if (_playbackEngine.State is PlaybackState.Cued or PlaybackState.Idle)
			IsLoading = false;

		UpdateStatus();
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

		// NOTE (W2): We deliberately do NOT call SongEntry.CreateFromScan() here.
		// CreateFromScan() calls PathResolver.MakeRelative() at scan time, which requires
		// a known .show file path. When no show has been saved yet that path is unknown.
		// Instead we store the absolute folder path and defer relativisation to
		// ShowFileService.Save(), which calls ToRelativePaths() at write time.
		// If CreateFromScan() ever gains new channel-default logic this path must be kept in sync.
		var entry = new SongEntry {
			FolderPath = folderPath,
			Name = Path.GetFileName(folderPath),
			Channels = channels,
		};

		_loadedShow!.Songs.Add(entry);
		SongRows.Add(new SongRowViewModel(entry, SongRows.Count + 1));
		MarkDirty();
	}

	private void ExecuteRemoveSong(SongRowViewModel? row) {
		if (row is null) return;
		if (_selectedSong == row.Song) SelectedSong = null;
		_loadedShow?.Songs.Remove(row.Song);
		SongRows.Remove(row);
		RenumberSongRows();
		MarkDirty();
	}

	private void ExecuteReorderSong((int from, int to) args) {
		var (from, to) = args;
		if (from == to) return;
		if (from < 0 || to < 0 || from >= SongRows.Count || to >= SongRows.Count) return;

		SongRows.Move(from, to);
		RenumberSongRows();

		// Sync reordered ViewModel rows back into the model's Songs list.
		_loadedShow?.Songs.Clear();
		if (_loadedShow is not null) {
			foreach (var row in SongRows)
				_loadedShow.Songs.Add(row.Song);
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
		// Capture video window layouts so the operator's window arrangement is persisted.
		if (_playbackEngine is not null) {
			_loadedShow!.VideoWindowLayouts.Clear();
			foreach (var (slotIndex, window) in _playbackEngine.VideoWindows) {
				_loadedShow.VideoWindowLayouts[slotIndex] =
					window.Dispatcher.Invoke(() => window.GetLayout());
			}
		}

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

	private void ExecuteBrowseFallbackImage() {
		if (_loadedShow is null) return;
		var path = _dialogService.PickOpenFile(
			"Select fallback image",
			"PNG images|*.png|All files|*.*");
		if (path is null) return;
		// Store path relative to the show file when a save path is known, otherwise absolute.
		_loadedShow.FallbackImagePath = _showFilePath is not null
			? PathResolver.MakeRelative(path, Path.GetDirectoryName(_showFilePath)!)
			: path;
		// Propagate the absolute path to the engine immediately so open windows update
		// without waiting for the next UpdateShow() call.
		_playbackEngine?.SetFallbackImage(path);
		OnPropertyChanged(nameof(FallbackImageDisplay));
		MarkDirty();
	}

	// ── Unsaved-changes guard ─────────────────────────────────────────────────

	/// <summary>
	/// Applies the unsaved-changes guard before a destructive operation (New / Open):
	/// <list type="bullet">
	///   <item>No unsaved changes → returns <see langword="true"/> (proceed immediately).</item>
	///   <item>Yes → saves first; returns <see langword="true"/> on success, <see langword="false"/> if save fails.</item>
	///   <item>No → discards changes and returns <see langword="true"/> (proceed without saving).</item>
	///   <item>Cancel → returns <see langword="false"/> (abort — do not perform the operation).</item>
	/// </list>
	/// </summary>
	/// <returns><see langword="true"/> to proceed; <see langword="false"/> to abort.</returns>
	private bool ApplyUnsavedChangesGuard() {
		if (!_hasUnsavedChanges) return true;

		bool? result = _dialogService.Confirm3(
			"You have unsaved changes. Save before continuing?",
			"Unsaved Changes");

		if (result is null) return false;   // Cancel → abort

		if (result == true) {
			ExecuteSaveShow();
			if (_hasUnsavedChanges) return false;  // Save failed or was cancelled → abort
		}
		return true;  // No → discard and proceed; Yes+saved → proceed
	}

	// ── Show application helpers ──────────────────────────────────────────────

	private void ApplyShow(Show show, string? showFilePath) {
		_showFilePath = showFilePath;
		// Notify the PlaybackEngine about the new show before updating UI state.
		_playbackEngine?.UpdateShow(show);
		LoadedShow = show;

		SongRows.Clear();
		for (int i = 0; i < show.Songs.Count; i++)
			SongRows.Add(new SongRowViewModel(show.Songs[i], i + 1));

		SelectedSong = null;

		OnPropertyChanged(nameof(FallbackImageDisplay));
		HasUnsavedChanges = false;
		OnPropertyChanged(nameof(WindowTitle));
	}

	/// <summary>
	/// After loading a show from disk, converts relative <see cref="SongEntry.FolderPath"/>
	/// and <see cref="Show.FallbackImagePath"/> to absolute paths so the in-memory model
	/// is always absolute and <see cref="ShowFileService"/> can safely re-relativize on save.
	/// </summary>
	private static void NormalizeLoadedPaths(Show show, string showDirectory) {
		foreach (var song in show.Songs) {
			if (!string.IsNullOrEmpty(song.FolderPath) && !Path.IsPathRooted(song.FolderPath))
				song.FolderPath = PathResolver.Resolve(song.FolderPath, showDirectory);
		}

		if (!string.IsNullOrEmpty(show.FallbackImagePath) && !Path.IsPathRooted(show.FallbackImagePath))
			show.FallbackImagePath = PathResolver.Resolve(show.FallbackImagePath, showDirectory);
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
	/// Updates the <see cref="SongRowViewModel.DisplayIndex"/> of every row to match its current
	/// position in <see cref="SongRows"/>. Called after every Add, Remove, or Reorder mutation
	/// so the bound <c>TextBlock</c> in each setlist row always displays the correct 1-based number.
	/// </summary>
	private void RenumberSongRows() {
		for (int i = 0; i < SongRows.Count; i++)
			SongRows[i].DisplayIndex = i + 1;
	}

	/// <summary>Sets <see cref="HasUnsavedChanges"/> to <see langword="true"/>.</summary>
	private void MarkDirty() => HasUnsavedChanges = true;

	private void UpdateStatus() {
		if (_loadedShow is null) {
			StatusText = StatusReady;
			return;
		}

		if (_selectedSong is null) {
			StatusText = StatusShowLoaded;
			return;
		}

		var songName = _selectedSong.Name;

		StatusText = (_playbackState, _isLoading) switch {
			(_, true)                                => $"Loading: {songName}\u2026",
			(PlaybackState.Cued, _)                  => $"Cued: {songName} \u2014 press GO to start",
			(PlaybackState.Playing, _)               => $"Playing: {songName}",
			(PlaybackState.Paused, _)                => $"Paused: {songName} \u2014 press ESC to rewind",
			_                                        => StatusShowLoaded,
		};
	}


	// ── INotifyPropertyChanged ────────────────────────────────────────────────

	/// <inheritdoc />
	public event PropertyChangedEventHandler? PropertyChanged;

	private void OnPropertyChanged([CallerMemberName] string? name = null) =>
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
