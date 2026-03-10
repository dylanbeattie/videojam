using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VideoJam.UI.ViewModels;

/// <summary>
/// Represents a per-display fallback PNG assignment for binding in the fallback image section.
/// </summary>
public sealed class FallbackImageEntryViewModel : INotifyPropertyChanged {
	private string _imagePath;
	private readonly Action _onChanged;

	/// <summary>Placeholder shown when no image has been assigned.</summary>
	private const string NoImagePlaceholder = "(none)";

	/// <summary>
	/// Initialises a new <see cref="FallbackImageEntryViewModel"/>.
	/// </summary>
	/// <param name="displayIndex">The 0-based display index this entry covers.</param>
	/// <param name="displayName">The OS-reported display device name.</param>
	/// <param name="imagePath">
	/// Current fallback PNG path, or <see langword="null"/> / empty if none assigned.
	/// </param>
	/// <param name="onChanged">Callback invoked whenever the image path changes.</param>
	public FallbackImageEntryViewModel(
		int displayIndex,
		string displayName,
		string? imagePath,
		Action onChanged) {
		DisplayIndex = displayIndex;
		DisplayName = displayName;
		_imagePath = string.IsNullOrEmpty(imagePath) ? NoImagePlaceholder : imagePath;
		_onChanged = onChanged;
	}

	/// <summary>0-based display index.</summary>
	public int DisplayIndex { get; }

	/// <summary>OS-reported display device name (e.g. <c>\\.\DISPLAY1</c>).</summary>
	public string DisplayName { get; }

	/// <summary>
	/// Label shown in the UI: <c>"Display {index} — {name}"</c>.
	/// </summary>
	public string Label => $"Display {DisplayIndex} — {DisplayName}";

	/// <summary>
	/// The fallback PNG path, or <c>"(none)"</c> when no image is assigned.
	/// Two-way bound in the UI; set via the Browse button handler.
	/// </summary>
	public string ImagePath {
		get => _imagePath;
		set {
			var newValue = string.IsNullOrEmpty(value) ? NoImagePlaceholder : value;
			if (_imagePath == newValue) return;
			_imagePath = newValue;
			OnPropertyChanged();
			OnPropertyChanged(nameof(HasImage));
			_onChanged();
		}
	}

	/// <summary><see langword="true"/> when an image has been assigned.</summary>
	public bool HasImage => _imagePath != NoImagePlaceholder;

	/// <summary>
	/// Returns the raw file path, or <see langword="null"/> when no image is assigned.
	/// </summary>
	public string? RawPath => HasImage ? _imagePath : null;

	/// <inheritdoc />
	public event PropertyChangedEventHandler? PropertyChanged;

	private void OnPropertyChanged([CallerMemberName] string? name = null) =>
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
