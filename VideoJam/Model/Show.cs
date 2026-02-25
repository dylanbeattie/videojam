namespace VideoJam.Model;

/// <summary>
/// Root persisted model representing a complete show (setlist + global config).
/// Serialised to and deserialised from a <c>.show</c> JSON file.
/// </summary>
public sealed class Show {
	/// <summary>Show file schema version. Current value is <c>1</c>.</summary>
	public int Version { get; set; } = 1;

	/// <summary>Ordered list of songs in the setlist.</summary>
	public List<SongEntry> Songs { get; set; } = [];

	/// <summary>
	/// Maps video filename suffix (e.g. <c>"_lyrics"</c>) to the display index it should appear on.
	/// Applied globally unless overridden per song.
	/// </summary>
	public Dictionary<string, int> GlobalDisplayRouting { get; set; } = [];

	/// <summary>
	/// Maps display index to the relative path of the PNG to show when no video is assigned.
	/// Paths are relative to the <c>.show</c> file's directory.
	/// </summary>
	public Dictionary<int, string> FallbackImages { get; set; } = [];
}