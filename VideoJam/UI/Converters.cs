using System.Globalization;
using System.Windows;
using System.Windows.Controls;
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
/// Multi-value converter that returns the 1-based position of an item within an
/// <see cref="ItemsControl"/>. Avoids the <c>AlternationIndex</c> approach, which is
/// unreliable during drag-and-drop operations.
/// </summary>
/// <remarks>
/// Bind as: <c>{ Binding }, { Binding RelativeSource AncestorType=ListBox }</c>.
/// Returns 0 when the item is not found.
/// </remarks>
public sealed class ItemIndexConverter : IMultiValueConverter {
	/// <inheritdoc />
	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) {
		if (values.Length >= 2 && values[1] is ItemsControl ic) {
			int idx = ic.Items.IndexOf(values[0]);
			return idx >= 0 ? idx + 1 : 0;
		}
		return 0;
	}

	/// <inheritdoc />
	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
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
/// <see langword="true"/> → <see cref="VideoAudioColor"/> (muted blue-grey); <see langword="false"/> → default foreground.
/// Used to distinguish video-audio channel rows in the mixer panel.
/// </summary>
[ValueConversion(typeof(bool), typeof(Brush))]
public sealed class VideoAudioForegroundConverter : IValueConverter {
	/// <summary>Colour applied to video-audio channel name labels.</summary>
	private static readonly Color VideoAudioColor = Color.FromRgb(0x80, 0x80, 0xA0);

	private static readonly Brush VideoAudioBrush = new SolidColorBrush(VideoAudioColor);

	/// <inheritdoc />
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
		value is true ? VideoAudioBrush : SystemColors.ControlTextBrush;

	/// <inheritdoc />
	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
		throw new NotSupportedException();
}
