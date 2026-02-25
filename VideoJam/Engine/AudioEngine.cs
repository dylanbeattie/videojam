using System.Diagnostics;
using System.Windows;
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
internal sealed class AudioEngine : IDisposable {
	// ── Constants ────────────────────────────────────────────────────────────

	/// <summary>Target mix format: 44 100 Hz, 32-bit float, stereo.</summary>
	private static readonly WaveFormat MixFormat = WaveFormat.CreateIeeeFloatWaveFormat(44_100, 2);

	/// <summary>WASAPI shared-mode latency in milliseconds.</summary>
	private const int WasapiLatencyMs = 50;

	// ── State ────────────────────────────────────────────────────────────────

	private readonly List<IDisposable> _readers = [];
	private WasapiOut? _wasapiOut;
	private bool _stoppedExplicitly;
	private bool _disposed;

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
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (_wasapiOut is not null)
			throw new InvalidOperationException(
				"AudioEngine is already loaded. Call Stop() before loading a new song.");

		_stoppedExplicitly = false;

		var sampleProviders = new List<ISampleProvider>();

		foreach (AudioChannelManifest channel in manifest.AudioChannels) {
			ISampleProvider reader = CreateReader(channel.File);
			_readers.Add((IDisposable) reader);

			// Resample to the common mix format if needed.
			ISampleProvider resampled = EnsureMixFormat(reader);

			// Apply per-channel volume.
			float level = channelSettings.TryGetValue(channel.ChannelId, out ChannelSettings? settings)
				? settings.Level
				: 1.0f;

			var volumeProvider = new VolumeSampleProvider(resampled) { Volume = level };
			sampleProviders.Add(volumeProvider);
		}

		// If there are no channels, create a short silence so WasapiOut has something to drain.
		var mixer = sampleProviders.Count > 0
			? new MixingSampleProvider(sampleProviders)
			: new MixingSampleProvider(MixFormat);

		mixer.ReadFully = true; // Continue mixing after individual inputs end; stops when all are done.

		_wasapiOut = new WasapiOut(NAudio.CoreAudioApi.AudioClientShareMode.Shared, WasapiLatencyMs);
		_wasapiOut.PlaybackStopped += OnWasapiPlaybackStopped;
		_wasapiOut.Init(mixer);
	}

	/// <summary>
	/// Starts playback of all loaded stems simultaneously.
	/// </summary>
	/// <returns>
	/// A <see cref="Stopwatch.GetTimestamp"/> value captured immediately after
	/// <c>WasapiOut.Play()</c> returns, for use by <see cref="SyncCoordinator"/>.
	/// </returns>
	public long Play() {
		ObjectDisposedException.ThrowIf(_disposed, this);

		if (_wasapiOut is null)
			throw new InvalidOperationException("Call Load() before Play().");

		_wasapiOut.Play();
		return Stopwatch.GetTimestamp();
	}

	/// <summary>
	/// Stops playback and disposes all audio readers and the WASAPI device.
	/// Safe to call in any state.
	/// </summary>
	public void Stop() {
		if (_disposed) return;
		if (_wasapiOut is null) return; // Nothing loaded — nothing to stop.

		_stoppedExplicitly = true;

		_wasapiOut.PlaybackStopped -= OnWasapiPlaybackStopped;
		_wasapiOut.Stop();
		_wasapiOut.Dispose();
		_wasapiOut = null;

		foreach (IDisposable reader in _readers)
			reader.Dispose();
		_readers.Clear();
	}

	/// <inheritdoc />
	public void Dispose() {
		if (_disposed) return;
		Stop();
		_disposed = true;
	}

	// ── Private helpers ──────────────────────────────────────────────────────

	/// <summary>
	/// Creates the appropriate NAudio reader for the given file,
	/// based on its extension.
	/// </summary>
	private static ISampleProvider CreateReader(FileInfo file) {
		string ext = file.Extension;

		if (ext.Equals(".aiff", StringComparison.OrdinalIgnoreCase))
			return new AiffFileReader(file.FullName).ToSampleProvider();

		if (ext.Equals(".mp4", StringComparison.OrdinalIgnoreCase))
			return new MediaFoundationReader(file.FullName).ToSampleProvider();

		// .wav and .mp3 — AudioFileReader is NAudio's auto-detecting reader.
		return new AudioFileReader(file.FullName);
	}

	/// <summary>
	/// Wraps <paramref name="source"/> in a resampler if its format does not
	/// already match <see cref="MixFormat"/>.
	/// </summary>
	private static ISampleProvider EnsureMixFormat(ISampleProvider source) {
		WaveFormat fmt = source.WaveFormat;

		if (fmt.SampleRate == MixFormat.SampleRate &&
			fmt.Channels == MixFormat.Channels)
			return source;

		return new WdlResamplingSampleProvider(source, MixFormat.SampleRate);
	}

	/// <summary>
	/// Handles <c>WasapiOut.PlaybackStopped</c>. Raises <see cref="PlaybackEnded"/>
	/// on the UI thread if the stop was natural (not explicit).
	/// </summary>
	private void OnWasapiPlaybackStopped(object? sender, StoppedEventArgs e) {
		if (_stoppedExplicitly) return;

		// Marshal to the WPF UI thread before raising the event.
		Application.Current?.Dispatcher.InvokeAsync(() => PlaybackEnded?.Invoke(this, EventArgs.Empty));
	}
}