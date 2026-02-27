using System.Collections.Immutable;
using System.Diagnostics;
using LibVLCSharp.Shared;
using Microsoft.Extensions.Logging;
using VideoJam.Model;
using VideoJam.UI;

namespace VideoJam.Engine;

/// <summary>
/// Manages LibVLC instances and <see cref="VlcDisplayWindow"/> objects for video playback.
/// </summary>
/// <remarks>
/// <para>
/// A single <see cref="LibVLC"/> instance is shared across all <see cref="MediaPlayer"/>
/// instances managed by this engine. The instance is created with <c>--no-audio</c> so
/// LibVLC never opens a Windows audio device — all audio is routed through
/// <see cref="AudioEngine"/> via NAudio.
/// </para>
/// <para>
/// Call <see cref="Load"/> to load and pre-buffer a video file onto a display,
/// <see cref="Play"/> to start playback in sync with the audio engine, and
/// <see cref="Stop"/> to halt playback and revert displays to their fallback state.
/// </para>
/// </remarks>
internal sealed class VideoEngine : IDisposable, IVideoPlayback {
	// ── Constants ─────────────────────────────────────────────────────────────

	/// <summary>Milliseconds to wait for the pre-buffer Paused event before giving up.</summary>
	private const int PreBufferTimeoutMs = 2_000;

	/// <summary>VLC option: disable all audio output.</summary>
	private const string NoAudio = "--no-audio";

	/// <summary>VLC option: disable on-screen display overlays.</summary>
	private const string NoOsd = "--no-osd";

	// ── State ─────────────────────────────────────────────────────────────────

	private readonly ILogger<VideoEngine> _logger;

	/// <summary>
	/// Shared LibVLC instance. Created once with <see cref="NoAudio"/> and <see cref="NoOsd"/>.
	/// Must be disposed after all <see cref="MediaPlayer"/> instances.
	/// </summary>
	private readonly LibVLC _libVlc;

	/// <summary>
	/// Active slots: one per successfully pre-buffered video file.
	/// Each slot owns its <see cref="MediaPlayer"/> and the associated display window.
	/// Access is guarded by <see cref="_slotsLock"/> because Phase 3 may call
	/// <see cref="Load"/> concurrently for multiple displays.
	/// </summary>
	private readonly List<ActiveSlot> _slots = [];
	private readonly object _slotsLock = new();

	private bool _disposed;

	// ── Construction ──────────────────────────────────────────────────────────

	/// <summary>
	/// Initialises a new <see cref="VideoEngine"/> and creates the shared <see cref="LibVLC"/> instance.
	/// </summary>
	/// <param name="logger">Logger for diagnostic and warning output.</param>
	public VideoEngine(ILogger<VideoEngine> logger) {
		_logger = logger;
		_libVlc = new LibVLC(NoAudio, NoOsd);
	}

	// ── Public API ────────────────────────────────────────────────────────────

	/// <summary>
	/// Loads the video file for <paramref name="displayIndex"/> from <paramref name="manifest"/>
	/// onto <paramref name="window"/>, pre-buffering it so <see cref="Play"/> can start instantly.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Pre-buffering works by calling <c>MediaPlayer.Play()</c>, waiting for the
	/// <c>Paused</c> state event (up to <see cref="PreBufferTimeoutMs"/> ms), then seeking
	/// back to position 0. This primes the VLC decoder pipeline, eliminating the cold-start
	/// latency (~150–400 ms) from the A/V sync path.
	/// </para>
	/// <para>
	/// If the pre-buffer times out, the player is disposed and the method returns without
	/// registering a slot — the window remains in its current (fallback) state. The GO
	/// button may still be pressed; audio will play but the display will show only the
	/// fallback image for this song.
	/// </para>
	/// <para>
	/// If no video file in <paramref name="manifest"/> targets <paramref name="displayIndex"/>,
	/// the method returns immediately without modifying the window state.
	/// </para>
	/// </remarks>
	/// <param name="manifest">The song manifest produced by <c>SongScanner</c>.</param>
	/// <param name="displayIndex">The display index to target (0 = primary display).</param>
	/// <param name="window">The <see cref="VlcDisplayWindow"/> associated with this display.</param>
	/// <param name="cancellationToken">Cancellation token for the async operation.</param>
	public async Task Load(
		SongManifest manifest,
		int displayIndex,
		VlcDisplayWindow window,
		CancellationToken cancellationToken = default) {

		ObjectDisposedException.ThrowIf(_disposed, this);

		VideoFileManifest? videoFile = manifest.VideoFiles
			.FirstOrDefault(v => v.DisplayIndex == displayIndex);

		if (videoFile is null) {
			_logger.LogDebug(
				"No video file in manifest for display index {DisplayIndex}; window state unchanged.",
				displayIndex);
			return;
		}

		_logger.LogInformation(
			"Loading video file {File} for display {DisplayIndex}.",
			videoFile.File.Name, displayIndex);

		var player = new MediaPlayer(_libVlc);

		// Render directly into the VlcDisplayWindow's HWND.
		player.Hwnd = window.Hwnd;

		using var media = new Media(_libVlc, videoFile.File.FullName, FromType.FromPath);
		player.Media = media;

		// ── Pre-buffer sequence ───────────────────────────────────────────────
		// Play → wait for Paused event (decoder primed) → seek to 0.
		var prebufferTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

		player.Paused += OnPlayerPaused;

		player.Play();
		// Immediately pause so VLC primes the decoder but does not run the clock.
		player.SetPause(true);

		using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeoutCts.CancelAfter(PreBufferTimeoutMs);

		bool prebufferSucceeded;
		try {
			await prebufferTcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
			prebufferSucceeded = true;
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
			// Timed out (not cancelled by the caller) — abort this slot.
			prebufferSucceeded = false;
		}
		finally {
			player.Paused -= OnPlayerPaused;
		}

		if (!prebufferSucceeded) {
			// Pre-buffer did not complete in time. Dispose the player and leave the window
			// in fallback state. Audio will still play; this display will show its fallback image.
			_logger.LogWarning(
				"Pre-buffer for {File} did not complete within {TimeoutMs} ms. " +
				"Display {DisplayIndex} will show its fallback image during playback.",
				videoFile.File.Name, PreBufferTimeoutMs, displayIndex);
			player.Dispose();
			return;
		}

		// Seek back to the start so Play() begins from frame 0.
		player.Time = 0;

		// Switch the window to video mode.
		window.Dispatcher.Invoke(window.ShowVideo);

		lock (_slotsLock)
			_slots.Add(new ActiveSlot(player, window));

		_logger.LogDebug(
			"Video pre-buffer complete for {File} on display {DisplayIndex}.",
			videoFile.File.Name, displayIndex);

		// Local helper — signal the TCS from the VLC event thread.
		void OnPlayerPaused(object? s, EventArgs e) => prebufferTcs.TrySetResult(true);
	}

	/// <summary>
	/// Starts playback on all active (pre-buffered) <see cref="MediaPlayer"/> instances.
	/// </summary>
	/// <param name="audioStartTimestamp">
	/// The <see cref="Stopwatch.GetTimestamp"/> value captured by
	/// <see cref="AudioEngine.Play"/> immediately after audio started.
	/// Used internally by <see cref="SyncCoordinator"/> for Δt logging.
	/// </param>
	public void Play(long audioStartTimestamp) {
		ObjectDisposedException.ThrowIf(_disposed, this);

		IReadOnlyList<ActiveSlot> snapshot;
		lock (_slotsLock)
			snapshot = _slots.ToList();

		foreach (ActiveSlot slot in snapshot)
			slot.Player.Play();

		_logger.LogDebug("Dispatched {Count} MediaPlayer(s).", snapshot.Count);
	}

	/// <summary>
	/// Stops all active <see cref="MediaPlayer"/> instances, disposes them, and
	/// reverts every managed <see cref="VlcDisplayWindow"/> to its fallback state.
	/// Safe to call in any state.
	/// </summary>
	public void Stop() {
		if (_disposed) return;

		List<ActiveSlot> slots;
		lock (_slotsLock) {
			slots = [.. _slots];
			_slots.Clear();
		}

		foreach (ActiveSlot slot in slots) {
			slot.Player.Stop();
			slot.Player.Dispose();
			slot.Window.Dispatcher.Invoke(() => slot.Window.ShowFallback(null));
		}

		_logger.LogDebug("VideoEngine stopped; all displays reverted to fallback.");
	}

	/// <inheritdoc />
	public void Dispose() {
		if (_disposed) return;
		_disposed = true;

		// Stop and dispose all MediaPlayer instances BEFORE disposing LibVLC.
		// Disposing LibVLC while a MediaPlayer is active can crash the native thread.
		Stop();

		_libVlc.Dispose();

		_logger.LogDebug("VideoEngine disposed.");
	}

	// ── Private types ─────────────────────────────────────────────────────────

	/// <summary>Represents a loaded and pre-buffered video slot.</summary>
	private sealed record ActiveSlot(MediaPlayer Player, VlcDisplayWindow Window);
}
