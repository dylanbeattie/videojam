using System.Windows;
using System.Windows.Media.Imaging;

namespace VideoJam.UI;

/// <summary>
/// A borderless, topmost WPF window that covers a single physical display.
/// Each instance is associated with one LibVLC MediaPlayer which renders
/// directly into the window's HWND.
/// Stub — full implementation in Phase 2.
/// </summary>
public partial class VlcDisplayWindow : Window {
	/// <summary>The Win32 window handle, available after the window is loaded.</summary>
	public nint Hwnd { get; private set; }

	/// <inheritdoc />
	public VlcDisplayWindow() {
		InitializeComponent();
		Loaded += OnLoaded;
	}

	private void OnLoaded(object sender, RoutedEventArgs e) {
		Hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
	}

	/// <summary>Shows the fallback PNG image; hides the VLC render surface.</summary>
	public void ShowFallback(BitmapImage image) {
		FallbackImage.Source = image;
		FallbackImage.Visibility = Visibility.Visible;
	}

	/// <summary>Hides the fallback PNG; makes the VLC render surface the foreground.</summary>
	public void ShowVideo() {
		FallbackImage.Visibility = Visibility.Hidden;
	}
}