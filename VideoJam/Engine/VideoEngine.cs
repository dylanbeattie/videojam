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
	private const int PRE_BUFFER_TIMEOUT_MS = 2_000;

	/// <summary>VLC option: disable all audio output.</summary>
	private const string NO_AUDIO = "--no-audio";

	/// <summary>VLC option: disable on-screen display overlays.</summary>
	private const string NO_OSD = "--no-osd";

	// ── State ─────────────────────────────────────────────────────────────────

	private readonly ILogger<VideoEngine> logger;

	/// <summary>
	/// Shared LibVLC instance. Created once with <see cref="NO_AUDIO"/> and <see cref="NO_OSD"/>.
	/// Must be disposed after all <see cref="MediaPlayer"/> instances.
	/// </summary>
	private readonly LibVLC libVlc;

	/// <summary>
	/// Active slots: one per successfully pre-buffered video file.
	/// Each slot owns its <see cref="MediaPlayer"/> and the associated display window.
	/// Access is guarded by <see cref="slotsLock"/> because Phase 3 may call
	/// <see cref="Load"/> concurrently for multiple displays.
	/// </summary>
	private readonly List<ActiveSlot> slots = [];
	private readonly object slotsLock = new();

	private bool disposed;

	// ── Construction ──────────────────────────────────────────────────────────

	/// <summary>
	/// Initialises a new <see cref="VideoEngine"/> and creates the shared <see cref="LibVLC"/> instance.
	/// </summary>
	/// <param name="logger">Logger for diagnostic and warning output.</param>
	public VideoEngine(ILogger<VideoEngine> logger) {
		this.logger = logger;
		libVlc = new LibVLC(NO_AUDIO, NO_OSD);
	}

	// ── Public API ────────────────────────────────────────────────────────────

	/// <summary>
	/// Loads the video file for <paramref name="displayIndex"/> from <paramref name="manifest"/>
	/// onto <paramref name="window"/>, pre-buffering it so <see cref="Play"/> can start instantly.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Pre-buffering works by calling <c>MediaPlayer.Play()</c>, waiting for the
	/// <c>Playing</c> state event (VLC decoder pipeline warm), calling <c>SetPause(true)</c>
	/// from within the <c>Playing</c> handler, then waiting for the <c>Paused</c> event
	/// (up to <see cref="PRE_BUFFER_TIMEOUT_MS"/> ms total), and finally seeking back to
	/// position 0. This race-free sequence eliminates the concurrent-load failure where
	/// <c>SetPause(true)</c> was called while VLC was still in <c>Opening</c> state and
	/// silently ignored the request.
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

		ObjectDisposedException.ThrowIf(disposed, this);

		var videoFile = manifest.VideoFiles
			.FirstOrDefault(v => v.DisplayIndex == displayIndex);

		if (videoFile is null) {
			logger.LogDebug(
				"No video file in manifest for display index {DisplayIndex}; window state unchanged.",
				displayIndex);
			return;
		}

		logger.LogInformation(
			"Loading video file {File} for display {DisplayIndex}.",
			videoFile.File.Name, displayIndex);

		var player = new MediaPlayer(libVlc);

		// Render directly into the VlcDisplayWindow's HWND.
		player.Hwnd = window.Hwnd;

		using var media = new Media(libVlc, videoFile.File.FullName, FromType.FromPath);
		player.Media = media;

		// ── Pre-buffer sequence ───────────────────────────────────────────────
		// Subscribe to Playing and Paused BEFORE calling Play() to avoid a race where
		// VLC fires Playing before our handler is attached.
		//
		// Step 1: Play() kicks off VLC's async decoder pipeline.
		// Step 2: OnPlayerPlaying fires on a VLC internal thread once the decoder is warm.
		//         It calls SetPause(true) — now safe because VLC is in Playing state —
		//         then unsubscribes itself so it does not fire again.
		// Step 3: OnPlayerPaused fires on a VLC internal thread once the pause is confirmed.
		//         It signals the TCS, unblocking the await below.
		// Step 4: We seek to position 0 and register the slot.
		var prebufferTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

		player.Playing += OnPlayerPlaying;
		player.Paused  += OnPlayerPaused;

		using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeoutCts.CancelAfter(PRE_BUFFER_TIMEOUT_MS);

		bool prebufferSucceeded;
		try {
			player.Play();
			await prebufferTcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
			prebufferSucceeded = true;
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
			// Timed out (not cancelled by the caller) — abort this slot.
			logger.LogWarning(
				"Pre-buffer for {File} did not complete within {TimeoutMs} ms. " +
				"Display {DisplayIndex} will show its fallback image during playback.",
				videoFile.File.Name, PRE_BUFFER_TIMEOUT_MS, displayIndex);
			prebufferSucceeded = false;
		}
		catch (Exception ex) when (ex is not OperationCanceledException) {
			// LibVLC raised an error (e.g. file not found, codec failure) — treat as a graceful
			// pre-buffer failure so a single bad file does not abort the entire playback session.
			logger.LogWarning(ex,
				"Pre-buffer for {File} on display {DisplayIndex} failed with an exception. " +
				"The display will show its fallback image during playback.",
				videoFile.File.Name, displayIndex);
			prebufferSucceeded = false;
		}
		finally {
			// Always unsubscribe both handlers; they may not have fired yet on timeout/cancellation.
			player.Playing -= OnPlayerPlaying;
			player.Paused  -= OnPlayerPaused;
		}

		if (!prebufferSucceeded) {
			player.Dispose();
			return;
		}

		// Seek back to the start so Play() begins from frame 0.
		player.Time = 0;

		// Switch the window to video mode.
		window.Dispatcher.Invoke(window.ShowVideo);

		lock (slotsLock)
			slots.Add(new ActiveSlot(player, window));

		logger.LogDebug(
			"Video pre-buffer complete for {File} on display {DisplayIndex}.",
			videoFile.File.Name, displayIndex);

		// ── Local event handlers (VLC internal thread) ────────────────────────

		// Called when VLC reaches Playing state — decoder pipeline is warm and a pause
		// request will be honoured. Immediately gates SetPause(true) and unsubscribes
		// itself so subsequent Play() calls on this player do not re-trigger it.
		void OnPlayerPlaying(object? s, EventArgs e) {
			player.Playing -= OnPlayerPlaying;
			player.SetPause(true);
		}

		// Called when VLC confirms it has paused — signals the awaitable TCS.
		void OnPlayerPaused(object? s, EventArgs e) => prebufferTcs.TrySetResult(true);
	}

	/// <summary>
	/// Loads video files for all display slots in <paramref name="windows"/> concurrently,
	/// awaiting all pre-buffer operations via <see cref="Task.WhenAll"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Each entry in <paramref name="windows"/> is passed as a separate <see cref="Load"/>
	/// call dispatched in parallel. Partial failure is handled gracefully: if a <see cref="Load"/>
	/// times out during pre-buffering it returns without registering a slot (per the
	/// pre-buffer timeout contract) and <see cref="LoadAll"/> still completes normally.
	/// An unhandled exception from any slot propagates out of <see cref="LoadAll"/>.
	/// </para>
	/// <para>
	/// If <paramref name="windows"/> is empty, the method completes immediately without error.
	/// </para>
	/// </remarks>
	/// <param name="manifest">The song manifest produced by <c>SongScanner</c>.</param>
	/// <param name="windows">
	/// Map of display index → <see cref="VlcDisplayWindow"/>. One <see cref="Load"/> call
	/// is dispatched per entry.
	/// </param>
	/// <param name="cancellationToken">Cancellation token propagated to every <see cref="Load"/> call.</param>
	public Task LoadAll(
		SongManifest manifest,
		IReadOnlyDictionary<int, VlcDisplayWindow> windows,
		CancellationToken cancellationToken = default) {

		ObjectDisposedException.ThrowIf(disposed, this);

		var tasks = windows
			.Select(kvp => Load(manifest, kvp.Key, kvp.Value, cancellationToken))
			.ToArray();

		return Task.WhenAll(tasks);
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
		ObjectDisposedException.ThrowIf(disposed, this);

		IReadOnlyList<ActiveSlot> snapshot;
		lock (slotsLock)
			snapshot = slots.ToList();

		foreach (var slot in snapshot)
			slot.Player.Play();

		logger.LogDebug("Dispatched {Count} MediaPlayer(s).", snapshot.Count);
	}

	/// <summary>
	/// Stops all active <see cref="MediaPlayer"/> instances, disposes them, and
	/// reverts every managed <see cref="VlcDisplayWindow"/> to its fallback state.
	/// Safe to call in any state.
	/// </summary>
	public void Stop() {
		if (disposed) return;

		List<ActiveSlot> slots;
		lock (slotsLock) {
			slots = [.. this.slots];
			this.slots.Clear();
		}

		foreach (var slot in slots) {
			slot.Player.Stop();
			slot.Player.Dispose();
			slot.Window.Dispatcher.Invoke(() => slot.Window.ShowFallback(null));
		}

		logger.LogDebug("VideoEngine stopped; all displays reverted to fallback.");
	}

	/// <inheritdoc />
	public void Dispose() {
		if (disposed) return;
		disposed = true;

		// Stop and dispose all MediaPlayer instances BEFORE disposing LibVLC.
		// Disposing LibVLC while a MediaPlayer is active can crash the native thread.
		Stop();

		libVlc.Dispose();

		logger.LogDebug("VideoEngine disposed.");
	}

	// ── Private types ─────────────────────────────────────────────────────────

	/// <summary>Represents a loaded and pre-buffered video slot.</summary>
	private sealed record ActiveSlot(MediaPlayer Player, VlcDisplayWindow Window);
}