using System.Runtime.InteropServices;
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
		// If the window is in fullscreen, return the saved pre-fullscreen layout
		// so that persisted positions are never polluted with fullscreen dimensions.
		if (_isFullscreen) {
			return new VideoWindowLayout {
				Left = _preFullscreenLeft,
				Top = _preFullscreenTop,
				Width = _preFullscreenWidth,
				Height = _preFullscreenHeight,
				IsMaximised = _preFullscreenState == WindowState.Maximized,
			};
		}

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

	// ── Win32 interop ─────────────────────────────────────────────────────────

	/// <summary>
	/// Sent to a parent window when a child window receives a mouse button event.
	/// LibVLC creates a native child window; WPF events never fire on it, but
	/// WM_PARENTNOTIFY reliably propagates to the parent HWND.
	/// </summary>
	private const int WM_PARENTNOTIFY = 0x0210;

	/// <summary>Left mouse button down — the low word of WM_PARENTNOTIFY wParam.</summary>
	private const int WM_LBUTTONDOWN = 0x0201;

	[DllImport("user32.dll")]
	private static extern uint GetDoubleClickTime();

	// ── Private state ─────────────────────────────────────────────────────────

	private bool _forceClose;
	private bool _isFullscreen;

	/// <summary>Timestamp of the last single click on the VLC surface, for manual double-click detection.</summary>
	private DateTime _lastVlcClickTime = DateTime.MinValue;
	private double _preFullscreenLeft;
	private double _preFullscreenTop;
	private double _preFullscreenWidth;
	private double _preFullscreenHeight;
	private WindowState _preFullscreenState;

	/// <summary>
	/// Toggles between windowed and true fullscreen mode.
	/// Fullscreen is achieved by setting <see cref="WindowStyle"/> to <c>None</c>,
	/// <see cref="ResizeMode"/> to <c>NoResize</c>, and <see cref="WindowState"/> to
	/// <c>Maximized</c> — covering the full physical display including the taskbar.
	/// Exit restores the exact pre-fullscreen bounds and window state.
	/// </summary>
	private void ToggleFullscreen() {
		if (!_isFullscreen) {
			// Capture current windowed state before making any changes.
			_preFullscreenLeft = Left;
			_preFullscreenTop = Top;
			_preFullscreenWidth = Width;
			_preFullscreenHeight = Height;
			_preFullscreenState = WindowState;

			// Enter fullscreen: chrome must be removed before maximising
			// so the window covers the taskbar.
			WindowStyle = WindowStyle.None;
			ResizeMode = ResizeMode.NoResize;
			WindowState = WindowState.Maximized;
			_isFullscreen = true;
		} else {
			// Exit fullscreen: WindowState must be set to Normal before
			// restoring WindowStyle, otherwise WPF miscalculates the restore position.
			WindowState = WindowState.Normal;
			WindowStyle = WindowStyle.SingleBorderWindow;
			ResizeMode = ResizeMode.CanResize;
			Left = _preFullscreenLeft;
			Top = _preFullscreenTop;
			Width = _preFullscreenWidth;
			Height = _preFullscreenHeight;
			WindowState = _preFullscreenState;
			_isFullscreen = false;
		}
	}

	private void OnLoaded(object sender, RoutedEventArgs e) {
		Hwnd = new WindowInteropHelper(this).Handle;
		// Hook the Win32 message pump so we can detect double-clicks on the native
		// LibVLC child window, which swallows all WPF mouse events.
		HwndSource.FromHwnd(Hwnd)?.AddHook(WndProc);
	}

	/// <summary>
	/// Win32 message hook. Intercepts <c>WM_PARENTNOTIFY</c> to detect double-clicks
	/// on the LibVLC child window, which is a native Win32 surface invisible to WPF.
	/// </summary>
	private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
		if (msg == WM_PARENTNOTIFY && (wParam.ToInt32() & 0xFFFF) == WM_LBUTTONDOWN) {
			var now = DateTime.UtcNow;
			if ((now - _lastVlcClickTime).TotalMilliseconds <= GetDoubleClickTime()) {
				ToggleFullscreen();
				_lastVlcClickTime = DateTime.MinValue;
			} else {
				_lastVlcClickTime = now;
			}
		}
		return IntPtr.Zero;
	}

	/// <summary>
	/// Handles double-click to toggle fullscreen. This is view-lifecycle glue;
	/// no business logic resides here.
	/// </summary>
	protected override void OnMouseDoubleClick(MouseButtonEventArgs e) {
		ToggleFullscreen();
		e.Handled = true;
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