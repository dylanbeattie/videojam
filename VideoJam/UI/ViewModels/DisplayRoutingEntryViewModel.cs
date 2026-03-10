using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VideoJam.UI.ViewModels;

/// <summary>
/// Represents a single suffix → display-index routing entry for binding in the routing table UI.
/// </summary>
public sealed class DisplayRoutingEntryViewModel : INotifyPropertyChanged {
	private string _suffix;
	private int _displayIndex;
	private readonly Action _onChanged;

	/// <summary>
	/// Initialises a new <see cref="DisplayRoutingEntryViewModel"/>.
	/// </summary>
	/// <param name="suffix">Initial file suffix (e.g. <c>"_lyrics"</c>).</param>
	/// <param name="displayIndex">Initial display index.</param>
	/// <param name="onChanged">Callback invoked whenever a property is mutated.</param>
	public DisplayRoutingEntryViewModel(string suffix, int displayIndex, Action onChanged) {
		_suffix = suffix;
		_displayIndex = displayIndex;
		_onChanged = onChanged;
	}

	/// <summary>Video filename suffix that triggers routing (e.g. <c>"_lyrics"</c>).</summary>
	public string Suffix {
		get => _suffix;
		set {
			if (_suffix == value) return;
			_suffix = value;
			OnPropertyChanged();
			_onChanged();
		}
	}

	/// <summary>The display index (0-based) to route matching files to.</summary>
	public int DisplayIndex {
		get => _displayIndex;
		set {
			if (_displayIndex == value) return;
			_displayIndex = value;
			OnPropertyChanged();
			_onChanged();
		}
	}

	/// <inheritdoc />
	public event PropertyChangedEventHandler? PropertyChanged;

	private void OnPropertyChanged([CallerMemberName] string? name = null) =>
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
