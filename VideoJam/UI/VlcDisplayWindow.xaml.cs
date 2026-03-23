using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using VideoJam.Model;

namespace VideoJam.UI;

/// <summary>
/// A freely positionable, resizable WPF window used to display video for a single slot.
/// Each instance is associated with one LibVLC <c>MediaPlayer</c> which renders
/// directly into the window's HWND via <see cref="Hwnd"/>.
/// </summary>
/// <remarks>
/// The window supports two display states:
/// <list type="bullet">
///   <item><b>Fallback</b> — a static PNG image fills the window (default state)</item>
///   <item><b>Video</b> — the LibVLC render surface is the foreground layer</item>
/// </list>
/// Windows are keyed by <see cref="SlotIndex"/> and reused across songs. Closing the
/// window hides it rather than destroying it; use <see cref="ForceClose"/> during
/// application shutdown to bypass this guard.
/// <para>
/// Pressing <c>Ctrl+Tab</c> while this window has keyboard focus activates
/// <see cref="Application.Current"/> <c>.MainWindow</c>, returning the operator to the
/// operator UI without requiring the mouse.
/// </para>
/// </remarks>
public partial class VlcDisplayWindow : Window {
	// ── Public API ────────────────────────────────────────────────────────────

	/// <summary>
	/// The Win32 window handle. Available after the <see cref="Window.Loaded"/> event fires.
	/// Zero before the window has been created by the OS.
	/// </summary>
	public nint Hwnd { get; private set; }

	/// <summary>The slot index this window represents. Used for the title bar.</summary>
	public int SlotIndex { get; set; }

	/// <inheritdoc />
	public VlcDisplayWindow() {
		InitializeComponent();
		Loaded += OnLoaded;
	}

	/// <summary>Updates the window title to reflect the slot index.</summary>
	public void UpdateTitle() {
		Title = $"VideoJam \u2014 Video {SlotIndex + 1}";
	}

	/// <summary>
	/// Closes the window permanently, bypassing the hide-on-close behaviour.
	/// Used only during application shutdown.
	/// </summary>
	public void ForceClose() {
		_forceClose = true;
		Close();
	}

	/// <summary>
	/// Positions and sizes the window using device-independent units.
	/// Call this before <see cref="Window.Show"/> to set the initial position.
	/// </summary>
	/// <param name="left">Left edge in device-independent pixels (physical pixels ÷ DPI scale).</param>
	/// <param name="top">Top edge in device-independent pixels.</param>
	/// <param name="width">Width in device-independent pixels.</param>
	/// <param name="height">Height in device-independent pixels.</param>
	public void SetBounds(double left, double top, double width, double height) {
		Left = left;
		Top = top;
		Width = width;
		Height = height;
	}

	/// <summary>
	/// Captures the current window position and size for persistence.
	/// Uses <see cref="RestoreBounds"/> so that the non-maximised position is captured
	/// even when the window is currently maximised.
	/// </summary>
	public VideoWindowLayout GetLayout() {
		var bounds = WindowState == WindowState.Maximized
			? RestoreBounds
			: new Rect(Left, Top, Width, Height);
		return new VideoWindowLayout {
			Left = bounds.Left,
			Top = bounds.Top,
			Width = bounds.Width,
			Height = bounds.Height,
			IsMaximised = WindowState == WindowState.Maximized,
		};
	}

	/// <summary>
	/// Applies a saved layout to this window.
	/// </summary>
	public void ApplyLayout(VideoWindowLayout layout) {
		Left = layout.Left;
		Top = layout.Top;
		Width = layout.Width;
		Height = layout.Height;
		if (layout.IsMaximised)
			WindowState = WindowState.Maximized;
	}

	/// <summary>
	/// Shows the fallback PNG image as the foreground layer; hides the VLC render surface.
	/// If <paramref name="image"/> is <see langword="null"/>, the window displays solid black.
	/// </summary>
	/// <param name="image">The bitmap to display, or <see langword="null"/> for solid black.</param>
	public void ShowFallback(BitmapImage? image) {
		FallbackImage.Source = image;
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

	private bool _forceClose;

	private void OnLoaded(object sender, RoutedEventArgs e) {
		Hwnd = new WindowInteropHelper(this).Handle;
	}

	/// <inheritdoc />
	protected override void OnClosing(System.ComponentModel.CancelEventArgs e) {
		if (!_forceClose) {
			e.Cancel = true;
			Hide();
			return;
		}
		base.OnClosing(e);
	}

	/// <summary>
	/// Handles <c>Ctrl+Tab</c> to return keyboard focus to the operator UI (<c>MainWindow</c>).
	/// This is view-lifecycle glue: no business logic resides here.
	/// </summary>
	private void OnKeyDown(object sender, KeyEventArgs e) {
		if (e.Key == Key.Tab && Keyboard.Modifiers == ModifierKeys.Control) {
			Application.Current.MainWindow?.Activate();
			e.Handled = true;
		}
	}
}