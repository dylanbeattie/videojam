using System.Windows;
using Microsoft.Win32;
using VideoJam.Engine;
using VideoJam.Model;
using VideoJam.Services;

namespace VideoJam.UI;

/// <summary>
/// Phase 1 integration harness — a minimal WPF window for manual multi-stem sync verification.
/// This code-behind is temporary and will be replaced by the full MVVM operator UI in Phase 5.
/// </summary>
public partial class MainWindow : Window {
	private AudioEngine? _audioEngine;

	/// <inheritdoc />
	public MainWindow() {
		InitializeComponent();
	}

	// ── Button handlers ───────────────────────────────────────────────────────

	private void OnLoadClicked(object sender, RoutedEventArgs e) {
		// OpenFolderDialog is a native WPF folder picker (available since .NET 8).
		var dialog = new OpenFolderDialog {
			Title = "Select a song folder containing audio stems",
		};

		if (dialog.ShowDialog(this) != true)
			return;

		// Clean up any previously loaded engine before loading a new one.
		CleanupEngine();

		var folder = new DirectoryInfo(dialog.FolderName);

		try {
			SongManifest manifest = SongScanner.Scan(folder);

			// Build default ChannelSettings: stems at 1.0, video audio muted.
			var settings = new Dictionary<string, ChannelSettings>();
			foreach (AudioChannelManifest ch in manifest.AudioChannels) {
				settings[ch.ChannelId] = new ChannelSettings {
					Level = 1.0f,
					Muted = ch.Type == AudioChannelType.VideoAudio,
				};
			}

			_audioEngine = new AudioEngine();
			_audioEngine.PlaybackEnded += OnPlaybackEnded;
			_audioEngine.Load(manifest, settings);

			FolderLabel.Text = folder.FullName;
			SetStatus("Loaded");
			PlayButton.IsEnabled = true;
			StopButton.IsEnabled = false;
		}
		catch (Exception ex) {
			MessageBox.Show(
				$"Failed to load folder:\n\n{ex.Message}",
				"Load Error",
				MessageBoxButton.OK,
				MessageBoxImage.Error);
			CleanupEngine();
			SetStatus("Idle");
		}
	}

	private void OnPlayClicked(object sender, RoutedEventArgs e) {
		if (_audioEngine is null) return;

		try {
			_audioEngine.Play();
			SetStatus("Playing");
			PlayButton.IsEnabled = false;
			StopButton.IsEnabled = true;
			LoadButton.IsEnabled = false;
		}
		catch (Exception ex) {
			MessageBox.Show(
				$"Playback failed:\n\n{ex.Message}",
				"Playback Error",
				MessageBoxButton.OK,
				MessageBoxImage.Error);
		}
	}

	private void OnStopClicked(object sender, RoutedEventArgs e) {
		CleanupEngine();
		SetStatus("Stopped");
		FolderLabel.Text = "No folder loaded";
		PlayButton.IsEnabled = false;
		StopButton.IsEnabled = false;
		LoadButton.IsEnabled = true;
	}

	// ── PlaybackEnded callback (marshalled to UI thread by AudioEngine) ────────

	private void OnPlaybackEnded(object? sender, EventArgs e) {
		CleanupEngine();
		SetStatus("Stopped");
		FolderLabel.Text = "No folder loaded";
		PlayButton.IsEnabled = false;
		StopButton.IsEnabled = false;
		LoadButton.IsEnabled = true;
	}

	// ── Helpers ───────────────────────────────────────────────────────────────

	private void SetStatus(string status) =>
		StatusLabel.Text = $"Status: {status}";

	private void CleanupEngine() {
		if (_audioEngine is null) return;
		_audioEngine.PlaybackEnded -= OnPlaybackEnded;
		_audioEngine.Dispose();
		_audioEngine = null;
	}

	/// <inheritdoc />
	protected override void OnClosed(EventArgs e) {
		CleanupEngine();
		base.OnClosed(e);
	}
}