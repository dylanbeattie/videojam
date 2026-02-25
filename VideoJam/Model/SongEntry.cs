namespace VideoJam.Model;

/// <summary>
/// Persisted model for a single song in the setlist.
/// All file paths are relative to the <c>.show</c> file's directory.
/// </summary>
public sealed class SongEntry {
	/// <summary>
	/// Path to the song folder, relative to the <c>.show</c> file's directory.
	/// Stored as a raw relative string; resolve via PathResolver before use.
	/// </summary>
	public string FolderPath { get; set; } = string.Empty;

	/// <summary>Display name shown in the setlist (defaults to the folder name).</summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Per-song overrides for display routing, keyed by video filename suffix.
	/// Takes precedence over <see cref="Show.GlobalDisplayRouting"/>.
	/// </summary>
	public Dictionary<string, int> DisplayRoutingOverrides { get; set; } = [];

	/// <summary>
	/// Per-channel settings keyed by channel ID (e.g. <c>"drums.wav"</c> or <c>"video.mp4:audio"</c>).
	/// </summary>
	public Dictionary<string, ChannelSettings> Channels { get; set; } = [];
}