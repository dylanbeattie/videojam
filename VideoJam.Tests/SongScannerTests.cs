using VideoJam.Model;
using VideoJam.Services;

namespace VideoJam.Tests;

/// <summary>Unit tests for <see cref="SongScanner"/>.</summary>
public sealed class SongScannerTests : IDisposable {
	// Every test gets its own isolated temp directory.
	private readonly DirectoryInfo tempDir;

	public SongScannerTests() {
		tempDir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
		tempDir.Create();
	}

	public void Dispose() => tempDir.Delete(recursive: true);

	// ── helpers ──────────────────────────────────────────────────────────────

	private void CreateFile(string name) =>
		File.WriteAllText(Path.Combine(tempDir.FullName, name), string.Empty);

	// ── 5.1 ─────────────────────────────────────────────────────────────────

	[Fact]
	public void Scan_EmptyFolder_ReturnsManifestWithNoChannelsOrVideoFiles() {
		var result = SongScanner.Scan(tempDir);

		Assert.Empty(result.AudioChannels);
		Assert.Empty(result.VideoFiles);
	}

	// ── 5.2 ─────────────────────────────────────────────────────────────────

	[Fact]
	public void Scan_WavMp3Aiff_ProducesThreeStemChannelsWithCorrectIds() {
		CreateFile("drums.wav");
		CreateFile("bass.mp3");
		CreateFile("keys.aiff");

		var result = SongScanner.Scan(tempDir);

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

		var result = SongScanner.Scan(tempDir);

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

		var result = SongScanner.Scan(tempDir);

		Assert.Single(result.VideoFiles);
		Assert.Equal(string.Empty, result.VideoFiles[0].Suffix);
	}

	// ── 5.5 ─────────────────────────────────────────────────────────────────

	[Fact]
	public void Scan_NonMediaFiles_AreAllIgnored() {
		CreateFile("notes.txt");
		CreateFile("cover.png");
		CreateFile("readme.pdf");

		var result = SongScanner.Scan(tempDir);

		Assert.Empty(result.AudioChannels);
		Assert.Empty(result.VideoFiles);
	}

	// ── 5.6 ─────────────────────────────────────────────────────────────────

	[Fact]
	public void Scan_UppercaseExtensions_AreClassifiedCaseInsensitively() {
		CreateFile("DRUMS.WAV");
		CreateFile("Bass.Mp3");

		var result = SongScanner.Scan(tempDir);

		Assert.Equal(2, result.AudioChannels.Count);
		Assert.All(result.AudioChannels, ch => Assert.Equal(AudioChannelType.Stem, ch.Type));
	}

	// ── 5.7 ─────────────────────────────────────────────────────────────────

	[Fact]
	public void Scan_SongNameIsFolderName_AndFolderMatchesInput() {
		var result = SongScanner.Scan(tempDir);

		Assert.Equal(tempDir.Name, result.SongName);
		Assert.Equal(tempDir.FullName, result.Folder.FullName);
	}

	// ── mixed folder sanity check ────────────────────────────────────────────

	[Fact]
	public void Scan_MixedFolder_OnlyMediaFilesAreClassified() {
		CreateFile("drums.wav");
		CreateFile("notes.txt");
		CreateFile("cover.jpg");

		var result = SongScanner.Scan(tempDir);

		Assert.Single(result.AudioChannels);
		Assert.Equal("drums.wav", result.AudioChannels[0].ChannelId);
	}

	// ── Slot index assignment ─────────────────────────────────────────────────

	[Fact]
	public void Scan_SingleMp4_GetsSlotIndexZero() {
		CreateFile("show_lyrics.mp4");

		var result = SongScanner.Scan(tempDir);

		Assert.Single(result.VideoFiles);
		Assert.Equal(0, result.VideoFiles[0].SlotIndex);
	}

	[Fact]
	public void Scan_TwoMp4s_AssignedSequentialSlotIndicesInAlphabeticalOrder() {
		CreateFile("show_visuals.mp4");
		CreateFile("show_lyrics.mp4");

		var result = SongScanner.Scan(tempDir);

		Assert.Equal(2, result.VideoFiles.Count);
		// Alphabetical order: show_lyrics.mp4 < show_visuals.mp4
		var byName = result.VideoFiles.OrderBy(v => v.File.Name, StringComparer.OrdinalIgnoreCase).ToList();
		Assert.Equal(0, byName[0].SlotIndex); // show_lyrics.mp4
		Assert.Equal(1, byName[1].SlotIndex); // show_visuals.mp4
	}

	[Fact]
	public void Scan_Mp4SlotIndices_AreCaseInsensitiveAlphabetical() {
		CreateFile("ZETA.mp4");
		CreateFile("alpha.mp4");

		var result = SongScanner.Scan(tempDir);

		Assert.Equal(2, result.VideoFiles.Count);
		var sorted = result.VideoFiles.OrderBy(v => v.SlotIndex).ToList();
		Assert.Equal("alpha.mp4", sorted[0].File.Name, StringComparer.OrdinalIgnoreCase);
		Assert.Equal("ZETA.mp4", sorted[1].File.Name, StringComparer.OrdinalIgnoreCase);
	}
}