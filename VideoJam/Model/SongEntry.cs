using VideoJam.Services;

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

	/// <summary>
	/// Creates a <see cref="SongEntry"/> from a runtime scan result, with paths relative
	/// to the <c>.show</c> file's directory and per-channel defaults applied.
	/// </summary>
	/// <param name="manifest">The scan result to convert.</param>
	/// <param name="showFileDirectory">
	/// The directory of the <c>.show</c> file; used to compute <see cref="FolderPath"/>.
	/// </param>
	/// <returns>
	/// A new <see cref="SongEntry"/> with:
	/// <list type="bullet">
	///   <item><see cref="FolderPath"/> — relative path from <paramref name="showFileDirectory"/> to the song folder</item>
	///   <item><see cref="Name"/> — the folder's leaf name</item>
	///   <item><see cref="Channels"/> — one entry per audio channel, with type-appropriate defaults</item>
	///   <item><see cref="DisplayRoutingOverrides"/> — empty</item>
	/// </list>
	/// </returns>
	public static SongEntry CreateFromScan(SongManifest manifest, string showFileDirectory) {
		var channels = new Dictionary<string, ChannelSettings>();
		foreach (AudioChannelManifest channel in manifest.AudioChannels) {
			channels[channel.ChannelId] = new ChannelSettings {
				Level = 1.0f,
				Muted = channel.Type == AudioChannelType.VideoAudio,
			};
		}

		return new SongEntry {
			FolderPath = PathResolver.MakeRelative(manifest.Folder.FullName, showFileDirectory),
			Name = manifest.Folder.Name,
			Channels = channels,
			DisplayRoutingOverrides = [],
		};
	}
}
