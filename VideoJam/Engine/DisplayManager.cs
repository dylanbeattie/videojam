using System.Windows;
using VideoJam.Model;
using VideoJam.UI;

namespace VideoJam.Engine;

/// <summary>
/// Static utility that enumerates physical displays, resolves filename-suffix-to-display-index
/// routing, and creates correctly-positioned <see cref="VlcDisplayWindow"/> instances.
/// </summary>
/// <remarks>
/// <para>
/// Display indices map directly to positions in <see cref="System.Windows.Forms.Screen.AllScreens"/>:
/// index 0 is the primary display, higher indices are secondary displays in the order Windows
/// reports them.
/// </para>
/// <para>
/// <b>Stability caveat:</b> The order of <see cref="System.Windows.Forms.Screen.AllScreens"/>
/// is not guaranteed to be stable across reboots or driver updates. A routing entry such as
/// <c>"_lyrics" → 1</c> may address a different physical projector after a reboot or hardware
/// change. Operators should verify display assignment after any hardware reconfiguration.
/// A stable display-identity scheme (e.g. EDID-based naming) is deferred to a future phase.
/// </para>
/// </remarks>
internal static class DisplayManager {
	// ── Constants ─────────────────────────────────────────────────────────────

	/// <summary>
	/// Display index for the primary physical display.
	/// All other display indices are relative to this value.
	/// </summary>
	// Phase 4+ note: when DisplayManager gains EDID-based identity, promote this
	// to a well-known display descriptor rather than a bare integer.
	public const int PrimaryDisplayIndex = 0;

	// ── Routing ───────────────────────────────────────────────────────────────

	/// <summary>
	/// Resolves a video filename suffix to a display index using the provided routing table.
	/// </summary>
	/// <param name="suffix">
	/// The underscore-prefixed filename suffix extracted by <see cref="Services.SongScanner"/>
	/// (e.g. <c>"_lyrics"</c>, <c>"_visuals"</c>). May be an empty string.
	/// </param>
	/// <param name="routing">
	/// A dictionary mapping suffix strings to display indices. May be empty but not <see langword="null"/>.
	/// </param>
	/// <returns>
	/// The display index from <paramref name="routing"/> if <paramref name="suffix"/> is present;
	/// otherwise <see cref="PrimaryDisplayIndex"/>.
	/// </returns>
	public static int ResolveDisplayIndex(string suffix, IReadOnlyDictionary<string, int> routing) =>
		routing.TryGetValue(suffix, out int index) ? index : PrimaryDisplayIndex;

	// ── Manifest helpers ──────────────────────────────────────────────────────

	/// <summary>
	/// Returns the distinct set of display indices referenced by video files in
	/// <paramref name="manifest"/>.
	/// </summary>
	/// <param name="manifest">The song manifest to inspect.</param>
	/// <returns>
	/// A collection of unique display indices; empty if the manifest contains no video files.
	/// </returns>
	public static IReadOnlyCollection<int> GetRequiredDisplayIndices(SongManifest manifest) =>
		manifest.VideoFiles
			.Select(v => v.DisplayIndex)
			.Distinct()
			.ToList()
			.AsReadOnly();

	// ── Window factory ────────────────────────────────────────────────────────

	/// <summary>
	/// Creates, positions, and shows a <see cref="VlcDisplayWindow"/> covering the physical
	/// display at <paramref name="displayIndex"/>.
	/// </summary>
	/// <param name="displayIndex">
	/// Zero-based index into <see cref="System.Windows.Forms.Screen.AllScreens"/>.
	/// 0 = primary display.
	/// </param>
	/// <returns>A visible <see cref="VlcDisplayWindow"/> covering the requested display.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="displayIndex"/> is negative or greater than or equal to the
	/// number of connected displays.
	/// </exception>
	public static VlcDisplayWindow CreateWindowForDisplay(int displayIndex) {
		System.Windows.Forms.Screen[] screens = System.Windows.Forms.Screen.AllScreens;

		if (displayIndex < 0 || displayIndex >= screens.Length)
			throw new ArgumentOutOfRangeException(
				nameof(displayIndex),
				$"Display index {displayIndex} is out of range. " +
				$"{screens.Length} display(s) are currently available.");

		System.Windows.Forms.Screen screen = screens[displayIndex];

		// WPF window coordinates are expressed in device-independent pixels (DIPs)
		// relative to the primary monitor's DPI coordinate space. Convert the target
		// screen's physical pixel bounds to DIPs using the primary screen's DPI scale.
		// WPF's PerMonitorV2 awareness will apply the correct per-monitor DPI once
		// the window is shown on its target display.
		double dpiScaleX = System.Windows.Forms.Screen.PrimaryScreen!.Bounds.Width
			/ SystemParameters.PrimaryScreenWidth;
		double dpiScaleY = System.Windows.Forms.Screen.PrimaryScreen!.Bounds.Height
			/ SystemParameters.PrimaryScreenHeight;

		double left   = screen.Bounds.Left   / dpiScaleX;
		double top    = screen.Bounds.Top    / dpiScaleY;
		double width  = screen.Bounds.Width  / dpiScaleX;
		double height = screen.Bounds.Height / dpiScaleY;

		var window = new VlcDisplayWindow();
		window.SetBounds(left, top, width, height);
		window.Show();
		return window;
	}
}
