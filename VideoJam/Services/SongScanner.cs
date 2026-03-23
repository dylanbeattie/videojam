using VideoJam.Model;

namespace VideoJam.Services;

/// <summary>
/// Scans a song folder and classifies its contents into audio stems and video files,
/// producing a <see cref="SongManifest"/> for use by the audio and video engines.
/// </summary>
public static class SongScanner {
	// Supported audio stem extensions (lower-case; comparison is case-insensitive).
	private static readonly HashSet<string> stemExtensions =
		new(StringComparer.OrdinalIgnoreCase) { ".wav", ".mp3", ".aiff" };

	/// <summary>
	/// Scans <paramref name="folder"/> (non-recursive) and returns a <see cref="SongManifest"/>
	/// describing all recognised audio and video files.
	/// </summary>
	/// <param name="folder">The song directory to scan.</param>
	/// <returns>
	/// A manifest whose <see cref="SongManifest.SongName"/> is <c>folder.Name</c>
	/// and whose <see cref="SongManifest.Folder"/> is the supplied <paramref name="folder"/>.
	/// MP4 files are assigned <see cref="VideoFileManifest.SlotIndex"/> values of 0, 1, 2, …
	/// in alphabetical order (case-insensitive) of their filenames.
	/// Unrecognised files are silently ignored.
	/// </returns>
	public static SongManifest Scan(DirectoryInfo folder) {
		var audioChannels = new List<AudioChannelManifest>();

		// Collect MP4 files separately so they can be sorted before slot assignment.
		var mp4Files = new List<FileInfo>();

		foreach (var file in folder.EnumerateFiles()) {
			var ext = file.Extension;

			if (stemExtensions.Contains(ext)) {
				audioChannels.Add(new AudioChannelManifest(
					File: file,
					ChannelId: file.Name,
					Type: AudioChannelType.Stem));
			} else if (ext.Equals(".mp4", StringComparison.OrdinalIgnoreCase)) {
				mp4Files.Add(file);
			}
			// All other extensions are silently ignored.
		}

		// Sort MP4s alphabetically (case-insensitive) and assign sequential slot indices.
		mp4Files.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));

		var videoFiles = new List<VideoFileManifest>(mp4Files.Count);
		for (int i = 0; i < mp4Files.Count; i++) {
			var file = mp4Files[i];
			var suffix = ExtractSuffix(file.Name);

			videoFiles.Add(new VideoFileManifest(
				File: file,
				SlotIndex: i,
				Suffix: suffix));

			audioChannels.Add(new AudioChannelManifest(
				File: file,
				ChannelId: $"{file.Name}:audio",
				Type: AudioChannelType.VideoAudio));
		}

		return new SongManifest(
			SongName: folder.Name,
			Folder: folder,
			AudioChannels: audioChannels,
			VideoFiles: videoFiles);
	}

	/// <summary>
	/// Extracts the underscore-prefixed suffix from a filename stem.
	/// For example, <c>show_visuals.mp4</c> → <c>"_visuals"</c>.
	/// Returns an empty string if the name contains no underscore.
	/// </summary>
	private static string ExtractSuffix(string fileName) {
		// Strip extension to work with the bare name.
		var nameStem = Path.GetFileNameWithoutExtension(fileName);
		var lastUnderscore = nameStem.LastIndexOf('_');
		return lastUnderscore < 0 ? string.Empty : nameStem[lastUnderscore..];
	}
}