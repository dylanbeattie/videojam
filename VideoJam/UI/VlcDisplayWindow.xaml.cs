using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace VideoJam.UI;

/// <summary>
/// A borderless, topmost WPF window that covers a single physical display.
/// Each instance is associated with one LibVLC <c>MediaPlayer</c> which renders
/// directly into the window's HWND via <see cref="Hwnd"/>.
/// </summary>
/// <remarks>
/// The window supports two display states:
/// <list type="bullet">
///   <item><b>Fallback</b> — a static PNG image fills the window (default state)</item>
///   <item><b>Video</b> — the LibVLC render surface is the foreground layer</item>
/// </list>
/// Call <see cref="SetBounds"/> before <see cref="Window.Show"/> to position the window
/// on the correct physical display in device-independent units.
/// </remarks>
public partial class VlcDisplayWindow : Window {
	// ── Public API ────────────────────────────────────────────────────────────

	/// <summary>
	/// The Win32 window handle. Available after the <see cref="Window.Loaded"/> event fires.
	/// Zero before the window has been created by the OS.
	/// </summary>
	public nint Hwnd { get; private set; }

	/// <inheritdoc />
	public VlcDisplayWindow() {
		InitializeComponent();
		Loaded += OnLoaded;
	}

	/// <summary>
	/// Positions and sizes the window on the target display using device-independent units.
	/// Call this before <see cref="Window.Show"/> so the window appears on the correct screen.
	/// </summary>
	/// <param name="left">Left edge in device-independent pixels (physical pixels ÷ DPI scale).</param>
	/// <param name="top">Top edge in device-independent pixels.</param>
	/// <param name="width">Width in device-independent pixels.</param>
	/// <param name="height">Height in device-independent pixels.</param>
	public void SetBounds(double left, double top, double width, double height) {
		Left   = left;
		Top    = top;
		Width  = width;
		Height = height;
	}

	/// <summary>
	/// Shows the fallback PNG image as the foreground layer; hides the VLC render surface.
	/// If <paramref name="image"/> is <see langword="null"/>, the window displays solid black.
	/// </summary>
	/// <param name="image">The bitmap to display, or <see langword="null"/> for solid black.</param>
	public void ShowFallback(BitmapImage? image) {
		FallbackImage.Source     = image;
		FallbackImage.Visibility = Visibility.Visible;
	}

	/// <summary>
	/// Hides the fallback image layer; makes the VLC render surface the foreground.
	/// Call this after <see cref="VideoEngine"/> has pre-buffered and assigned its
	/// <c>MediaPlayer.Hwnd</c> to <see cref="Hwnd"/>.
	/// </summary>
	public void ShowVideo() {
		FallbackImage.Visibility = Visibility.Hidden;
	}

	// ── Private helpers ───────────────────────────────────────────────────────

	private void OnLoaded(object sender, RoutedEventArgs e) {
		Hwnd = new WindowInteropHelper(this).Handle;
	}
}
