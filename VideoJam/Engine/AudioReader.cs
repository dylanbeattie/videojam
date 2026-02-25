using NAudio.Wave;

namespace VideoJam.Engine;

/// <summary>
/// Wraps a raw NAudio file reader, separating the resource-owning
/// <see cref="IDisposable"/> from the sample-providing <see cref="ISampleProvider"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is necessary because some NAudio reader types (e.g. <see cref="AiffFileReader"/>,
/// <see cref="MediaFoundationReader"/>) return a lightweight wrapper from
/// <c>ToSampleProvider()</c> that does <b>not</b> itself implement <see cref="IDisposable"/>.
/// Without this wrapper, the file handle would be leaked if the cast
/// <c>(IDisposable) sampleProvider</c> is used naively.
/// </para>
/// <para>
/// <see cref="AudioReader"/> guarantees that both <see cref="IDisposable"/> and
/// <see cref="ISampleProvider"/> are available on a single object, regardless of
/// the underlying reader type. Disposal always targets <c>owner</c> — the object
/// that holds the underlying file handle — not the sample-provider wrapper.
/// </para>
/// </remarks>
internal sealed class AudioReader : ISampleProvider, IDisposable {
	private readonly IDisposable      _owner;
	private readonly ISampleProvider  _source;

	/// <param name="owner">
	/// The <see cref="IDisposable"/> that owns the underlying resource (file handle).
	/// </param>
	/// <param name="source">
	/// The <see cref="ISampleProvider"/> used to read decoded audio samples.
	/// May be the same object as <paramref name="owner"/> (e.g. <c>AudioFileReader</c>)
	/// or a separate wrapper (e.g. <c>WaveToSampleProvider</c> over <c>AiffFileReader</c>).
	/// </param>
	public AudioReader(IDisposable owner, ISampleProvider source) {
		_owner  = owner;
		_source = source;
	}

	/// <inheritdoc />
	public WaveFormat WaveFormat => _source.WaveFormat;

	/// <inheritdoc />
	public int Read(float[] buffer, int offset, int count) =>
		_source.Read(buffer, offset, count);

	/// <summary>
	/// Disposes the resource <b>owner</b>, releasing the underlying file handle.
	/// </summary>
	public void Dispose() => _owner.Dispose();
}
