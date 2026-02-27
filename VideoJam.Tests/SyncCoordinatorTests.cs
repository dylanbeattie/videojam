using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VideoJam.Engine;

namespace VideoJam.Tests;

/// <summary>Unit tests for <see cref="SyncCoordinator"/>.</summary>
public sealed class SyncCoordinatorTests {
	// ── Stubs ─────────────────────────────────────────────────────────────────

	/// <summary>
	/// Stub <see cref="IAudioPlayback"/> that records calls and returns a controlled timestamp.
	/// </summary>
	private sealed class FakeAudio : IAudioPlayback {
		public int PlayCallCount { get; private set; }
		public long ReturnTimestamp { get; set; } = 100L;

		public long Play() {
			PlayCallCount++;
			return ReturnTimestamp;
		}
	}

	/// <summary>
	/// Stub <see cref="IVideoPlayback"/> that records the timestamp it received.
	/// </summary>
	private sealed class FakeVideo : IVideoPlayback {
		public int PlayCallCount { get; private set; }
		public long? ReceivedTimestamp { get; private set; }

		public void Play(long audioStartTimestamp) {
			PlayCallCount++;
			ReceivedTimestamp = audioStartTimestamp;
		}
	}

	// ── Helpers ───────────────────────────────────────────────────────────────

	private static SyncCoordinator CreateCoordinator() =>
		new(NullLogger<SyncCoordinator>.Instance);

	// ── Tests ─────────────────────────────────────────────────────────────────

	[Fact]
	public void Start_CallsAudioPlayFirst() {
		var audio = new FakeAudio();
		var video = new FakeVideo();
		var coordinator = CreateCoordinator();

		coordinator.Start(audio, video);

		Assert.Equal(1, audio.PlayCallCount);
	}

	[Fact]
	public void Start_PassesAudioTimestampToVideoPlay() {
		const long expectedTimestamp = 42_000L;
		var audio = new FakeAudio { ReturnTimestamp = expectedTimestamp };
		var video = new FakeVideo();
		var coordinator = CreateCoordinator();

		coordinator.Start(audio, video);

		Assert.Equal(1, video.PlayCallCount);
		Assert.Equal(expectedTimestamp, video.ReceivedTimestamp);
	}

	[Fact]
	public void Start_IsStateless_CanBeCalledMultipleTimesOnSameInstance() {
		var coordinator = CreateCoordinator();

		for (int i = 0; i < 3; i++) {
			var audio = new FakeAudio();
			var video = new FakeVideo();

			coordinator.Start(audio, video);

			Assert.Equal(1, audio.PlayCallCount);
			Assert.Equal(1, video.PlayCallCount);
		}
	}

	[Fact]
	public void Start_CallsVideoPlayExactlyOnce() {
		var audio = new FakeAudio();
		var video = new FakeVideo();
		var coordinator = CreateCoordinator();

		coordinator.Start(audio, video);

		Assert.Equal(1, video.PlayCallCount);
	}

	[Fact]
	public void Start_WithDifferentTimestampsPerCall_EachVideoReceivesItsOwnTimestamp() {
		var coordinator = CreateCoordinator();

		long[] expectedTimestamps = [10L, 20L, 30L];

		foreach (long ts in expectedTimestamps) {
			var audio = new FakeAudio { ReturnTimestamp = ts };
			var video = new FakeVideo();

			coordinator.Start(audio, video);

			Assert.Equal(ts, video.ReceivedTimestamp);
		}
	}
}
