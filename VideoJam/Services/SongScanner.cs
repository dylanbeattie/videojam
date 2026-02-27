using VideoJam.Model;

namespace VideoJam.Services;

/// <summary>
/// Scans a song folder and classifies its contents into audio stems and video files,
/// producing a <see cref="SongManifest"/> for use by the audio and video engines.
/// </summary>
public static class SongScanner {
	// Supported audio stem extensions (lower-case; comparison is case-insensitive).
	private static readonly HashSet<string> StemExtensions =
		new(StringComparer.OrdinalIgnoreCase) { ".wav", ".mp3", ".aiff" };

	/// <summary>
	/// Display index assigned to all video files until routing is resolved by
	/// <c>DisplayManager</c> in Phase 3.
	/// </summary>
	// Phase 3 note: promote to DisplayManager.PrimaryDisplayIndex when that class is implemented.
	private const int PrimaryDisplayIndex = 0;

	/// <summary>
	/// Scans <paramref name="folder"/> (non-recursive) and returns a <see cref="SongManifest"/>
	/// describing all recognised audio and video files.
	/// </summary>
	/// <param name="folder">The song directory to scan.</param>
	/// <returns>
	/// A manifest whose <see cref="SongManifest.SongName"/> is <c>folder.Name</c>
	/// and whose <see cref="SongManifest.Folder"/> is the supplied <paramref name="folder"/>.
	/// Unrecognised files are silently ignored.
	/// </returns>
	public static SongManifest Scan(DirectoryInfo folder) {
		var audioChannels = new List<AudioChannelManifest>();
		var videoFiles = new List<VideoFileManifest>();

		foreach (FileInfo file in folder.EnumerateFiles()) {
			string ext = file.Extension;

			if (StemExtensions.Contains(ext)) {
				audioChannels.Add(new AudioChannelManifest(
					File: file,
					ChannelId: file.Name,
					Type: AudioChannelType.Stem));
			} else if (ext.Equals(".mp4", StringComparison.OrdinalIgnoreCase)) {
				string suffix = ExtractSuffix(file.Name);

				videoFiles.Add(new VideoFileManifest(
					File: file,
					DisplayIndex: PrimaryDisplayIndex,   // routing resolved later by DisplayManager / Phase 3
					Suffix: suffix));

				audioChannels.Add(new AudioChannelManifest(
					File: file,
					ChannelId: $"{file.Name}:audio",
					Type: AudioChannelType.VideoAudio));
			}
			// All other extensions are silently ignored.
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
		string nameStem = Path.GetFileNameWithoutExtension(fileName);
		int lastUnderscore = nameStem.LastIndexOf('_');
		return lastUnderscore < 0 ? string.Empty : nameStem[lastUnderscore..];
	}
}