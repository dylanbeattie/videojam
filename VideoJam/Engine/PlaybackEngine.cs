using System.Windows;
using Microsoft.Extensions.Logging;
using VideoJam.Model;
using VideoJam.Services;
using VideoJam.UI;

namespace VideoJam.Engine;

/// <summary>
/// Central controller for a performance session.
/// Owns the playback state machine and coordinates <see cref="AudioEngine"/>,
/// <see cref="VideoEngine"/>, and <see cref="SyncCoordinator"/>.
/// </summary>
/// <remarks>
/// <para>
/// All state mutations and event notifications are marshalled to the WPF UI thread.
/// The <see cref="StateChanged"/> event is always raised on the UI thread.
/// </para>
/// <para>
/// The <see cref="VideoEngine"/> and <see cref="SyncCoordinator"/> are injected and
/// long-lived. A new <see cref="AudioEngine"/> is created per-song in <see cref="Cue"/>.
/// </para>
/// <para>
/// <see cref="VlcDisplayWindow"/> instances are created lazily on first cue and reused
/// across songs to avoid display flicker.
/// </para>
/// </remarks>
internal sealed class PlaybackEngine : IDisposable {
	// ── State ─────────────────────────────────────────────────────────────────

	private Show _show;
	private readonly VideoEngine _videoEngine;
	private readonly SyncCoordinator _syncCoordinator;
	private UI.MainWindow _mainWindow;
	private readonly ILoggerFactory _loggerFactory;

	/// <summary>
	/// Display windows keyed by display index, created lazily on first cue and
	/// reused across songs to avoid display flicker.
	/// </summary>
	private readonly Dictionary<int, VlcDisplayWindow> _displayWindows = [];

	/// <summary>The current per-song audio engine; <see langword="null"/> until first cue.</summary>
	private AudioEngine? _audioEngine;

	/// <summary>Cancellation source for the in-progress Cue() operation.</summary>
	private CancellationTokenSource? _cueCts;

	private PlaybackState _state = PlaybackState.Idle;
	private int _cuedSongIndex = -1;
	private bool _disposed;

	// ── Construction ──────────────────────────────────────────────────────────

	/// <summary>
	/// Initialises a new <see cref="PlaybackEngine"/>.
	/// </summary>
	/// <param name="show">The loaded show providing the setlist and global routing.</param>
	/// <param name="videoEngine">The long-lived video engine.</param>
	/// <param name="syncCoordinator">The A/V sync coordinator.</param>
	/// <param name="mainWindow">The operator window — always visible; never hidden during playback. May be set later via <see cref="SetMainWindow"/>.</param>
	/// <param name="loggerFactory">Factory for creating per-song <see cref="AudioEngine"/> loggers.</param>
	public PlaybackEngine(
		Show show,
		VideoEngine videoEngine,
		SyncCoordinator syncCoordinator,
		UI.MainWindow? mainWindow,
		ILoggerFactory loggerFactory) {
		_show = show;
		_videoEngine = videoEngine;
		_syncCoordinator = syncCoordinator;
		_mainWindow = mainWindow!;
		_loggerFactory = loggerFactory;
	}

	// ── Public API ────────────────────────────────────────────────────────────

	/// <summary>
	/// Raised on the WPF UI thread whenever <see cref="State"/> changes.
	/// </summary>
	public event EventHandler? StateChanged;

	/// <summary>
	/// Sets the operator <see cref="UI.MainWindow"/> reference.
	/// Must be called once after window construction, before any cue operations.
	/// </summary>
	/// <param name="mainWindow">The operator window.</param>
	public void SetMainWindow(UI.MainWindow mainWindow) {
		_mainWindow = mainWindow;
	}

	/// <summary>
	/// Updates the show reference. Call this when the operator loads a new show.
	/// The engine is reset to <see cref="PlaybackState.Idle"/> as part of the update.
	/// </summary>
	/// <param name="show">The newly loaded show.</param>
	public void UpdateShow(Show show) {
		ObjectDisposedException.ThrowIf(_disposed, this);

		// Cancel any pending cue and tear down resources from the old show.
		_cueCts?.Cancel();
		_cueCts?.Dispose();
		_cueCts = null;

		if (_audioEngine is not null) {
			_audioEngine.PlaybackEnded -= OnPlaybackEnded;
			_audioEngine.Dispose();
			_audioEngine = null;
		}

		_videoEngine.Stop();
		_cuedSongIndex = -1;
		_show = show;
		SetState(PlaybackState.Idle);
	}

	/// <summary>Current playback state machine state.</summary>
	public PlaybackState State => _state;

	/// <summary>
	/// 0-based index into <see cref="Show.Songs"/> of the currently cued song,
	/// or -1 when no song is cued.
	/// </summary>
	public int CuedSongIndex => _cuedSongIndex;

	/// <summary>
	/// Starts playback of the currently cued song.
	/// Fires the A/V sync sequence and brings all display windows to the foreground via
	/// <see cref="Window.Activate"/>. <see cref="MainWindow"/> is never hidden; the operator
	/// UI remains accessible throughout playback.
	/// </summary>
	/// <exception cref="InvalidOperationException">
	/// Thrown when <see cref="State"/> is not <see cref="PlaybackState.Cued"/>.
	/// </exception>
	public void Go() {
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (_state != PlaybackState.Cued)
			throw new InvalidOperationException(
				$"Go() requires State == Cued; current state is {_state}.");

		// Subscribe before starting so we never miss the end event.
		_audioEngine!.PlaybackEnded += OnPlaybackEnded;

		_syncCoordinator.Start(_audioEngine!, _videoEngine);

		// Bring all display windows to the front so video is visible without hiding MainWindow.
		foreach (var window in _displayWindows.Values)
			window.Dispatcher.Invoke(window.Activate);

		SetState(PlaybackState.Playing);
	}

	/// <summary>
	/// Two-phase stop:
	/// <list type="bullet">
	///   <item>Playing → Paused: stops audio, pauses video.</item>
	///   <item>Paused → Cued: stops video, activates the operator window, re-cues the current song.</item>
	/// </list>
	/// <see cref="MainWindow"/> is never hidden; <see cref="Window.Activate"/> is used to
	/// return keyboard focus to the operator UI on phase 2.
	/// </summary>
	/// <exception cref="InvalidOperationException">
	/// Thrown when <see cref="State"/> is not <see cref="PlaybackState.Playing"/> or
	/// <see cref="PlaybackState.Paused"/>.
	/// </exception>
	public void StopAndRewind() {
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (_state == PlaybackState.Playing) {
			// Phase 1: Playing → Paused.
			// Unsubscribe so the natural-end handler does not fire after an explicit stop.
			if (_audioEngine is not null)
				_audioEngine.PlaybackEnded -= OnPlaybackEnded;

			_audioEngine?.Stop();

			// VideoEngine has no Pause method — we stop it and will reload on the next Cue.
			// The displays remain in their current visual state because Stop() reverts to fallback.
			// Per spec: "pause video players" — Stop is the mechanism available here.
			_videoEngine.Stop();

			SetState(PlaybackState.Paused);
			return;
		}

		if (_state == PlaybackState.Paused) {
			// Phase 2: Paused → Cued. Reload the same song.
			var indexToReload = _cuedSongIndex;

			// Return focus to the operator UI; MainWindow is already visible.
			_mainWindow.Activate();

			// Fire-and-forget; CueSong will set State = Cued when complete.
			_ = Cue(indexToReload);
			return;
		}

		throw new InvalidOperationException(
			$"StopAndRewind() requires State == Playing or Paused; current state is {_state}.");
	}

	/// <summary>
	/// Prepares the song at <paramref name="songIndex"/> for immediate playback:
	/// disposes the existing audio engine, stops and clears the video engine,
	/// scans the song folder, loads audio, pre-buffers all video files concurrently,
	/// then sets <see cref="State"/> to <see cref="PlaybackState.Cued"/>.
	/// </summary>
	/// <param name="songIndex">0-based index into <see cref="Show.Songs"/>.</param>
	/// <param name="ct">Optional cancellation token.</param>
	public async Task Cue(int songIndex, CancellationToken ct = default) {
		ObjectDisposedException.ThrowIf(_disposed, this);

		// Cancel any in-progress cue operation.
		_cueCts?.Cancel();
		_cueCts?.Dispose();
		_cueCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
		var linkedCt = _cueCts.Token;

		var song = _show.Songs[songIndex];

		try {
			// Tear down existing resources.
			if (_audioEngine is not null) {
				_audioEngine.PlaybackEnded -= OnPlaybackEnded;
				_audioEngine.Dispose();
				_audioEngine = null;
			}

			_videoEngine.Stop();

			linkedCt.ThrowIfCancellationRequested();

			// Resolve and scan the song folder.
			// FolderPath is absolute in memory (normalised on load per MainViewModel.NormalizeLoadedPaths).
			var folder = new DirectoryInfo(song.FolderPath);

			// Build merged routing: global + per-song overrides.
			var routing = BuildRouting(song);

			var manifest = SongScanner.Scan(folder, routing);

			linkedCt.ThrowIfCancellationRequested();

			// Ensure display windows exist for every required display index.
			EnsureDisplayWindows(manifest);

			// Load audio.
			_audioEngine = new AudioEngine(_loggerFactory.CreateLogger<AudioEngine>());
			_audioEngine.Load(manifest, song.Channels);

			linkedCt.ThrowIfCancellationRequested();

			// Load all video concurrently.
			await _videoEngine.LoadAll(manifest, _displayWindows, linkedCt).ConfigureAwait(true);

			linkedCt.ThrowIfCancellationRequested();

			_cuedSongIndex = songIndex;
			SetState(PlaybackState.Cued);
		}
		catch (OperationCanceledException) {
			// Cue was superseded by a newer call — silently swallow.
		}
		catch {
			// Any non-cancellation failure (scan, audio load, video load) must leave the
			// engine in Idle so the caller is not stuck in a broken intermediate state.
			SetState(PlaybackState.Idle);
			throw;
		}
	}

	/// <inheritdoc />
	public void Dispose() {
		if (_disposed) return;
		_disposed = true;

		_cueCts?.Cancel();
		_cueCts?.Dispose();
		_cueCts = null;

		if (_audioEngine is not null) {
			_audioEngine.PlaybackEnded -= OnPlaybackEnded;
			_audioEngine.Dispose();
			_audioEngine = null;
		}

		_videoEngine.Stop();

		foreach (var window in _displayWindows.Values)
			window.Dispatcher.Invoke(window.Close);

		_displayWindows.Clear();
	}

	// ── Private helpers ───────────────────────────────────────────────────────

	/// <summary>
	/// Handles <see cref="AudioEngine.PlaybackEnded"/>: stops video, activates the operator
	/// window, then automatically cues the next song (or returns to Idle if the setlist is
	/// exhausted). <see cref="MainWindow"/> is never hidden, so only <see cref="Window.Activate"/>
	/// is needed to return keyboard focus to the operator UI.
	/// </summary>
	private void OnPlaybackEnded(object? sender, EventArgs e) {
		// Already on UI thread (AudioEngine marshals this event).
		if (_audioEngine is not null)
			_audioEngine.PlaybackEnded -= OnPlaybackEnded;

		_audioEngine?.Dispose();
		_audioEngine = null;

		_videoEngine.Stop();

		_mainWindow.Activate();

		var nextIndex = _cuedSongIndex + 1;
		if (nextIndex < _show.Songs.Count) {
			// Fire-and-forget auto-advance to the next song.
			_ = Cue(nextIndex);
		} else {
			_cuedSongIndex = -1;
			SetState(PlaybackState.Idle);
		}
	}

	/// <summary>
	/// Builds the effective display routing for a song by merging global routing
	/// with per-song overrides (per-song takes precedence).
	/// </summary>
	private IReadOnlyDictionary<string, int> BuildRouting(SongEntry song) {
		var merged = new Dictionary<string, int>(_show.GlobalDisplayRouting);
		foreach (var kvp in song.DisplayRoutingOverrides)
			merged[kvp.Key] = kvp.Value;
		return merged;
	}

	/// <summary>
	/// Creates <see cref="VlcDisplayWindow"/> instances for any display indices in
	/// <paramref name="manifest"/> that do not yet have a window.
	/// </summary>
	private void EnsureDisplayWindows(SongManifest manifest) {
		var required = DisplayManager.GetRequiredDisplayIndices(manifest);
		foreach (var displayIndex in required) {
			if (!_displayWindows.ContainsKey(displayIndex)) {
				var window = DisplayManager.CreateWindowForDisplay(displayIndex);
				_displayWindows[displayIndex] = window;
			}
		}
	}

	/// <summary>
	/// Sets <see cref="State"/> and raises <see cref="StateChanged"/> on the UI thread.
	/// </summary>
	private void SetState(PlaybackState newState) {
		_state = newState;
		var dispatcher = Application.Current?.Dispatcher;
		if (dispatcher is not null && !dispatcher.CheckAccess())
			dispatcher.InvokeAsync(() => StateChanged?.Invoke(this, EventArgs.Empty));
		else
			StateChanged?.Invoke(this, EventArgs.Empty);
	}
}
