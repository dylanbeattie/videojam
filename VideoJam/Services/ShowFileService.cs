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
/// <see cref="Show.FallbackImagePath"/>) are stored as paths relative to the
/// <c>.show</c> file's directory, using forward slashes.
/// </para>
/// </remarks>
internal sealed class ShowFileService {
	private const int SupportedVersion = 2;

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

			// Migrate older schema versions at the JSON level before deserialisation,
			// since the obsolete properties no longer exist on the C# model classes.
			int version = doc.RootElement.GetProperty("version").GetInt32();
			if (version < SupportedVersion) {
				bytes = MigrateToCurrentVersion(doc.RootElement, version);
			}

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
				Channels = new Dictionary<string, ChannelSettings>(entry.Channels),
			})
			.ToList();

		string? fallbackImagePath = string.IsNullOrEmpty(show.FallbackImagePath)
			? show.FallbackImagePath
			: PathResolver.MakeRelative(show.FallbackImagePath, showDirectory);

		return new Show {
			Version = show.Version,
			Songs = songs,
			FallbackImagePath = fallbackImagePath,
			VideoWindowLayouts = new Dictionary<int, VideoWindowLayout>(show.VideoWindowLayouts),
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

		if (!versionEl.TryGetInt32(out int version) || version < 1) {
			throw new ShowFileException(
				$"The show file at '{filePath}' has unsupported version '{versionEl}'.");
		}

		if (version > SupportedVersion) {
			throw new ShowFileException(
				"This show file requires a newer version of VideoJam.");
		}

		if (!root.TryGetProperty("songs", out _)) {
			throw new ShowFileException(
				$"The show file at '{filePath}' is missing the required 'songs' field.");
		}
	}

	/// <summary>
	/// Migrates the JSON object from an older schema version up to
	/// <see cref="SupportedVersion"/>, returning the updated JSON bytes.
	/// </summary>
	/// <remarks>
	/// Migration is performed at the JSON level because the obsolete properties
	/// no longer exist on the C# model classes.
	/// </remarks>
	private static ReadOnlyMemory<byte> MigrateToCurrentVersion(JsonElement root, int fromVersion) {
		// Build a mutable dictionary from the root object so we can add/remove properties.
		var obj = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
		foreach (var prop in root.EnumerateObject())
			obj[prop.Name] = prop.Value;

		// v1 → v2 state collected before the write pass.
		string? migratedFallbackImagePath = null;

		if (fromVersion == 1) {
			// v1 → v2:
			// - Remove globalDisplayRouting
			// - Remove displayRoutingOverrides from each song entry
			// - If fallbackImages dict has entries, take the first value as fallbackImagePath
			// - Add empty videoWindowLayouts
			// - Bump version to 2

			obj.Remove("globalDisplayRouting");

			// Migrate fallbackImages → fallbackImagePath (take first value if any).
			if (obj.TryGetValue("fallbackImages", out JsonElement fallbackImagesEl)
			    && fallbackImagesEl.ValueKind == JsonValueKind.Object) {
				foreach (var entry in fallbackImagesEl.EnumerateObject()) {
					migratedFallbackImagePath = entry.Value.GetString();
					break;
				}
			}
			obj.Remove("fallbackImages");

			// Re-write each song entry without displayRoutingOverrides.
			if (obj.TryGetValue("songs", out JsonElement songsEl)
			    && songsEl.ValueKind == JsonValueKind.Array) {
				using var buffer = new System.IO.MemoryStream();
				using (var writer = new Utf8JsonWriter(buffer)) {
					writer.WriteStartArray();
					foreach (var songEl in songsEl.EnumerateArray()) {
						writer.WriteStartObject();
						foreach (var prop in songEl.EnumerateObject()) {
							if (prop.Name.Equals("displayRoutingOverrides", StringComparison.OrdinalIgnoreCase))
								continue;
							prop.WriteTo(writer);
						}
						writer.WriteEndObject();
					}
					writer.WriteEndArray();
				}
				// Re-parse the cleaned songs array so we can store it back in obj.
				var cleanSongsDoc = JsonDocument.Parse(buffer.ToArray());
				obj["songs"] = cleanSongsDoc.RootElement.Clone();
			}
		}

		// Write the migrated document back to JSON bytes.
		using var outStream = new System.IO.MemoryStream();
		using (var writer = new Utf8JsonWriter(outStream)) {
			writer.WriteStartObject();

			// Emit version bumped to SupportedVersion.
			writer.WriteNumber("version", SupportedVersion);

			// Emit songs (already cleaned above for v1 migration).
			if (obj.TryGetValue("songs", out JsonElement migratedSongs)) {
				writer.WritePropertyName("songs");
				migratedSongs.WriteTo(writer);
			}

			if (fromVersion == 1) {
				// Emit fallbackImagePath only if a value was extracted from the old dict.
				if (migratedFallbackImagePath is not null)
					writer.WriteString("fallbackImagePath", migratedFallbackImagePath);

				// Emit empty videoWindowLayouts.
				writer.WritePropertyName("videoWindowLayouts");
				writer.WriteStartObject();
				writer.WriteEndObject();
			}

			// Pass through any other properties not already explicitly emitted.
			foreach (var kvp in obj) {
				var key = kvp.Key;
				if (key.Equals("version", StringComparison.OrdinalIgnoreCase)) continue;
				if (key.Equals("songs", StringComparison.OrdinalIgnoreCase)) continue;
				if (fromVersion == 1 &&
				    (key.Equals("fallbackImagePath", StringComparison.OrdinalIgnoreCase) ||
				     key.Equals("videoWindowLayouts", StringComparison.OrdinalIgnoreCase))) continue;
				writer.WritePropertyName(key);
				kvp.Value.WriteTo(writer);
			}

			writer.WriteEndObject();
		}

		return outStream.ToArray();
	}
}
