using VideoJam.Services;
using Xunit;

namespace VideoJam.Tests;

public sealed class PathResolverTests {
	// ── MakeRelative ─────────────────────────────────────────────────────────

	[Fact]
	public void MakeRelative_TargetInSameDirectory_ReturnsSingleComponent() {
		// Arrange
		const string dir = @"C:\shows";
		const string target = @"C:\shows\song1";

		// Act
		string result = PathResolver.MakeRelative(target, dir);

		// Assert
		Assert.Equal("song1", result);
	}

	[Fact]
	public void MakeRelative_TargetInSubdirectory_ReturnsForwardSlashPath() {
		// Arrange
		const string dir = @"C:\shows";
		const string target = @"C:\shows\songs\song1";

		// Act
		string result = PathResolver.MakeRelative(target, dir);

		// Assert
		Assert.Equal("songs/song1", result);
	}

	[Fact]
	public void MakeRelative_TargetInSiblingDirectory_ReturnsParentTraversalPath() {
		// Arrange
		const string dir = @"C:\shows";
		const string target = @"C:\media\songs\song1";

		// Act
		string result = PathResolver.MakeRelative(target, dir);

		// Assert
		Assert.Equal("../media/songs/song1", result);
	}

	[Fact]
	public void MakeRelative_ResultContainsNoBackslashes() {
		// Arrange
		const string dir = @"C:\shows\tour";
		const string target = @"C:\shows\tour\setA\song2";

		// Act
		string result = PathResolver.MakeRelative(target, dir);

		// Assert
		Assert.DoesNotContain('\\', result);
	}

	// ── Resolve ──────────────────────────────────────────────────────────────

	[Fact]
	public void Resolve_SimplePath_ReturnsCorrectAbsolutePath() {
		// Arrange
		const string dir = @"C:\shows";
		const string relative = "song1";

		// Act
		string result = PathResolver.Resolve(relative, dir);

		// Assert
		Assert.Equal(Path.GetFullPath(@"C:\shows\song1"), result);
	}

	[Fact]
	public void Resolve_SubdirectoryPath_ReturnsCorrectAbsolutePath() {
		// Arrange
		const string dir = @"C:\shows";
		const string relative = "songs/song1";

		// Act
		string result = PathResolver.Resolve(relative, dir);

		// Assert
		Assert.Equal(Path.GetFullPath(@"C:\shows\songs\song1"), result);
	}

	[Fact]
	public void Resolve_ParentTraversalPath_CollapsesDoubleDot() {
		// Arrange
		const string dir = @"C:\shows";
		const string relative = "../media/songs/song1";

		// Act
		string result = PathResolver.Resolve(relative, dir);

		// Assert
		Assert.Equal(Path.GetFullPath(@"C:\media\songs\song1"), result);
		Assert.DoesNotContain("..", result);
	}

	// ── Round-trip ───────────────────────────────────────────────────────────

	[Fact]
	public void RoundTrip_MakeRelativeThenResolve_RecoverOriginalPath() {
		// Arrange
		const string dir = @"C:\shows\2025-tour";
		const string absolutePath = @"C:\media\songs\opener";

		// Act
		string relative = PathResolver.MakeRelative(absolutePath, dir);
		string resolved = PathResolver.Resolve(relative, dir);

		// Assert
		Assert.Equal(Path.GetFullPath(absolutePath), resolved);
	}
}
