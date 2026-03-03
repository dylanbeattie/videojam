using VideoJam.Model;
using Xunit;

namespace VideoJam.Tests;

/// <summary>Unit tests for <see cref="SongEntry.CreateFromScan"/>.</summary>
public sealed class SongEntryCreateFromScanTests : IDisposable {
	private readonly DirectoryInfo tempDir;

	public SongEntryCreateFromScanTests() {
		tempDir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
		tempDir.Create();
	}

	public void Dispose() => tempDir.Delete(recursive: true);

	// ── helpers ───────────────────────────────────────────────────────────────

	/// <summary>
	/// Builds a minimal <see cref="SongManifest"/> for a song folder inside the temp dir.
	/// </summary>
	private SongManifest MakeManifest(string folderName, params AudioChannelManifest[] channels) {
		var folder = new DirectoryInfo(Path.Combine(tempDir.FullName, folderName));
		folder.Create();
		return new SongManifest(folderName, folder, channels, []);
	}

	private static AudioChannelManifest StemChannel(string name) =>
		new(new FileInfo(name), name, AudioChannelType.Stem);

	private static AudioChannelManifest VideoAudioChannel(string videoName) =>
		new(new FileInfo(videoName), $"{videoName}:audio", AudioChannelType.VideoAudio);

	// ── 7.2 FolderPath is relative ────────────────────────────────────────────

	[Fact]
	public void CreateFromScan_FolderPath_IsRelativeToShowDirectory() {
		// Arrange
		SongManifest manifest = MakeManifest("MySong");
		string showDir = tempDir.FullName;

		// Act
		SongEntry entry = SongEntry.CreateFromScan(manifest, showDir);

		// Assert
		Assert.False(Path.IsPathRooted(entry.FolderPath),
			$"FolderPath should be relative but got: '{entry.FolderPath}'");
		Assert.Equal("MySong", entry.FolderPath);
	}

	// ── 7.3 Name equals folder leaf name ─────────────────────────────────────

	[Fact]
	public void CreateFromScan_Name_EqualsFolderLeafName() {
		// Arrange
		SongManifest manifest = MakeManifest("OpeningAct");

		// Act
		SongEntry entry = SongEntry.CreateFromScan(manifest, tempDir.FullName);

		// Assert
		Assert.Equal("OpeningAct", entry.Name);
	}

	// ── 7.4 stem channel defaults ─────────────────────────────────────────────

	[Fact]
	public void CreateFromScan_StemChannel_LevelOnePointZeroMutedFalse() {
		// Arrange
		SongManifest manifest = MakeManifest("Song", StemChannel("drums.wav"));

		// Act
		SongEntry entry = SongEntry.CreateFromScan(manifest, tempDir.FullName);

		// Assert
		Assert.True(entry.Channels.TryGetValue("drums.wav", out ChannelSettings? settings));
		Assert.Equal(1.0f, settings!.Level);
		Assert.False(settings.Muted);
	}

	// ── 7.5 video audio channel defaults ─────────────────────────────────────

	[Fact]
	public void CreateFromScan_VideoAudioChannel_LevelOnePointZeroMutedTrue() {
		// Arrange
		SongManifest manifest = MakeManifest("Song", VideoAudioChannel("video_lyrics.mp4"));

		// Act
		SongEntry entry = SongEntry.CreateFromScan(manifest, tempDir.FullName);

		// Assert
		string channelId = "video_lyrics.mp4:audio";
		Assert.True(entry.Channels.TryGetValue(channelId, out ChannelSettings? settings));
		Assert.Equal(1.0f, settings!.Level);
		Assert.True(settings.Muted);
	}

	// ── 7.6 Channels keyed by ChannelId ──────────────────────────────────────

	[Fact]
	public void CreateFromScan_Channels_KeyedByChannelId() {
		// Arrange
		SongManifest manifest = MakeManifest("Song",
			StemChannel("bass.wav"),
			VideoAudioChannel("show_visuals.mp4"));

		// Act
		SongEntry entry = SongEntry.CreateFromScan(manifest, tempDir.FullName);

		// Assert
		Assert.True(entry.Channels.ContainsKey("bass.wav"));
		Assert.True(entry.Channels.ContainsKey("show_visuals.mp4:audio"));
		Assert.Equal(2, entry.Channels.Count);
	}

	// ── 7.7 DisplayRoutingOverrides is empty ──────────────────────────────────

	[Fact]
	public void CreateFromScan_DisplayRoutingOverrides_IsEmpty() {
		// Arrange
		SongManifest manifest = MakeManifest("Song", StemChannel("guitar.wav"));

		// Act
		SongEntry entry = SongEntry.CreateFromScan(manifest, tempDir.FullName);

		// Assert
		Assert.NotNull(entry.DisplayRoutingOverrides);
		Assert.Empty(entry.DisplayRoutingOverrides);
	}
}
