namespace VideoJam.Model;

/// <summary>Distinguishes a pure audio stem from an audio track extracted from a video file.</summary>
public enum AudioChannelType {
	/// <summary>A standalone audio stem file (.wav, .mp3, .aiff).</summary>
	Stem,

	/// <summary>An audio track decoded from a video file (.mp4).</summary>
	VideoAudio,
}

/// <summary>
/// Describes a single audio channel resolved from a song folder scan.
/// Immutable; never persisted.
/// </summary>
/// <param name="File">The audio or video file on disk.</param>
/// <param name="ChannelId">
/// The logical channel identifier used as the key in the <c>.show</c> file.
/// For stems this is the filename (e.g. <c>drums.wav</c>);
/// for video audio tracks it is <c>{filename}:audio</c>.
/// </param>
/// <param name="Type">Whether this channel comes from a stem or a video file.</param>
public sealed record AudioChannelManifest(
	FileInfo File,
	string ChannelId,
	AudioChannelType Type);

/// <summary>
/// Describes a video file resolved from a song folder scan, including its playback slot.
/// Immutable; never persisted.
/// </summary>
/// <param name="File">The video file on disk.</param>
/// <param name="SlotIndex">
/// The 0-based slot index assigned to this video file, determined by alphabetical sort order
/// of video filenames within the song folder.
/// </param>
/// <param name="Suffix">
/// The underscore-prefixed filename suffix extracted from the filename stem
/// (e.g. <c>_lyrics</c>, <c>_visuals</c>). Empty string if no suffix.
/// </param>
public sealed record VideoFileManifest(
	FileInfo File,
	int SlotIndex,
	string Suffix);

/// <summary>
/// The complete runtime description of a song folder, produced by <see cref="Services.SongScanner"/>.
/// Immutable; never persisted.
/// </summary>
/// <param name="SongName">The display name of the song (the folder's own name).</param>
/// <param name="Folder">The directory that was scanned.</param>
/// <param name="AudioChannels">All audio channels found in the folder.</param>
/// <param name="VideoFiles">All video files found in the folder.</param>
public sealed record SongManifest(
	string SongName,
	DirectoryInfo Folder,
	IReadOnlyList<AudioChannelManifest> AudioChannels,
	IReadOnlyList<VideoFileManifest> VideoFiles);