using System.ComponentModel;
using System.Runtime.CompilerServices;
using VideoJam.Model;

namespace VideoJam.UI.ViewModels;

/// <summary>
/// Wraps a <see cref="ChannelSettings"/> model object for two-way WPF binding in the mixer panel.
/// Property changes automatically propagate back to the underlying model and invoke the
/// <see cref="OnChanged"/> callback so the parent ViewModel can track dirty state.
/// </summary>
public sealed class ChannelSettingsViewModel : INotifyPropertyChanged {
	private readonly ChannelSettings _model;
	private readonly Action _onChanged;

	/// <summary>
	/// Initialises a new <see cref="ChannelSettingsViewModel"/>.
	/// </summary>
	/// <param name="channelId">The channel identifier (e.g. <c>"drums.wav"</c> or <c>"video.mp4:audio"</c>).</param>
	/// <param name="model">The underlying channel settings to wrap.</param>
	/// <param name="onChanged">Callback invoked whenever a property is mutated.</param>
	public ChannelSettingsViewModel(string channelId, ChannelSettings model, Action onChanged) {
		ChannelId = channelId;
		_model = model;
		_onChanged = onChanged;
	}

	/// <summary>The channel identifier.</summary>
	public string ChannelId { get; }

	/// <summary>
	/// Human-readable channel name derived from the channel ID.
	/// For video audio channels (ending in <c>:audio</c>), the suffix is stripped.
	/// </summary>
	public string DisplayName => IsVideoAudio
		? ChannelId[..ChannelId.LastIndexOf(':')]
		: ChannelId;

	/// <summary><see langword="true"/> when this channel carries video audio (channel ID ends with <c>:audio</c>).</summary>
	public bool IsVideoAudio => ChannelId.EndsWith(":audio", StringComparison.OrdinalIgnoreCase);

	/// <summary>Output level in the range [0.0, 1.0]. Two-way bound to the mixer slider.</summary>
	public float Level {
		get => _model.Level;
		set {
			if (Math.Abs(_model.Level - value) < 0.0001f) return;
			_model.Level = value;
			OnPropertyChanged();
			_onChanged();
		}
	}

	/// <summary>Whether the channel is muted. Two-way bound to the mixer checkbox.</summary>
	public bool Muted {
		get => _model.Muted;
		set {
			if (_model.Muted == value) return;
			_model.Muted = value;
			OnPropertyChanged();
			_onChanged();
		}
	}

	/// <inheritdoc />
	public event PropertyChangedEventHandler? PropertyChanged;

	private void OnPropertyChanged([CallerMemberName] string? name = null) =>
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
