namespace VideoJam.Model;

/// <summary>
/// Root persisted model representing a complete show (setlist + global config).
/// Serialised to and deserialised from a <c>.show</c> JSON file.
/// </summary>
public sealed class Show {
	/// <summary>Current <c>.show</c> file schema version.</summary>
	private const int CURRENT_SCHEMA_VERSION = 2;

	/// <summary>Show file schema version. Current value is <see cref="CURRENT_SCHEMA_VERSION"/>.</summary>
	public int Version { get; set; } = CURRENT_SCHEMA_VERSION;

	/// <summary>Ordered list of songs in the setlist.</summary>
	public List<SongEntry> Songs { get; set; } = [];

	/// <summary>
	/// Relative path of the PNG to show when no video is playing.
	/// Path is relative to the <c>.show</c> file's directory.
	/// </summary>
	public string? FallbackImagePath { get; set; }

	/// <summary>
	/// Persisted window layouts keyed by video slot index (0-based).
	/// </summary>
	public Dictionary<int, VideoWindowLayout> VideoWindowLayouts { get; set; } = [];
}