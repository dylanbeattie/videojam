using System.Diagnostics;
using System.Windows;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using VideoJam.Model;

namespace VideoJam.Engine;

/// <summary>
/// Manages the NAudio audio pipeline for a single song.
/// Loads all audio stems and video audio tracks into a single WASAPI output
/// via a <see cref="MixingSampleProvider"/>, providing sample-accurate
/// inter-stem synchronisation by construction.
/// </summary>
/// <remarks>
/// <para>
/// Call <see cref="Load"/> to construct the pipeline, <see cref="Play"/> to start,
/// and <see cref="Stop"/> to halt and release all resources. <see cref="Stop"/> is
/// safe to call in any state (including before <see cref="Load"/> or after a
/// previous <see cref="Stop"/>).
/// </para>
/// <para>
/// The <see cref="PlaybackEnded"/> event is raised on the WPF UI thread when all
/// stems finish naturally. It is <b>not</b> raised after an explicit <see cref="Stop"/>.
/// </para>
/// </remarks>
internal sealed class AudioEngine : IDisposable, IAudioPlayback {
	// ── Constants ────────────────────────────────────────────────────────────

	/// <summary>Target mix sample rate in Hz.</summary>
	private const int MIX_SAMPLE_RATE = 44_100;

	/// <summary>Target mix channel count (stereo).</summary>
	private const int MIX_CHANNEL_COUNT = 2;

	/// <summary>Channel count for a mono source.</summary>
	private const int MONO_CHANNEL_COUNT = 1;

	/// <summary>Default per-channel volume level (unity gain) when no settings are provided.</summary>
	private const float DEFAULT_CHANNEL_LEVEL = 1.0f;

	/// <summary>Target mix format: 44 100 Hz, 32-bit float, stereo.</summary>
	private static readonly WaveFormat mixFormat =
		WaveFormat.CreateIeeeFloatWaveFormat(MIX_SAMPLE_RATE, MIX_CHANNEL_COUNT);

	/// <summary>WASAPI shared-mode latency in milliseconds.</summary>
	private const int WASAPI_LATENCY_MS = 50;

	// ── State ────────────────────────────────────────────────────────────────

	private readonly ILogger<AudioEngine> logger;
	private readonly List<IDisposable> readers = [];
	private readonly Dictionary<string, VolumeSampleProvider> _volumeProviders = [];
	private WasapiOut? wasapiOut;
	private bool stoppedExplicitly;
	private bool disposed;

	// ── Construction ─────────────────────────────────────────────────────────

	/// <summary>
	/// Initialises a new <see cref="AudioEngine"/>.
	/// </summary>
	/// <param name="logger">Logger for diagnostic and warning output.</param>
	public AudioEngine(ILogger<AudioEngine> logger) {
		this.logger = logger;
	}

	// ── Public API ───────────────────────────────────────────────────────────

	/// <summary>
	/// Raised on the WPF UI thread when all stems finish playing naturally.
	/// Not raised when <see cref="Stop"/> is called explicitly.
	/// </summary>
	public event EventHandler? PlaybackEnded;

	/// <summary>
	/// Constructs the NAudio pipeline from the supplied <paramref name="manifest"/>.
	/// Applies per-channel volume from <paramref name="channelSettings"/>
	/// (defaults to 1.0 for any channel not listed).
	/// Does <b>not</b> start playback.
	/// </summary>
	/// <param name="manifest">The song manifest produced by <see cref="Services.SongScanner"/>.</param>
	/// <param name="channelSettings">Per-channel volume/mute settings, keyed by channel ID.</param>
	/// <exception cref="InvalidOperationException">
	/// Thrown if <see cref="Load"/> is called while a previous pipeline is still active.
	/// Call <see cref="Stop"/> first.
	/// </exception>
	public void Load(
		SongManifest manifest,
		IReadOnlyDictionary<string, ChannelSettings> channelSettings) {
		ObjectDisposedException.ThrowIf(disposed, this);

		if (wasapiOut is not null)
			throw new InvalidOperationException(
				"AudioEngine is already loaded. Call Stop() before loading a new song.");

		stoppedExplicitly = false;

		var sampleProviders = new List<ISampleProvider>();

		foreach (var channel in manifest.AudioChannels) {
			AudioReader reader;
			try {
				reader = CreateReader(channel.File);
			}
			catch (Exception ex) {
				logger.LogWarning(ex,
					"Audio channel {ChannelId} ({File}) could not be loaded — the channel will be skipped.",
					channel.ChannelId, channel.File.Name);
				continue;
			}

			readers.Add(reader);

			// Resample to the common mix format if needed.
			var resampled = EnsureMixFormat(reader);

			// Apply per-channel volume, accounting for mute state.
			var settings = channelSettings.TryGetValue(channel.ChannelId, out var s) ? s : null;
			var level = (settings?.Muted ?? false) ? 0f : (settings?.Level ?? DEFAULT_CHANNEL_LEVEL);

			var volumeProvider = new VolumeSampleProvider(resampled) { Volume = level };
			_volumeProviders[channel.ChannelId] = volumeProvider;
			sampleProviders.Add(volumeProvider);
		}

		// If there are no channels, create a short silence so WasapiOut has something to drain.
		var mixer = sampleProviders.Count > 0
			? new MixingSampleProvider(sampleProviders)
			: new MixingSampleProvider(mixFormat);

		mixer.ReadFully = true; // Continue mixing after individual inputs end; stops when all are done.

		wasapiOut = new WasapiOut(NAudio.CoreAudioApi.AudioClientShareMode.Shared, WASAPI_LATENCY_MS);
		wasapiOut.PlaybackStopped += OnWasapiPlaybackStopped;
		wasapiOut.Init(mixer);
	}

	/// <summary>
	/// Starts playback of all loaded stems simultaneously.
	/// </summary>
	/// <returns>
	/// A <see cref="Stopwatch.GetTimestamp"/> value captured immediately after
	/// <c>WasapiOut.Play()</c> returns, for use by <see cref="SyncCoordinator"/>.
	/// </returns>
	public long Play() {
		ObjectDisposedException.ThrowIf(disposed, this);

		if (wasapiOut is null)
			throw new InvalidOperationException("Call Load() before Play().");

		wasapiOut.Play();
		return Stopwatch.GetTimestamp();
	}

	/// <summary>
	/// Stops playback and disposes all audio readers and the WASAPI device.
	/// Safe to call in any state.
	/// </summary>
	public void Stop() {
		if (disposed) return;
		if (wasapiOut is null) return; // Nothing loaded — nothing to stop.

		stoppedExplicitly = true;

		wasapiOut.PlaybackStopped -= OnWasapiPlaybackStopped;
		wasapiOut.Stop();
		wasapiOut.Dispose();
		wasapiOut = null;

		foreach (var reader in readers)
			reader.Dispose();
		readers.Clear();
		_volumeProviders.Clear();
	}

	/// <summary>
	/// Re-applies volume levels from the supplied channel settings to the active pipeline.
	/// Called immediately before playback starts so that any level/mute changes made
	/// after <see cref="Load"/> are picked up.
	/// </summary>
	/// <param name="channelSettings">Per-channel settings keyed by channel ID.</param>
	public void ApplyChannelSettings(IReadOnlyDictionary<string, ChannelSettings> channelSettings) {
		ObjectDisposedException.ThrowIf(disposed, this);
		foreach (var (channelId, provider) in _volumeProviders) {
			var settings = channelSettings.TryGetValue(channelId, out var s) ? s : null;
			provider.Volume = (settings?.Muted ?? false) ? 0f : (settings?.Level ?? DEFAULT_CHANNEL_LEVEL);
		}
	}

	/// <inheritdoc />
	public void Dispose() {
		if (disposed) return;
		Stop();
		disposed = true;
	}

	// ── Private helpers ──────────────────────────────────────────────────────

	/// <summary>
	/// Creates the appropriate NAudio reader for the given file,
	/// based on its extension.
	/// </summary>
	private static AudioReader CreateReader(FileInfo file) {
		var ext = file.Extension;

		if (ext.Equals(".aiff", StringComparison.OrdinalIgnoreCase)) {
			var aiffReader = new AiffFileReader(file.FullName);
			return new AudioReader(owner: aiffReader, source: aiffReader.ToSampleProvider());
		}

		if (ext.Equals(".mp4", StringComparison.OrdinalIgnoreCase)) {
			var mfReader = new MediaFoundationReader(file.FullName);
			return new AudioReader(owner: mfReader, source: mfReader.ToSampleProvider());
		}

		// .wav and .mp3 — AudioFileReader implements both IDisposable and ISampleProvider.
		var afReader = new AudioFileReader(file.FullName);
		return new AudioReader(owner: afReader, source: afReader);
	}

	/// <summary>
	/// Ensures <paramref name="source"/> matches <see cref="mixFormat"/> by
	/// applying sample-rate conversion and/or channel-count upmix as needed,
	/// each as a distinct step.
	/// </summary>
	/// <exception cref="NotSupportedException">
	/// Thrown if <paramref name="source"/> is stereo but <see cref="mixFormat"/> is mono,
	/// which is not a supported configuration for this application.
	/// </exception>
	private static ISampleProvider EnsureMixFormat(ISampleProvider source) {
		var result = source;

		// Step 1 — Resample if sample rate differs. WdlResamplingSampleProvider
		// preserves channel count; it does not perform upmixing.
		if (result.WaveFormat.SampleRate != mixFormat.SampleRate)
			result = new WdlResamplingSampleProvider(result, mixFormat.SampleRate);

		// Step 2 — Upmix mono → stereo if the mix format requires it.
		if (result.WaveFormat.Channels == MONO_CHANNEL_COUNT && mixFormat.Channels == MIX_CHANNEL_COUNT)
			result = new MonoToStereoSampleProvider(result);

		// Guard: a stereo source cannot be downmixed to a mono mix format here.
		if (result.WaveFormat.Channels > mixFormat.Channels)
			throw new NotSupportedException(
				$"Source has {result.WaveFormat.Channels} channels but the mix format has " +
				$"{mixFormat.Channels}. Downmixing is not supported.");

		return result;
	}

	/// <summary>
	/// Handles <c>WasapiOut.PlaybackStopped</c>. Raises <see cref="PlaybackEnded"/>
	/// on the UI thread if the stop was natural (not explicit).
	/// </summary>
	private void OnWasapiPlaybackStopped(object? sender, StoppedEventArgs e) {
		if (stoppedExplicitly) return;

		// Marshal to the WPF UI thread before raising the event.
		Application.Current?.Dispatcher.InvokeAsync(() => PlaybackEnded?.Invoke(this, EventArgs.Empty));
	}
}