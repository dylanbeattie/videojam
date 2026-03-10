using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using VideoJam.UI.ViewModels;

namespace VideoJam.UI;

/// <summary>
/// Code-behind for <see cref="MainWindow"/>.
/// Contains only WPF lifecycle glue — all business logic lives in <see cref="MainViewModel"/>.
/// </summary>
public partial class MainWindow : Window {
	// ── Construction ──────────────────────────────────────────────────────────

	/// <inheritdoc />
	public MainWindow() {
		InitializeComponent();

		// Ctrl+S keyboard shortcut for Save — wired here so it works regardless of focus.
		var saveGesture = new KeyBinding(
			new RelayCommandAdapter(() => ViewModel?.SaveShowCommand),
			Key.S,
			ModifierKeys.Control);
		InputBindings.Add(saveGesture);
	}

	// ── ViewModel accessor ────────────────────────────────────────────────────

	private MainViewModel? ViewModel => DataContext as MainViewModel;

	// ── WPF lifecycle glue ────────────────────────────────────────────────────

	/// <summary>
	/// Applies the unsaved-changes guard before the window closes.
	/// Cancels the close if the ViewModel requests it.
	/// </summary>
	private void OnWindowClosing(object sender, CancelEventArgs e) {
		if (ViewModel?.ConfirmClose() == true)
			e.Cancel = true;
	}

	/// <summary>
	/// Prevents song selection clicks when the setlist is not interactive (i.e. during playback).
	/// The <c>SelectionChanged</c> event fires before <c>IsEnabled</c> binding updates, so we
	/// guard selection changes here and revert them when the ViewModel says not to allow selection.
	/// </summary>
	private void OnSetlistSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) {
		if (ViewModel is { IsSetlistInteractive: false }) {
			// Revert — do not allow selection changes during playback.
			SetlistBox.SelectedItem = ViewModel.SelectedSong;
		}
	}
}

/// <summary>
/// Thin <see cref="ICommand"/> adapter used for keyboard bindings that lazily resolve the
/// actual command from the ViewModel. This avoids capturing a stale command reference at
/// construction time when the DataContext is not yet set.
/// </summary>
file sealed class RelayCommandAdapter : ICommand {
	private readonly Func<ICommand?> _resolver;

	internal RelayCommandAdapter(Func<ICommand?> resolver) => _resolver = resolver;

	public event EventHandler? CanExecuteChanged {
		add => CommandManager.RequerySuggested += value;
		remove => CommandManager.RequerySuggested -= value;
	}

	public bool CanExecute(object? parameter) => _resolver()?.CanExecute(parameter) ?? false;
	public void Execute(object? parameter) => _resolver()?.Execute(parameter);
}
