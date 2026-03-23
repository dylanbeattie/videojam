using System.Text;
using VideoJam.Model;
using VideoJam.Services;
using Xunit;

namespace VideoJam.Tests;

/// <summary>Unit tests for <see cref="ShowFileService"/>.</summary>
public sealed class ShowFileServiceTests : IDisposable {
	private readonly DirectoryInfo tempDir;
	private readonly ShowFileService svc = new();

	public ShowFileServiceTests() {
		tempDir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
		tempDir.Create();
	}

	public void Dispose() => tempDir.Delete(recursive: true);

	// ── helpers ───────────────────────────────────────────────────────────────

	private string ShowPath(string name = "test.show") =>
		Path.Combine(tempDir.FullName, name);

	private void WriteRaw(string path, string json) =>
		File.WriteAllText(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

	// ── 6.2 round-trip ───────────────────────────────────────────────────────

	[Fact]
	public void RoundTrip_SaveThenLoad_ProducesIdenticalShow() {
		// Arrange
		string showPath = ShowPath();
		// Use an absolute path in FolderPath so Save() has something to relativise.
		string songFolder = Path.Combine(tempDir.FullName, "MySong");
		Directory.CreateDirectory(songFolder);
		string fallbackPath = Path.Combine(tempDir.FullName, "bg.png");

		var original = new Show {
			FallbackImagePath = fallbackPath,
			Songs = [
				new SongEntry {
					FolderPath = songFolder,
					Name = "My Song",
					Channels = new Dictionary<string, ChannelSettings> {
						["drums.wav"] = new ChannelSettings { Level = 0.8f, Muted = false },
					},
				},
			],
		};

		// Act
		svc.Save(original, showPath);
		Show loaded = svc.Load(showPath);

		// Assert
		Assert.Equal(2, loaded.Version);
		Assert.Single(loaded.Songs);
		Assert.Equal("My Song", loaded.Songs[0].Name);
		Assert.Equal(0.8f, loaded.Songs[0].Channels["drums.wav"].Level);
		Assert.False(loaded.Songs[0].Channels["drums.wav"].Muted);
		Assert.NotNull(loaded.FallbackImagePath);
	}

	// ── 6.3 valid minimal show ────────────────────────────────────────────────

	[Fact]
	public void Load_ValidMinimalShow_Succeeds() {
		// Arrange
		string path = ShowPath();
		WriteRaw(path, """{"version":2,"songs":[]}""");

		// Act
		Show show = svc.Load(path);

		// Assert
		Assert.Equal(2, show.Version);
		Assert.Empty(show.Songs);
	}

	// ── 6.4 missing version ───────────────────────────────────────────────────

	[Fact]
	public void Load_MissingVersionField_ThrowsShowFileException() {
		// Arrange
		string path = ShowPath();
		WriteRaw(path, """{"songs":[]}""");

		// Act & Assert
		var ex = Assert.Throws<ShowFileException>(() => svc.Load(path));
		Assert.Contains("version", ex.Message, StringComparison.OrdinalIgnoreCase);
	}

	// ── 6.5 unsupported version (future) ─────────────────────────────────────

	[Fact]
	public void Load_FutureVersion_ThrowsShowFileExceptionWithNewer() {
		// Arrange
		string path = ShowPath();
		WriteRaw(path, """{"version":99,"songs":[]}""");

		// Act & Assert
		var ex = Assert.Throws<ShowFileException>(() => svc.Load(path));
		Assert.Contains("newer", ex.Message, StringComparison.OrdinalIgnoreCase);
	}

	// ── 6.6 missing songs ─────────────────────────────────────────────────────

	[Fact]
	public void Load_MissingSongsField_ThrowsShowFileException() {
		// Arrange
		string path = ShowPath();
		WriteRaw(path, """{"version":2}""");

		// Act & Assert
		var ex = Assert.Throws<ShowFileException>(() => svc.Load(path));
		Assert.Contains("songs", ex.Message, StringComparison.OrdinalIgnoreCase);
	}

	// ── 6.7 v1 → v2 migration ─────────────────────────────────────────────────

	[Fact]
	public void Load_V1ShowFile_MigratesSuccessfully_AndVersion2IsReturned() {
		// Arrange: a complete v1 show file with all v1-only fields present.
		string path = ShowPath();
		WriteRaw(path, """
		{
			"version": 1,
			"songs": [
				{
					"folderPath": "MySong",
					"name": "My Song",
					"displayRoutingOverrides": { "_visuals": 2 },
					"channels": {}
				}
			],
			"globalDisplayRouting": { "_lyrics": 1 },
			"fallbackImages": { "1": "bg.png" }
		}
		""");

		// Act
		Show show = svc.Load(path);

		// Assert
		Assert.Equal(2, show.Version);
		Assert.Single(show.Songs);
		Assert.Equal("My Song", show.Songs[0].Name);
		// displayRoutingOverrides must have been stripped — no such property on SongEntry v2.
		// fallbackImages first value should have been promoted to fallbackImagePath.
		Assert.Equal("bg.png", show.FallbackImagePath);
		// videoWindowLayouts should be present and empty.
		Assert.NotNull(show.VideoWindowLayouts);
	}

	[Fact]
	public void Load_V1ShowFile_WithNoFallbackImages_FallbackImagePathIsNull() {
		// Arrange
		string path = ShowPath();
		WriteRaw(path, """{"version":1,"songs":[],"globalDisplayRouting":{}}""");

		// Act
		Show show = svc.Load(path);

		// Assert
		Assert.Equal(2, show.Version);
		Assert.Null(show.FallbackImagePath);
	}

	// ── 6.8 UTF-8 BOM ────────────────────────────────────────────────────────

	[Fact]
	public void Load_Utf8WithBom_Succeeds() {
		// Arrange
		string path = ShowPath();
		string json = """{"version":2,"songs":[]}""";
		byte[] bom = [0xEF, 0xBB, 0xBF];
		byte[] content = bom.Concat(Encoding.UTF8.GetBytes(json)).ToArray();
		File.WriteAllBytes(path, content);

		// Act
		Show show = svc.Load(path);

		// Assert
		Assert.Equal(2, show.Version);
	}

	// ── 6.9 paths stored as relative strings ─────────────────────────────────

	[Fact]
	public void Save_WritesRelativePaths_LoadRestoresRawRelativeStrings() {
		// Arrange
		string showPath = ShowPath();
		string songFolder = Path.Combine(tempDir.FullName, "SongA");
		Directory.CreateDirectory(songFolder);

		var show = new Show {
			Songs = [new SongEntry { FolderPath = songFolder, Name = "Song A" }],
		};

		// Act
		svc.Save(show, showPath);
		Show loaded = svc.Load(showPath);

		// Assert — FolderPath should be relative (not absolute)
		string loadedPath = loaded.Songs[0].FolderPath;
		Assert.False(Path.IsPathRooted(loadedPath),
			$"Expected a relative path but got: '{loadedPath}'");
		Assert.Equal("SongA", loadedPath);
	}

	// ── 6.10 no leftover .tmp ─────────────────────────────────────────────────

	[Fact]
	public void Save_NoTmpFileRemainsAfterSuccess() {
		// Arrange
		string showPath = ShowPath();
		var show = new Show();

		// Act
		svc.Save(show, showPath);

		// Assert
		string tmpPath = showPath + ".tmp";
		Assert.False(File.Exists(tmpPath), $"Temp file should have been removed: '{tmpPath}'");
	}
}
