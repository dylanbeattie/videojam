using VideoJam.UI.ViewModels;

namespace VideoJam.Tests;

/// <summary>Unit tests for <see cref="RelayCommand"/> and <see cref="RelayCommand{T}"/>.</summary>
public sealed class RelayCommandTests {
	// ── RelayCommand ──────────────────────────────────────────────────────────

	[Fact]
	public void RelayCommand_Execute_InvokesDelegate() {
		var invoked = false;
		var cmd = new RelayCommand(() => invoked = true);

		cmd.Execute(null);

		Assert.True(invoked);
	}

	[Fact]
	public void RelayCommand_CanExecute_ReturnsTrueWhenNoPredicateProvided() {
		var cmd = new RelayCommand(() => { });

		Assert.True(cmd.CanExecute(null));
	}

	[Fact]
	public void RelayCommand_CanExecute_ReturnsFalseWhenPredicateReturnsFalse() {
		var cmd = new RelayCommand(() => { }, canExecute: () => false);

		Assert.False(cmd.CanExecute(null));
	}

	[Fact]
	public void RelayCommand_CanExecute_ReturnsTrueWhenPredicateReturnsTrue() {
		var cmd = new RelayCommand(() => { }, canExecute: () => true);

		Assert.True(cmd.CanExecute(null));
	}

	[Fact]
	public void RelayCommand_Constructor_ThrowsWhenExecuteIsNull() {
		Assert.Throws<ArgumentNullException>(() => new RelayCommand(null!));
	}

	// ── RelayCommand<T> ───────────────────────────────────────────────────────

	[Fact]
	public void RelayCommandT_Execute_PassesParameterToDelegate() {
		string? received = null;
		var cmd = new RelayCommand<string>(p => received = p);

		cmd.Execute("hello");

		Assert.Equal("hello", received);
	}

	[Fact]
	public void RelayCommandT_Execute_PassesNullWhenParameterIsWrongType() {
		string? received = "initial";
		var cmd = new RelayCommand<string>(p => received = p);

		cmd.Execute(42); // wrong type — should coerce to null

		Assert.Null(received);
	}

	[Fact]
	public void RelayCommandT_CanExecute_ReturnsFalseWhenPredicateReturnsFalse() {
		var cmd = new RelayCommand<string>(_ => { }, canExecute: _ => false);

		Assert.False(cmd.CanExecute("x"));
	}

	[Fact]
	public void RelayCommandT_CanExecute_ReturnsTrueWhenNoPredicateProvided() {
		var cmd = new RelayCommand<string>(_ => { });

		Assert.True(cmd.CanExecute("x"));
	}
}
