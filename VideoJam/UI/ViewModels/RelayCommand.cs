using System.Windows.Input;

namespace VideoJam.UI.ViewModels;

/// <summary>
/// A command that delegates execution to caller-supplied delegates.
/// Raises <see cref="CanExecuteChanged"/> via <see cref="CommandManager.RequerySuggested"/>
/// so WPF automatically re-evaluates the command whenever UI state changes.
/// </summary>
public sealed class RelayCommand : ICommand {
	private readonly Action _execute;
	private readonly Func<bool>? _canExecute;

	/// <summary>
	/// Initialises a new <see cref="RelayCommand"/>.
	/// </summary>
	/// <param name="execute">The action to invoke on <see cref="Execute"/>.</param>
	/// <param name="canExecute">Optional predicate; when <see langword="null"/> the command is always enabled.</param>
	public RelayCommand(Action execute, Func<bool>? canExecute = null) {
		_execute = execute ?? throw new ArgumentNullException(nameof(execute));
		_canExecute = canExecute;
	}

	/// <inheritdoc />
	public event EventHandler? CanExecuteChanged {
		add => CommandManager.RequerySuggested += value;
		remove => CommandManager.RequerySuggested -= value;
	}

	/// <inheritdoc />
	public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

	/// <inheritdoc />
	public void Execute(object? parameter) => _execute();
}

/// <summary>
/// A strongly-typed command that delegates execution to caller-supplied delegates.
/// Raises <see cref="CanExecuteChanged"/> via <see cref="CommandManager.RequerySuggested"/>.
/// </summary>
/// <typeparam name="T">The type of the command parameter.</typeparam>
public sealed class RelayCommand<T> : ICommand {
	private readonly Action<T?> _execute;
	private readonly Func<T?, bool>? _canExecute;

	/// <summary>
	/// Initialises a new <see cref="RelayCommand{T}"/>.
	/// </summary>
	/// <param name="execute">The action to invoke on <see cref="Execute"/>.</param>
	/// <param name="canExecute">Optional predicate; when <see langword="null"/> the command is always enabled.</param>
	public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null) {
		_execute = execute ?? throw new ArgumentNullException(nameof(execute));
		_canExecute = canExecute;
	}

	/// <inheritdoc />
	public event EventHandler? CanExecuteChanged {
		add => CommandManager.RequerySuggested += value;
		remove => CommandManager.RequerySuggested -= value;
	}

	/// <inheritdoc />
	public bool CanExecute(object? parameter) {
		if (_canExecute is null) return true;
		return _canExecute(parameter is T t ? t : default);
	}

	/// <inheritdoc />
	public void Execute(object? parameter) => _execute(parameter is T t ? t : default);
}
