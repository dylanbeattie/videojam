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

		var original = new Show {
			GlobalDisplayRouting = new Dictionary<string, int> { ["_lyrics"] = 1 },
			FallbackImages = new Dictionary<int, string> { [1] = Path.Combine(tempDir.FullName, "bg.png") },
			Songs = [
				new SongEntry {
					FolderPath = songFolder,
					Name = "My Song",
					Channels = new Dictionary<string, ChannelSettings> {
						["drums.wav"] = new ChannelSettings { Level = 0.8f, Muted = false },
					},
					DisplayRoutingOverrides = new Dictionary<string, int> { ["_visuals"] = 2 },
				},
			],
		};

		// Act
		svc.Save(original, showPath);
		Show loaded = svc.Load(showPath);

		// Assert
		Assert.Equal(1, loaded.Version);
		Assert.Single(loaded.Songs);
		Assert.Equal("My Song", loaded.Songs[0].Name);
		Assert.Equal(0.8f, loaded.Songs[0].Channels["drums.wav"].Level);
		Assert.False(loaded.Songs[0].Channels["drums.wav"].Muted);
		Assert.Equal(2, loaded.Songs[0].DisplayRoutingOverrides["_visuals"]);
		Assert.Equal(1, loaded.GlobalDisplayRouting["_lyrics"]);
		Assert.True(loaded.FallbackImages.ContainsKey(1));
	}

	// ── 6.3 valid minimal show ────────────────────────────────────────────────

	[Fact]
	public void Load_ValidMinimalShow_Succeeds() {
		// Arrange
		string path = ShowPath();
		WriteRaw(path, """{"version":1,"songs":[],"globalDisplayRouting":{}}""");

		// Act
		Show show = svc.Load(path);

		// Assert
		Assert.Equal(1, show.Version);
		Assert.Empty(show.Songs);
	}

	// ── 6.4 missing version ───────────────────────────────────────────────────

	[Fact]
	public void Load_MissingVersionField_ThrowsShowFileException() {
		// Arrange
		string path = ShowPath();
		WriteRaw(path, """{"songs":[],"globalDisplayRouting":{}}""");

		// Act & Assert
		var ex = Assert.Throws<ShowFileException>(() => svc.Load(path));
		Assert.Contains("version", ex.Message, StringComparison.OrdinalIgnoreCase);
	}

	// ── 6.5 unsupported version ───────────────────────────────────────────────

	[Fact]
	public void Load_UnsupportedVersion_ThrowsShowFileException() {
		// Arrange
		string path = ShowPath();
		WriteRaw(path, """{"version":99,"songs":[],"globalDisplayRouting":{}}""");

		// Act & Assert
		var ex = Assert.Throws<ShowFileException>(() => svc.Load(path));
		Assert.Contains("version", ex.Message, StringComparison.OrdinalIgnoreCase);
	}

	// ── 6.6 missing songs ─────────────────────────────────────────────────────

	[Fact]
	public void Load_MissingSongsField_ThrowsShowFileException() {
		// Arrange
		string path = ShowPath();
		WriteRaw(path, """{"version":1,"globalDisplayRouting":{}}""");

		// Act & Assert
		var ex = Assert.Throws<ShowFileException>(() => svc.Load(path));
		Assert.Contains("songs", ex.Message, StringComparison.OrdinalIgnoreCase);
	}

	// ── 6.7 missing globalDisplayRouting ─────────────────────────────────────

	[Fact]
	public void Load_MissingGlobalDisplayRoutingField_ThrowsShowFileException() {
		// Arrange
		string path = ShowPath();
		WriteRaw(path, """{"version":1,"songs":[]}""");

		// Act & Assert
		var ex = Assert.Throws<ShowFileException>(() => svc.Load(path));
		Assert.Contains("globalDisplayRouting", ex.Message, StringComparison.OrdinalIgnoreCase);
	}

	// ── 6.8 UTF-8 BOM ────────────────────────────────────────────────────────

	[Fact]
	public void Load_Utf8WithBom_Succeeds() {
		// Arrange
		string path = ShowPath();
		string json = """{"version":1,"songs":[],"globalDisplayRouting":{}}""";
		byte[] bom = [0xEF, 0xBB, 0xBF];
		byte[] content = bom.Concat(Encoding.UTF8.GetBytes(json)).ToArray();
		File.WriteAllBytes(path, content);

		// Act
		Show show = svc.Load(path);

		// Assert
		Assert.Equal(1, show.Version);
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
