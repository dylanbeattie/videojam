using System.ComponentModel;
using System.Runtime.CompilerServices;
using VideoJam.Model;

namespace VideoJam.UI.ViewModels;

/// <summary>
/// Lightweight ViewModel wrapper around a <see cref="SongEntry"/> for display in the setlist.
/// Exposes a bindable <see cref="DisplayIndex"/> (1-based) that <see cref="MainViewModel"/>
/// keeps current after every collection mutation, so WPF always shows the correct position
/// without relying on <c>AlternationIndex</c> or multi-value converter re-evaluation.
/// </summary>
public sealed class SongRowViewModel : INotifyPropertyChanged {
	private int _displayIndex;

	/// <summary>The underlying <see cref="SongEntry"/> model object.</summary>
	public SongEntry Song { get; }

	/// <summary>Song display name (delegates to <see cref="Song"/>).</summary>
	public string Name => Song.Name;

	/// <summary>
	/// 1-based position of this song in the setlist.
	/// Raises <see cref="PropertyChanged"/> when updated so the bound <c>TextBlock</c>
	/// refreshes without needing a collection rebuild.
	/// </summary>
	public int DisplayIndex {
		get => _displayIndex;
		set {
			if (_displayIndex == value) return;
			_displayIndex = value;
			OnPropertyChanged();
		}
	}

	/// <summary>
	/// Initialises a new <see cref="SongRowViewModel"/>.
	/// </summary>
	/// <param name="song">The song model to wrap.</param>
	/// <param name="displayIndex">Initial 1-based display index.</param>
	public SongRowViewModel(SongEntry song, int displayIndex) {
		Song = song;
		_displayIndex = displayIndex;
	}

	/// <inheritdoc />
	public event PropertyChangedEventHandler? PropertyChanged;

	private void OnPropertyChanged([CallerMemberName] string? name = null) =>
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
