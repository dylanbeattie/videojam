using NAudio.Wave;
using VideoJam.Engine;

namespace VideoJam.Tests;

/// <summary>
/// Unit tests for <see cref="AudioReader"/>.
///
/// <see cref="AudioReader"/> is the thin wrapper that pairs an <see cref="IDisposable"/>
/// resource owner with an <see cref="ISampleProvider"/> sample source. The two may be the
/// same object (WAV/MP3) or different objects (AIFF/MP4, where <c>ToSampleProvider()</c>
/// returns a non-disposable wrapper). All tests use hand-rolled stubs — no mocking library
/// is present in this project.
/// </summary>
public sealed class AudioReaderTests {
	// ── Stubs ─────────────────────────────────────────────────────────────────

	/// <summary>
	/// A minimal <see cref="ISampleProvider"/> stub whose <see cref="WaveFormat"/> and
	/// <see cref="Read"/> behaviour can be configured at construction time.
	/// </summary>
	private sealed class StubSampleProvider : ISampleProvider {
		private readonly float[] _samples;

		public StubSampleProvider(WaveFormat format, float[] samples) {
			WaveFormat = format;
			_samples   = samples;
		}

		public WaveFormat WaveFormat { get; }

		public int Read(float[] buffer, int offset, int count) {
			int toCopy = Math.Min(count, _samples.Length);
			Array.Copy(_samples, 0, buffer, offset, toCopy);
			return toCopy;
		}
	}

	/// <summary>
	/// An <see cref="IDisposable"/> stub that records whether <see cref="Dispose"/>
	/// has been called.
	/// </summary>
	private sealed class SpyDisposable : IDisposable {
		public bool Disposed { get; private set; }
		public void Dispose() => Disposed = true;
	}

	// ── Convenience factories ─────────────────────────────────────────────────

	private static WaveFormat StereoFloat44k =>
		WaveFormat.CreateIeeeFloatWaveFormat(44_100, 2);

	// ── WaveFormat delegation ─────────────────────────────────────────────────

	/// <summary>
	/// WaveFormat must be sourced from the ISampleProvider, not the owner.
	/// The two can differ — e.g. AIFF/MP4 where the owner is the raw reader
	/// and the source is a ToSampleProvider() wrapper.
	/// </summary>
	[Fact]
	public void WaveFormat_DelegatesToSource() {
		WaveFormat expected = StereoFloat44k;
		var        source   = new StubSampleProvider(expected, []);
		var        owner    = new SpyDisposable();

		using var reader = new AudioReader(owner, source);

		Assert.Equal(expected, reader.WaveFormat);
	}

	// ── Read delegation ───────────────────────────────────────────────────────

	/// <summary>
	/// Read() must forward to the source and return whatever the source returns.
	/// </summary>
	[Fact]
	public void Read_DelegatesToSourceAndReturnsSourceCount() {
		float[] data   = [0.1f, 0.2f, 0.3f, 0.4f];
		var     source = new StubSampleProvider(StereoFloat44k, data);
		var     owner  = new SpyDisposable();

		using var reader = new AudioReader(owner, source);

		float[] buffer = new float[4];
		int     count  = reader.Read(buffer, 0, 4);

		Assert.Equal(4,    count);
		Assert.Equal(data, buffer);
	}

	/// <summary>
	/// Read() must respect the offset parameter, writing into the correct
	/// position in the caller's buffer.
	/// </summary>
	[Fact]
	public void Read_RespectsOffset() {
		float[] data   = [0.5f, 0.6f];
		var     source = new StubSampleProvider(StereoFloat44k, data);
		var     owner  = new SpyDisposable();

		using var reader = new AudioReader(owner, source);

		float[] buffer = new float[4]; // pre-zeroed
		reader.Read(buffer, offset: 2, count: 2);

		Assert.Equal(0.0f, buffer[0]); // untouched
		Assert.Equal(0.0f, buffer[1]); // untouched
		Assert.Equal(0.5f, buffer[2]);
		Assert.Equal(0.6f, buffer[3]);
	}

	// ── Dispose behaviour ─────────────────────────────────────────────────────

	/// <summary>
	/// Dispose() must dispose the owner — this is the object that holds the
	/// underlying file handle (AiffFileReader, MediaFoundationReader, etc.).
	/// </summary>
	[Fact]
	public void Dispose_DisposesOwner() {
		var source = new StubSampleProvider(StereoFloat44k, []);
		var owner  = new SpyDisposable();

		var reader = new AudioReader(owner, source);
		reader.Dispose();

		Assert.True(owner.Disposed);
	}

	/// <summary>
	/// When owner and source are different objects — the AIFF/MP4 case where
	/// ToSampleProvider() returns a non-disposable wrapper — Dispose() must
	/// target only the owner, leaving the wrapper unaffected.
	/// This verifies there is no attempt to cast or dispose the source separately.
	/// </summary>
	[Fact]
	public void Dispose_DoesNotAttemptToDisposeSource_WhenOwnerAndSourceDiffer() {
		// source is a plain StubSampleProvider (not IDisposable).
		// If AudioReader tried to dispose the source it would have to cast —
		// that would throw or fail silently, detectable by checking owner was reached.
		var source = new StubSampleProvider(StereoFloat44k, []);
		var owner  = new SpyDisposable();

		var reader = new AudioReader(owner, source);
		reader.Dispose(); // must not throw

		Assert.True(owner.Disposed); // owner was properly disposed
	}

	/// <summary>
	/// When owner and source are the same object — the WAV/MP3 case — Dispose()
	/// still goes through the owner reference. Verify the owner is disposed exactly
	/// once (no double-dispose from trying to dispose both references).
	/// </summary>
	[Fact]
	public void Dispose_OwnerAndSourceSameObject_DisposedOnce() {
		var spy = new SpyDisposableSampleProvider(StereoFloat44k, []);

		var reader = new AudioReader(owner: spy, source: spy);
		reader.Dispose();

		Assert.Equal(1, spy.DisposeCount);
	}

	/// <summary>
	/// A combined stub that is both <see cref="IDisposable"/> and
	/// <see cref="ISampleProvider"/>, modelling the <c>AudioFileReader</c> case
	/// (WAV / MP3) where owner and source are the same object. Counts dispose calls.
	/// </summary>
	private sealed class SpyDisposableSampleProvider : ISampleProvider, IDisposable {
		private readonly float[] _samples;

		public SpyDisposableSampleProvider(WaveFormat format, float[] samples) {
			WaveFormat = format;
			_samples   = samples;
		}

		public WaveFormat WaveFormat  { get; }
		public int        DisposeCount { get; private set; }

		public int Read(float[] buffer, int offset, int count) {
			int toCopy = Math.Min(count, _samples.Length);
			Array.Copy(_samples, 0, buffer, offset, toCopy);
			return toCopy;
		}

		public void Dispose() => DisposeCount++;
	}
}
