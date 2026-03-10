using System.Windows;
using Microsoft.Win32;

namespace VideoJam.UI;

/// <summary>
/// Production implementation of <see cref="IDialogService"/> using native WPF dialogs.
/// </summary>
internal sealed class WpfDialogService : IDialogService {
	/// <inheritdoc />
	public string? PickFolder(string title) {
		var dialog = new OpenFolderDialog {
			Title = title,
			Multiselect = false,
		};
		return dialog.ShowDialog() == true ? dialog.FolderName : null;
	}

	/// <inheritdoc />
	public string? PickOpenFile(string title, string filter) {
		var dialog = new OpenFileDialog {
			Title = title,
			Filter = filter,
			CheckFileExists = true,
		};
		return dialog.ShowDialog() == true ? dialog.FileName : null;
	}

	/// <inheritdoc />
	public string? PickSaveFile(string title, string filter, string defaultExtension) {
		var dialog = new SaveFileDialog {
			Title = title,
			Filter = filter,
			DefaultExt = defaultExtension,
			AddExtension = true,
		};
		return dialog.ShowDialog() == true ? dialog.FileName : null;
	}

	/// <inheritdoc />
	public bool Confirm(string message, string title) {
		var result = MessageBox.Show(
			message,
			title,
			MessageBoxButton.YesNo,
			MessageBoxImage.Question);
		return result == MessageBoxResult.Yes;
	}

	/// <inheritdoc />
	public void ShowError(string message, string title) {
		MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
	}
}
