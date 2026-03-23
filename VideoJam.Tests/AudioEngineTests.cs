using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using VideoJam.Engine;
using VideoJam.Model;
using Xunit;

namespace VideoJam.Tests;

/// <summary>Unit tests for <see cref="AudioEngine.ApplyChannelSettings"/>.</summary>
/// <remarks>
/// <para>
/// <see cref="AudioEngine.Load"/> requires real audio files and a live WASAPI device,
/// so these tests bypass it entirely. The private <c>_volumeProviders</c> dictionary is
/// populated directly via reflection, which lets us exercise
/// <see cref="AudioEngine.ApplyChannelSettings"/> in complete isolation.
/// </para>
/// </remarks>
public sealed class AudioEngineTests : IDisposable {
	// ── Infrastructure ────────────────────────────────────────────────────────

	/// <summary>
	/// Stereo 44 100 Hz float format — matches <c>AudioEngine.mixFormat</c> so that
	/// <see cref="VolumeSampleProvider"/> can be constructed without complaint.
	/// </summary>
	private static readonly WaveFormat TestFormat =
		WaveFormat.CreateIeeeFloatWaveFormat(44_100, 2);

	private static readonly FieldInfo VolumeProvidersField =
		typeof(AudioEngine).GetField(
			"_volumeProviders",
			BindingFlags.NonPublic | BindingFlags.Instance)
		?? throw new InvalidOperationException(
			"Could not reflect '_volumeProviders' on AudioEngine. " +
			"Has the field been renamed?");

	private readonly AudioEngine engine =
		new(NullLogger<AudioEngine>.Instance);

	public void Dispose() => engine.Dispose();

	// ── Helpers ───────────────────────────────────────────────────────────────

	/// <summary>
	/// Returns the live <c>_volumeProviders</c> dictionary from <paramref name="audioEngine"/>
	/// via reflection, so tests can seed entries without calling <c>Load()</c>.
	/// </summary>
	private static Dictionary<string, VolumeSampleProvider> GetProviders(AudioEngine audioEngine) =>
		(Dictionary<string, VolumeSampleProvider>)VolumeProvidersField.GetValue(audioEngine)!;

	/// <summary>
	/// Creates a <see cref="VolumeSampleProvider"/> backed by a <see cref="SilenceProvider"/>,
	/// seeded to <paramref name="initialVolume"/>.
	/// </summary>
	private static VolumeSampleProvider MakeProvider(float initialVolume = 1.0f) =>
		new(new SilenceProvider(TestFormat)) { Volume = initialVolume };

	// ── Tests ─────────────────────────────────────────────────────────────────

	/// <summary>
	/// A channel-settings entry with a specific level and Muted=false
	/// must result in that exact level being written to the volume provider.
	/// </summary>
	[Fact]
	public void ApplyChannelSettings_LevelApplied_SetsProviderVolumeToSpecifiedLevel() {
		// Arrange
		const string CHANNEL_ID = "drums.wav";
		const float EXPECTED_LEVEL = 0.5f;

		var provider = MakeProvider(initialVolume: 1.0f);
		GetProviders(engine)[CHANNEL_ID] = provider;

		var settings = new Dictionary<string, ChannelSettings> {
			[CHANNEL_ID] = new ChannelSettings { Level = EXPECTED_LEVEL, Muted = false },
		};

		// Act
		engine.ApplyChannelSettings(settings);

		// Assert
		Assert.Equal(EXPECTED_LEVEL, provider.Volume);
	}

	/// <summary>
	/// When <see cref="ChannelSettings.Muted"/> is <c>true</c>,
	/// the provider volume must be set to zero regardless of the level value.
	/// </summary>
	[Fact]
	public void ApplyChannelSettings_MutedTrue_SetsProviderVolumeToZero() {
		// Arrange
		const string CHANNEL_ID = "keys.wav";

		var provider = MakeProvider(initialVolume: 0.8f);
		GetProviders(engine)[CHANNEL_ID] = provider;

		var settings = new Dictionary<string, ChannelSettings> {
			[CHANNEL_ID] = new ChannelSettings { Level = 0.8f, Muted = true },
		};

		// Act
		engine.ApplyChannelSettings(settings);

		// Assert
		Assert.Equal(0f, provider.Volume);
	}

	/// <summary>
	/// After muting a channel, calling <see cref="AudioEngine.ApplyChannelSettings"/>
	/// with <c>Muted=false</c> must restore the volume to the specified level
	/// rather than leaving it at zero.
	/// </summary>
	[Fact]
	public void ApplyChannelSettings_UnmuteAfterMute_RestoresLevelFromSettings() {
		// Arrange
		const string CHANNEL_ID = "bass.wav";
		const float RESTORED_LEVEL = 0.75f;

		// Start the provider at 0f — simulating the muted state from a previous apply.
		var provider = MakeProvider(initialVolume: 0f);
		GetProviders(engine)[CHANNEL_ID] = provider;

		var settings = new Dictionary<string, ChannelSettings> {
			[CHANNEL_ID] = new ChannelSettings { Level = RESTORED_LEVEL, Muted = false },
		};

		// Act
		engine.ApplyChannelSettings(settings);

		// Assert
		Assert.Equal(RESTORED_LEVEL, provider.Volume);
	}

	/// <summary>
	/// Passing a settings dictionary that contains a key which has no corresponding entry
	/// in the loaded providers must not throw. Channels in settings but not in the pipeline
	/// are silently ignored. Channels in the pipeline but not in settings fall back to
	/// DEFAULT_CHANNEL_LEVEL (1.0f).
	/// </summary>
	[Fact]
	public void ApplyChannelSettings_SettingsKeyNotInProviders_DoesNotThrow() {
		// Arrange — one real provider loaded under "guitar.wav"
		const string LOADED_CHANNEL = "guitar.wav";
		const string UNKNOWN_CHANNEL = "theremin.wav"; // not in _volumeProviders

		var provider = MakeProvider(initialVolume: 1.0f);
		GetProviders(engine)[LOADED_CHANNEL] = provider;

		// Settings contain only the unknown channel; nothing for the loaded one.
		var settings = new Dictionary<string, ChannelSettings> {
			[UNKNOWN_CHANNEL] = new ChannelSettings { Level = 0.5f, Muted = false },
		};

		// Act & Assert — must not throw
		var ex = Record.Exception(() => engine.ApplyChannelSettings(settings));
		Assert.Null(ex);

		// The loaded provider has no matching settings entry, so it gets DEFAULT_CHANNEL_LEVEL.
		Assert.Equal(1.0f, provider.Volume);
	}

	/// <summary>
	/// Calling <see cref="AudioEngine.ApplyChannelSettings"/> before <c>Load()</c>
	/// (i.e. when <c>_volumeProviders</c> is empty) must not throw.
	/// </summary>
	[Fact]
	public void ApplyChannelSettings_EmptyProviders_DoesNotThrow() {
		// Arrange — _volumeProviders is empty by default before Load() is called.
		var settings = new Dictionary<string, ChannelSettings> {
			["any-channel.wav"] = new ChannelSettings { Level = 0.9f, Muted = false },
		};

		// Act & Assert
		var ex = Record.Exception(() => engine.ApplyChannelSettings(settings));
		Assert.Null(ex);
	}
}
