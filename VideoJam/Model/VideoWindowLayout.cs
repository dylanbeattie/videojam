namespace VideoJam.Model;

/// <summary>
/// Persisted layout for a single video window, keyed by slot index in the show file.
/// </summary>
public sealed class VideoWindowLayout {
	/// <summary>Left edge in device-independent pixels.</summary>
	public double Left { get; set; }

	/// <summary>Top edge in device-independent pixels.</summary>
	public double Top { get; set; }

	/// <summary>Width in device-independent pixels.</summary>
	public double Width { get; set; } = 640;

	/// <summary>Height in device-independent pixels.</summary>
	public double Height { get; set; } = 360;

	/// <summary>Whether the window was maximised when the layout was saved.</summary>
	public bool IsMaximised { get; set; }
}
