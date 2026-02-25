namespace VideoJam.Model;

/// <summary>Playback state machine states.</summary>
internal enum PlaybackState {
	Idle,
	Cued,
	Playing,
	Paused,
}

/// <summary>
/// Runtime application state (not persisted).
/// Stub — full implementation in Phase 6.
/// </summary>
internal sealed class AppState {
}