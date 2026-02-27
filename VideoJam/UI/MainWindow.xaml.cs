using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
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
	// ── Engines ───────────────────────────────────────────────────────────────

	private AudioEngine?     _audioEngine;
	private VideoEngine?     _videoEngine;
	private SyncCoordinator? _syncCoordinator;

	// ── Loaded data ───────────────────────────────────────────────────────────

	private SongManifest?                    _manifest;
	private Dictionary<int, VlcDisplayWindow> _displayWindows = [];
	private string?                          _videoFilePath;

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
			// Phase 3 harness: auto-route each unique suffix to a sequential display index.
			// First distinct suffix found → display 0, second → display 1, etc.
			// Real routing comes from the .show file in Phase 4.
			SongManifest rawManifest = SongScanner.Scan(folder);
			var autoRouting = new Dictionary<string, int>();
			int nextDisplayIndex = DisplayManager.PrimaryDisplayIndex;
			foreach (string suffix in rawManifest.VideoFiles.Select(v => v.Suffix).Distinct())
				autoRouting[suffix] = nextDisplayIndex++;

			_manifest = SongScanner.Scan(folder, displayRouting: autoRouting);

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
			using var loggerFactory = LoggerFactory.Create(b =>
				b.AddConsole().SetMinimumLevel(LogLevel.Debug));

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
				&& !_manifest.VideoFiles.Any(v => v.DisplayIndex == DisplayManager.PrimaryDisplayIndex)) {
				var injectedVideo = new VideoFileManifest(
					File: new FileInfo(_videoFilePath),
					DisplayIndex: DisplayManager.PrimaryDisplayIndex,
					Suffix: string.Empty);

				manifestForVideo = _manifest with {
					VideoFiles = _manifest.VideoFiles.Prepend(injectedVideo).ToList(),
				};
			}

			if (manifestForVideo.VideoFiles.Count > 0) {
				// Create one VlcDisplayWindow per distinct display index referenced by the manifest.
				foreach (int idx in DisplayManager.GetRequiredDisplayIndices(manifestForVideo))
					_displayWindows[idx] = DisplayManager.CreateWindowForDisplay(idx);

				await _videoEngine.LoadAll(manifestForVideo, _displayWindows);
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

	private void SetStatus(string status) =>
		StatusLabel.Text = $"Status: {status}";

	private void UpdatePlayButtonState() {
		PlayButton.IsEnabled = _manifest is not null;
	}

	private void CloseDisplayWindow() {
		foreach (VlcDisplayWindow window in _displayWindows.Values)
			window.Close();
		_displayWindows.Clear();
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
