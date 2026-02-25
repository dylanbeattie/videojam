using VideoJam.Model;

namespace VideoJam.Tests;

/// <summary>Unit tests for <see cref="ChannelSettings"/> defaults and mutation.</summary>
public sealed class ChannelSettingsTests {
	// ── 6.1 ──────────────────────────────────────────────────────────────────

	[Fact]
	public void DefaultConstructor_HasLevelOneAndUnmuted() {
		var settings = new ChannelSettings();

		Assert.Equal(1.0f, settings.Level);
		Assert.False(settings.Muted);
	}

	// ── 6.2 ──────────────────────────────────────────────────────────────────

	[Fact]
	public void Level_RoundTrips() {
		var settings = new ChannelSettings { Level = 0.5f };

		Assert.Equal(0.5f, settings.Level);
	}

	// ── 6.3 ──────────────────────────────────────────────────────────────────

	[Fact]
	public void Muted_RoundTrips() {
		var settings = new ChannelSettings { Muted = true };

		Assert.True(settings.Muted);
	}
}