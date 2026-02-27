using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;
using VideoJam.Engine;
using VideoJam.Model;
using VideoJam.Services;

namespace VideoJam.UI;

/// <summary>
/// Phase 2 integration harness — a minimal WPF window for manual A/V sync verification.
/// This code-behind is temporary and will be replaced by the full MVVM operator UI in Phase 5.
/// </summary>
public partial class MainWindow : Window {
	// ── Constants ─────────────────────────────────────────────────────────────

	/// <summary>
	/// Display index for the primary (laptop) display.
	/// Phase 3 note: promote to DisplayManager.PrimaryDisplayIndex when that class is implemented.
	/// </summary>
	private const int PrimaryDisplayIndex = 0;

	// ── Engines ───────────────────────────────────────────────────────────────

	private AudioEngine?     _audioEngine;
	private VideoEngine?     _videoEngine;
	private SyncCoordinator? _syncCoordinator;

	// ── Loaded data ───────────────────────────────────────────────────────────

	private SongManifest?     _manifest;
	private VlcDisplayWindow? _displayWindow;
	private string?           _videoFilePath;

	// ── Construction ──────────────────────────────────────────────────────────

	/// <inheritdoc />
	public MainWindow() {
		InitializeComponent();
	}

	// ── Button handlers ───────────────────────────────────────────────────────

	private void OnLoadClicked(object sender, RoutedEventArgs e) {
		var dialog = new OpenFolderDialog {
			Title = "Select a song folder containing audio stems",
		};

		if (dialog.ShowDialog(this) != true)
			return;

		CleanupAll();

		var folder = new DirectoryInfo(dialog.FolderName);

		try {
			_manifest = SongScanner.Scan(folder);

			FolderLabel.Text = folder.FullName;
			SetStatus("Folder loaded");

			UpdatePlayButtonState();
		}
		catch (Exception ex) {
			MessageBox.Show(
				$"Failed to load folder:\n\n{ex.Message}",
				"Load Error",
				MessageBoxButton.OK,
				MessageBoxImage.Error);
			SetStatus("Idle");
		}
	}

	private void OnLoadVideoClicked(object sender, RoutedEventArgs e) {
		var dialog = new OpenFileDialog {
			Title  = "Select a video file (MP4)",
			Filter = "MP4 Video|*.mp4|All files|*.*",
		};

		if (dialog.ShowDialog(this) != true)
			return;

		_videoFilePath  = dialog.FileName;
		VideoLabel.Text = System.IO.Path.GetFileName(_videoFilePath);

		UpdatePlayButtonState();
	}

	private async void OnPlayClicked(object sender, RoutedEventArgs e) {
		if (_manifest is null) return;

		// FIX #1: Dispose stale engines from a previous natural-end cycle before
		// creating new ones. OnPlaybackEnded intentionally skips CleanupAll() to
		// preserve _manifest and _videoFilePath for replay, so stale engine instances
		// can survive until the next Play press. Dispose them here.
		if (_audioEngine is not null) {
			_audioEngine.PlaybackEnded -= OnPlaybackEnded;
			_audioEngine.Dispose();
			_audioEngine = null;
		}
		_videoEngine?.Dispose();
		_videoEngine = null;
		CloseDisplayWindow();

		SetStatus("Loading…");
		LoadButton.IsEnabled      = false;
		LoadVideoButton.IsEnabled = false;
		PlayButton.IsEnabled      = false;

		try {
			var loggerFactory = NullLoggerFactory.Instance;

			// ── Build the audio pipeline ──────────────────────────────────────
			var channelSettings = new Dictionary<string, ChannelSettings>();
			foreach (AudioChannelManifest ch in _manifest.AudioChannels) {
				channelSettings[ch.ChannelId] = new ChannelSettings {
					Level = 1.0f,
					Muted = ch.Type == AudioChannelType.VideoAudio,
				};
			}

			_audioEngine = new AudioEngine();
			_audioEngine.PlaybackEnded += OnPlaybackEnded;
			_audioEngine.Load(_manifest, channelSettings);

			// ── Build the video pipeline (if a video file was selected) ───────
			_videoEngine = new VideoEngine(loggerFactory.CreateLogger<VideoEngine>());

			SongManifest manifestForVideo = _manifest;

			// If the user selected a video file separately (outside of a song folder),
			// inject it into the manifest as a primary-display video entry.
			if (_videoFilePath is not null
				&& !_manifest.VideoFiles.Any(v => v.DisplayIndex == PrimaryDisplayIndex)) {
				var injectedVideo = new VideoFileManifest(
					File: new FileInfo(_videoFilePath),
					DisplayIndex: PrimaryDisplayIndex,
					Suffix: string.Empty);

				manifestForVideo = _manifest with {
					VideoFiles = _manifest.VideoFiles.Prepend(injectedVideo).ToList(),
				};
			}

			if (manifestForVideo.VideoFiles.Any(v => v.DisplayIndex == PrimaryDisplayIndex)) {
				_displayWindow = CreatePrimaryDisplayWindow();
				await _videoEngine.Load(manifestForVideo, displayIndex: PrimaryDisplayIndex, _displayWindow);
			}

			// ── Synchronised start ────────────────────────────────────────────
			_syncCoordinator = new SyncCoordinator(loggerFactory.CreateLogger<SyncCoordinator>());
			_syncCoordinator.Start(_audioEngine, _videoEngine);

			SetStatus("Playing");
			StopButton.IsEnabled = true;
		}
		catch (Exception ex) {
			MessageBox.Show(
				$"Playback failed:\n\n{ex.Message}",
				"Playback Error",
				MessageBoxButton.OK,
				MessageBoxImage.Error);

			CleanupAll();
			SetStatus("Idle");
			LoadButton.IsEnabled      = true;
			LoadVideoButton.IsEnabled = true;
			UpdatePlayButtonState();
		}
	}

	private void OnStopClicked(object sender, RoutedEventArgs e) {
		CleanupAll();
		SetStatus("Stopped");
		LoadButton.IsEnabled      = true;
		LoadVideoButton.IsEnabled = true;
		StopButton.IsEnabled      = false;
		UpdatePlayButtonState();
	}

	// ── PlaybackEnded callback (marshalled to UI thread by AudioEngine) ────────

	private void OnPlaybackEnded(object? sender, EventArgs e) {
		// Intentionally does NOT call CleanupAll() — _manifest and _videoFilePath
		// must survive so the user can press Play again for the same song.
		// Stale _audioEngine and _videoEngine are disposed at the top of OnPlayClicked.
		_videoEngine?.Stop();
		CloseDisplayWindow();
		SetStatus("Stopped");
		LoadButton.IsEnabled      = true;
		LoadVideoButton.IsEnabled = true;
		PlayButton.IsEnabled      = _manifest is not null;
		StopButton.IsEnabled      = false;
	}

	// ── Helpers ───────────────────────────────────────────────────────────────

	/// <summary>
	/// Creates a <see cref="VlcDisplayWindow"/> sized and positioned to cover the primary display.
	/// </summary>
	/// <remarks>
	/// The DPI scale factor is derived from the ratio of the primary screen's physical pixel
	/// dimensions (<see cref="System.Windows.Forms.Screen.PrimaryScreen"/>) to WPF's
	/// device-independent dimensions (<see cref="SystemParameters.PrimaryScreenWidth"/>).
	/// This is independent of the monitor the harness window currently sits on, fixing the
	/// bug where using <c>HwndSource.FromHwnd(this.Handle)</c> would apply the harness
	/// window's DPI rather than the primary display's DPI.
	/// </remarks>
	private static VlcDisplayWindow CreatePrimaryDisplayWindow() {
		var screen = System.Windows.Forms.Screen.PrimaryScreen!;

		// Derive DPI scale from the primary screen's physical vs. logical dimensions.
		// SystemParameters returns WPF DIP values for the primary monitor regardless
		// of where the harness window is placed.
		double dpiScaleX = screen.Bounds.Width  / SystemParameters.PrimaryScreenWidth;
		double dpiScaleY = screen.Bounds.Height / SystemParameters.PrimaryScreenHeight;

		double left   = screen.Bounds.Left   / dpiScaleX;
		double top    = screen.Bounds.Top    / dpiScaleY;
		double width  = screen.Bounds.Width  / dpiScaleX;
		double height = screen.Bounds.Height / dpiScaleY;

		var window = new VlcDisplayWindow();
		window.SetBounds(left, top, width, height);
		window.Show();
		return window;
	}

	private void SetStatus(string status) =>
		StatusLabel.Text = $"Status: {status}";

	private void UpdatePlayButtonState() {
		PlayButton.IsEnabled = _manifest is not null;
	}

	private void CloseDisplayWindow() {
		if (_displayWindow is null) return;
		_displayWindow.Close();
		_displayWindow = null;
	}

	private void CleanupAll() {
		if (_audioEngine is not null) {
			_audioEngine.PlaybackEnded -= OnPlaybackEnded;
			_audioEngine.Dispose();
			_audioEngine = null;
		}

		_videoEngine?.Dispose();
		_videoEngine = null;

		CloseDisplayWindow();

		_manifest        = null;
		_syncCoordinator = null;

		FolderLabel.Text = "No folder loaded";
		VideoLabel.Text  = "No video file selected";
		_videoFilePath   = null;
	}

	/// <inheritdoc />
	protected override void OnClosed(EventArgs e) {
		CleanupAll();
		base.OnClosed(e);
	}
}
