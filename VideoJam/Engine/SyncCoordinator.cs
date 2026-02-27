using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace VideoJam.Engine;

/// <summary>
/// Orchestrates the start-time synchronisation sequence between
/// <see cref="AudioEngine"/> and <see cref="VideoEngine"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Start"/> fires <see cref="IAudioPlayback.Play"/> first, captures the returned
/// timestamp, then immediately fires <see cref="IVideoPlayback.Play"/> with that timestamp.
/// The measured interval between audio start and video dispatch is logged for diagnostics.
/// </para>
/// <para>
/// This class is stateless between invocations — it holds no song state, no timers,
/// and no event subscriptions. It is safe to call <see cref="Start"/> multiple times
/// on the same instance.
/// </para>
/// </remarks>
internal sealed class SyncCoordinator {
	// ── Constants ─────────────────────────────────────────────────────────────

	/// <summary>Log label for the audio-to-video dispatch interval measurement.</summary>
	private const string AvDispatchLabel = "A/V dispatch Δt";

	/// <summary>Conversion factor from Stopwatch ticks to milliseconds.</summary>
	private const double MillisecondsPerSecond = 1_000.0;

	// ── State ─────────────────────────────────────────────────────────────────

	private readonly ILogger<SyncCoordinator> _logger;

	// ── Construction ──────────────────────────────────────────────────────────

	/// <summary>
	/// Initialises a new <see cref="SyncCoordinator"/>.
	/// </summary>
	/// <param name="logger">Logger for diagnostic output.</param>
	public SyncCoordinator(ILogger<SyncCoordinator> logger) {
		_logger = logger;
	}

	// ── Public API ────────────────────────────────────────────────────────────

	/// <summary>
	/// Fires the audio engine, captures the start timestamp, then immediately fires
	/// the video engine — keeping the dispatch interval under 1 ms.
	/// </summary>
	/// <param name="audio">The loaded audio engine ready for playback.</param>
	/// <param name="video">The loaded video engine ready for playback.</param>
	public void Start(IAudioPlayback audio, IVideoPlayback video) {
		long tStart = audio.Play();

		video.Play(tStart);

		long   tEnd    = Stopwatch.GetTimestamp();
		double deltaMs = (tEnd - tStart) * MillisecondsPerSecond / Stopwatch.Frequency;

		_logger.LogDebug("{Label}: {DeltaMs:F2} ms", AvDispatchLabel, deltaMs);
	}
}
