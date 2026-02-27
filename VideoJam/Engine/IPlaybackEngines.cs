namespace VideoJam.Engine;

/// <summary>
/// Minimal contract for the audio playback side of a synchronised start sequence.
/// Implemented by <see cref="AudioEngine"/>.
/// </summary>
internal interface IAudioPlayback {
	/// <summary>
	/// Starts audio playback and returns a <see cref="System.Diagnostics.Stopwatch.GetTimestamp"/>
	/// value captured immediately after the hardware output begins.
	/// </summary>
	long Play();
}

/// <summary>
/// Minimal contract for the video playback side of a synchronised start sequence.
/// Implemented by <see cref="VideoEngine"/>.
/// </summary>
internal interface IVideoPlayback {
	/// <summary>
	/// Starts video playback on all pre-buffered displays, using
	/// <paramref name="audioStartTimestamp"/> for Δt diagnostics.
	/// </summary>
	/// <param name="audioStartTimestamp">
	/// The timestamp returned by <see cref="IAudioPlayback.Play"/>.
	/// </param>
	void Play(long audioStartTimestamp);
}
