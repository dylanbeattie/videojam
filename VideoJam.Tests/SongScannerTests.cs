using VideoJam.Model;
using VideoJam.Services;

namespace VideoJam.Tests;

/// <summary>Unit tests for <see cref="SongScanner"/>.</summary>
public sealed class SongScannerTests : IDisposable {
	// Every test gets its own isolated temp directory.
	private readonly DirectoryInfo _tempDir;

	public SongScannerTests() {
		_tempDir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
		_tempDir.Create();
	}

	public void Dispose() => _tempDir.Delete(recursive: true);

	// ── helpers ──────────────────────────────────────────────────────────────

	private void CreateFile(string name) =>
		File.WriteAllText(Path.Combine(_tempDir.FullName, name), string.Empty);

	// ── 5.1 ─────────────────────────────────────────────────────────────────

	[Fact]
	public void Scan_EmptyFolder_ReturnsManifestWithNoChannelsOrVideoFiles() {
		SongManifest result = SongScanner.Scan(_tempDir);

		Assert.Empty(result.AudioChannels);
		Assert.Empty(result.VideoFiles);
	}

	// ── 5.2 ─────────────────────────────────────────────────────────────────

	[Fact]
	public void Scan_WavMp3Aiff_ProducesThreeStemChannelsWithCorrectIds() {
		CreateFile("drums.wav");
		CreateFile("bass.mp3");
		CreateFile("keys.aiff");

		SongManifest result = SongScanner.Scan(_tempDir);

		Assert.Equal(3, result.AudioChannels.Count);
		Assert.All(result.AudioChannels, ch => Assert.Equal(AudioChannelType.Stem, ch.Type));

		var ids = result.AudioChannels.Select(ch => ch.ChannelId).ToHashSet();
		Assert.Contains("drums.wav", ids);
		Assert.Contains("bass.mp3", ids);
		Assert.Contains("keys.aiff", ids);
	}

	// ── 5.3 ─────────────────────────────────────────────────────────────────

	[Fact]
	public void Scan_Mp4WithSuffix_ProducesVideoFileManifestAndVideoAudioChannel() {
		CreateFile("show_lyrics.mp4");

		SongManifest result = SongScanner.Scan(_tempDir);

		// One video file manifest with the correct suffix.
		Assert.Single(result.VideoFiles);
		Assert.Equal("_lyrics", result.VideoFiles[0].Suffix);

		// One audio channel with the correct id and type.
		Assert.Single(result.AudioChannels);
		Assert.Equal("show_lyrics.mp4:audio", result.AudioChannels[0].ChannelId);
		Assert.Equal(AudioChannelType.VideoAudio, result.AudioChannels[0].Type);
	}

	// ── 5.4 ─────────────────────────────────────────────────────────────────

	[Fact]
	public void Scan_Mp4WithNoSuffix_ProducesVideoFileManifestWithEmptySuffix() {
		CreateFile("performance.mp4");

		SongManifest result = SongScanner.Scan(_tempDir);

		Assert.Single(result.VideoFiles);
		Assert.Equal(string.Empty, result.VideoFiles[0].Suffix);
	}

	// ── 5.5 ─────────────────────────────────────────────────────────────────

	[Fact]
	public void Scan_NonMediaFiles_AreAllIgnored() {
		CreateFile("notes.txt");
		CreateFile("cover.png");
		CreateFile("readme.pdf");

		SongManifest result = SongScanner.Scan(_tempDir);

		Assert.Empty(result.AudioChannels);
		Assert.Empty(result.VideoFiles);
	}

	// ── 5.6 ─────────────────────────────────────────────────────────────────

	[Fact]
	public void Scan_UppercaseExtensions_AreClassifiedCaseInsensitively() {
		CreateFile("DRUMS.WAV");
		CreateFile("Bass.Mp3");

		SongManifest result = SongScanner.Scan(_tempDir);

		Assert.Equal(2, result.AudioChannels.Count);
		Assert.All(result.AudioChannels, ch => Assert.Equal(AudioChannelType.Stem, ch.Type));
	}

	// ── 5.7 ─────────────────────────────────────────────────────────────────

	[Fact]
	public void Scan_SongNameIsFolderName_AndFolderMatchesInput() {
		SongManifest result = SongScanner.Scan(_tempDir);

		Assert.Equal(_tempDir.Name, result.SongName);
		Assert.Equal(_tempDir.FullName, result.Folder.FullName);
	}

	// ── mixed folder sanity check ────────────────────────────────────────────

	[Fact]
	public void Scan_MixedFolder_OnlyMediaFilesAreClassified() {
		CreateFile("drums.wav");
		CreateFile("notes.txt");
		CreateFile("cover.jpg");

		SongManifest result = SongScanner.Scan(_tempDir);

		Assert.Single(result.AudioChannels);
		Assert.Equal("drums.wav", result.AudioChannels[0].ChannelId);
	}

	// ── Display routing ───────────────────────────────────────────────────────

	[Fact]
	public void Scan_WithRoutingAndMatchingSuffix_ResolvesDisplayIndexFromRouting() {
		CreateFile("show_lyrics.mp4");
		var routing = new Dictionary<string, int> { ["_lyrics"] = 2 };

		SongManifest result = SongScanner.Scan(_tempDir, displayRouting: routing);

		Assert.Single(result.VideoFiles);
		Assert.Equal(2, result.VideoFiles[0].DisplayIndex);
	}

	[Fact]
	public void Scan_WithRoutingButSuffixAbsent_FallsBackToPrimaryDisplayIndex() {
		CreateFile("show_visuals.mp4");
		var routing = new Dictionary<string, int> { ["_lyrics"] = 2 };

		SongManifest result = SongScanner.Scan(_tempDir, displayRouting: routing);

		Assert.Single(result.VideoFiles);
		Assert.Equal(VideoJam.Engine.DisplayManager.PrimaryDisplayIndex, result.VideoFiles[0].DisplayIndex);
	}

	[Fact]
	public void Scan_WithNoRoutingArgument_FallsBackToPrimaryDisplayIndex() {
		CreateFile("show_visuals.mp4");

		SongManifest result = SongScanner.Scan(_tempDir);

		Assert.Single(result.VideoFiles);
		Assert.Equal(VideoJam.Engine.DisplayManager.PrimaryDisplayIndex, result.VideoFiles[0].DisplayIndex);
	}
}