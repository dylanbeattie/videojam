using VideoJam.Engine;
using VideoJam.Model;

namespace VideoJam.Tests;

/// <summary>Unit tests for <see cref="DisplayManager"/>.</summary>
public sealed class DisplayManagerTests {
	// ── ResolveDisplayIndex ───────────────────────────────────────────────────

	[Fact]
	public void ResolveDisplayIndex_SuffixFoundInRouting_ReturnsMappedIndex() {
		var routing = new Dictionary<string, int> { ["_lyrics"] = 1, ["_visuals"] = 2 };

		int result = DisplayManager.ResolveDisplayIndex("_lyrics", routing);

		Assert.Equal(1, result);
	}

	[Fact]
	public void ResolveDisplayIndex_SuffixNotInRouting_ReturnsPrimaryDisplayIndex() {
		var routing = new Dictionary<string, int> { ["_lyrics"] = 1 };

		int result = DisplayManager.ResolveDisplayIndex("_unknown", routing);

		Assert.Equal(DisplayManager.PrimaryDisplayIndex, result);
	}

	[Fact]
	public void ResolveDisplayIndex_EmptyRouting_ReturnsPrimaryDisplayIndex() {
		int result = DisplayManager.ResolveDisplayIndex("_lyrics", new Dictionary<string, int>());

		Assert.Equal(DisplayManager.PrimaryDisplayIndex, result);
	}

	[Fact]
	public void ResolveDisplayIndex_EmptySuffixNotInRouting_ReturnsPrimaryDisplayIndex() {
		var routing = new Dictionary<string, int> { ["_lyrics"] = 1 };

		int result = DisplayManager.ResolveDisplayIndex(string.Empty, routing);

		Assert.Equal(DisplayManager.PrimaryDisplayIndex, result);
	}

	// ── GetRequiredDisplayIndices ─────────────────────────────────────────────

	[Fact]
	public void GetRequiredDisplayIndices_DuplicateIndices_ReturnsDistinctSet() {
		var manifest = new SongManifest(
			SongName: "test",
			Folder: new DirectoryInfo(Path.GetTempPath()),
			AudioChannels: [],
			VideoFiles: [
				new VideoFileManifest(new FileInfo("a.mp4"), DisplayIndex: 0, Suffix: "_a"),
				new VideoFileManifest(new FileInfo("b.mp4"), DisplayIndex: 1, Suffix: "_b"),
				new VideoFileManifest(new FileInfo("c.mp4"), DisplayIndex: 1, Suffix: "_c"),
			]);

		IReadOnlyCollection<int> result = DisplayManager.GetRequiredDisplayIndices(manifest);

		Assert.Equal(2, result.Count);
		Assert.Contains(0, result);
		Assert.Contains(1, result);
	}

	[Fact]
	public void GetRequiredDisplayIndices_NoVideoFiles_ReturnsEmptyCollection() {
		var manifest = new SongManifest(
			SongName: "test",
			Folder: new DirectoryInfo(Path.GetTempPath()),
			AudioChannels: [],
			VideoFiles: []);

		IReadOnlyCollection<int> result = DisplayManager.GetRequiredDisplayIndices(manifest);

		Assert.Empty(result);
	}

	// ── PrimaryDisplayIndex ───────────────────────────────────────────────────

	[Fact]
	public void PrimaryDisplayIndex_IsZero() {
		Assert.Equal(0, DisplayManager.PrimaryDisplayIndex);
	}
}
