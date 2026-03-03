using System.Text;
using System.Text.Json;
using VideoJam.Model;

namespace VideoJam.Services;

/// <summary>
/// Serialises and deserialises <c>.show</c> files using <see cref="System.Text.Json"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Save</b> is atomic: the JSON is first written to a <c>.tmp</c> sibling file in the same
/// directory, then renamed over the target. This ensures the previous file remains intact if
/// the process crashes mid-write. The temp file and target must reside on the same volume.
/// </para>
/// <para>
/// All file paths within the JSON (<see cref="SongEntry.FolderPath"/>,
/// <see cref="Show.FallbackImages"/> values) are stored as paths relative to the
/// <c>.show</c> file's directory, using forward slashes.
/// </para>
/// </remarks>
internal sealed class ShowFileService {
	private const int SupportedVersion = 1;

	// UTF-8 without BOM — never emit a BOM in files we write.
	private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

	private static readonly JsonSerializerOptions JsonOptions = new() {
		WriteIndented = true,
		PropertyNameCaseInsensitive = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
	};

	/// <summary>
	/// Serialises <paramref name="show"/> to a UTF-8 JSON <c>.show</c> file at
	/// <paramref name="filePath"/>.
	/// </summary>
	/// <remarks>
	/// Song folder paths and fallback image paths are converted to paths relative to
	/// the <c>.show</c> file's directory before serialisation. The original
	/// <paramref name="show"/> object is not mutated.
	/// </remarks>
	/// <param name="show">The show to save.</param>
	/// <param name="filePath">Absolute path of the destination <c>.show</c> file.</param>
	/// <exception cref="IOException">
	/// Thrown if the file cannot be written or the atomic rename fails.
	/// </exception>
	public void Save(Show show, string filePath) {
		string showDirectory = Path.GetDirectoryName(filePath)
		                       ?? throw new IOException($"Cannot determine directory for path: {filePath}");

		// Build a serialisation-ready clone with relative paths — do not mutate the caller's object.
		Show toSerialise = ToRelativePaths(show, showDirectory);

		string json = JsonSerializer.Serialize(toSerialise, JsonOptions);
		byte[] bytes = Utf8NoBom.GetBytes(json);

		string tmpPath = filePath + ".tmp";
		File.WriteAllBytes(tmpPath, bytes);

		// Atomic rename — both files are in the same directory (same volume).
		File.Move(tmpPath, filePath, overwrite: true);
	}

	/// <summary>
	/// Loads and deserialises a <c>.show</c> file from <paramref name="filePath"/>.
	/// </summary>
	/// <remarks>
	/// Path values inside the loaded <see cref="Show"/> are stored as raw relative strings
	/// exactly as they appear in the JSON. Use <see cref="PathResolver.Resolve"/> at the
	/// point of use to obtain absolute paths.
	/// </remarks>
	/// <param name="filePath">Absolute path of the <c>.show</c> file to load.</param>
	/// <returns>The deserialised <see cref="Show"/>.</returns>
	/// <exception cref="ShowFileException">
	/// Thrown when the file fails schema validation (missing required fields or unsupported version).
	/// </exception>
	/// <exception cref="IOException">Thrown if the file cannot be read.</exception>
	public Show Load(string filePath) {
		byte[] raw = File.ReadAllBytes(filePath);

		// Strip UTF-8 BOM if present — System.Text.Json rejects BOM by default.
		// Use ReadOnlyMemory<byte> so it works with both JsonDocument.Parse and JsonSerializer.Deserialize.
		ReadOnlyMemory<byte> bytes = StripBom(raw);

		JsonDocument doc;
		try {
			doc = JsonDocument.Parse(bytes);
		} catch (JsonException ex) {
			throw new ShowFileException($"The show file at '{filePath}' is not valid JSON: {ex.Message}");
		}

		using (doc) {
			ValidateDocument(doc.RootElement, filePath);

			Show? show = JsonSerializer.Deserialize<Show>(bytes.Span, JsonOptions);
			if (show is null) {
				throw new ShowFileException($"The show file at '{filePath}' deserialised to null.");
			}
			return show;
		}
	}

	// ── Helpers ──────────────────────────────────────────────────────────────

	/// <summary>
	/// Returns a shallow clone of <paramref name="show"/> with all path fields converted
	/// to paths relative to <paramref name="showDirectory"/>. The original is not mutated.
	/// </summary>
	private static Show ToRelativePaths(Show show, string showDirectory) {
		var songs = show.Songs
			.Select(entry => new SongEntry {
				FolderPath = string.IsNullOrEmpty(entry.FolderPath)
					? entry.FolderPath
					: PathResolver.MakeRelative(entry.FolderPath, showDirectory),
				Name = entry.Name,
				DisplayRoutingOverrides = new Dictionary<string, int>(entry.DisplayRoutingOverrides),
				Channels = new Dictionary<string, ChannelSettings>(entry.Channels),
			})
			.ToList();

		var fallbackImages = show.FallbackImages
			.ToDictionary(
				kvp => kvp.Key,
				kvp => string.IsNullOrEmpty(kvp.Value)
					? kvp.Value
					: PathResolver.MakeRelative(kvp.Value, showDirectory));

		return new Show {
			Version = show.Version,
			Songs = songs,
			GlobalDisplayRouting = new Dictionary<string, int>(show.GlobalDisplayRouting),
			FallbackImages = fallbackImages,
		};
	}

	/// <summary>
	/// Strips the UTF-8 byte-order mark from the beginning of <paramref name="bytes"/> if present,
	/// returning a <see cref="ReadOnlyMemory{T}"/> compatible with both
	/// <see cref="JsonDocument.Parse(ReadOnlyMemory{byte}, JsonDocumentOptions)"/> and
	/// <see cref="JsonSerializer.Deserialize{T}(ReadOnlySpan{byte}, JsonSerializerOptions?)"/>.
	/// </summary>
	private static ReadOnlyMemory<byte> StripBom(byte[] bytes) {
		if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) {
			return bytes.AsMemory(3);
		}
		return bytes;
	}

	/// <summary>
	/// Validates required fields directly on the raw <see cref="JsonElement"/> before deserialisation,
	/// so that absent fields are distinguishable from fields with default values.
	/// Throws <see cref="ShowFileException"/> with a field-specific message on failure.
	/// </summary>
	private static void ValidateDocument(JsonElement root, string filePath) {
		if (!root.TryGetProperty("version", out JsonElement versionEl)) {
			throw new ShowFileException(
				$"The show file at '{filePath}' is missing the required 'version' field.");
		}

		if (!versionEl.TryGetInt32(out int version) || version != SupportedVersion) {
			throw new ShowFileException(
				$"The show file at '{filePath}' has unsupported version '{versionEl}'. " +
				$"Only version {SupportedVersion} is supported.");
		}

		if (!root.TryGetProperty("songs", out _)) {
			throw new ShowFileException(
				$"The show file at '{filePath}' is missing the required 'songs' field.");
		}

		if (!root.TryGetProperty("globalDisplayRouting", out _)) {
			throw new ShowFileException(
				$"The show file at '{filePath}' is missing the required 'globalDisplayRouting' field.");
		}
	}
}
