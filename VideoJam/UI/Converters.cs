using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace VideoJam.UI;

/// <summary>Converts an integer by adding one. Used to display 1-based indices.</summary>
[ValueConversion(typeof(int), typeof(int))]
public sealed class AddOneConverter : IValueConverter {
	/// <inheritdoc />
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
		value is int i ? i + 1 : value;

	/// <inheritdoc />
	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
		throw new NotSupportedException();
}

/// <summary>
/// Converts a <see cref="bool"/> to a <see cref="FontStyle"/>.
/// <see langword="true"/> → <see cref="FontStyles.Italic"/>; <see langword="false"/> → <see cref="FontStyles.Normal"/>.
/// Used to italicise video-audio channel rows in the mixer panel.
/// </summary>
[ValueConversion(typeof(bool), typeof(FontStyle))]
public sealed class BoolToFontStyleConverter : IValueConverter {
	/// <inheritdoc />
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
		value is true ? FontStyles.Italic : FontStyles.Normal;

	/// <inheritdoc />
	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
		throw new NotSupportedException();
}

/// <summary>
/// Converts a <see cref="bool"/> to a <see cref="Brush"/> for visual emphasis.
/// <see langword="true"/> → dim gray (video audio channel); <see langword="false"/> → default foreground.
/// </summary>
[ValueConversion(typeof(bool), typeof(Brush))]
public sealed class VideoAudioForegroundConverter : IValueConverter {
	private static readonly Brush VideoAudioBrush = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0xA0));

	/// <inheritdoc />
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
		value is true ? VideoAudioBrush : SystemColors.ControlTextBrush;

	/// <inheritdoc />
	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
		throw new NotSupportedException();
}
