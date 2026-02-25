namespace VideoJam.Model;

/// <summary>
/// Persisted audio channel settings for a single channel within a song.
/// </summary>
public sealed class ChannelSettings {
	/// <summary>
	/// Output level in the range [0.0, 1.0] where 1.0 is full volume.
	/// Default is <c>1.0</c>.
	/// </summary>
	public float Level { get; set; } = 1.0f;

	/// <summary>
	/// Whether the channel is muted.
	/// Default is <c>false</c> for audio stems; <c>true</c> for video audio channels.
	/// </summary>
	public bool Muted { get; set; } = false;
}