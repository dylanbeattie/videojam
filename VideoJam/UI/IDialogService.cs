namespace VideoJam.UI;

/// <summary>
/// Abstracts all user-facing dialog interactions so that <c>MainViewModel</c>
/// can be tested without a real WPF message pump.
/// </summary>
public interface IDialogService {
	/// <summary>
	/// Opens a folder-picker dialog.
	/// </summary>
	/// <param name="title">Dialog title shown to the user.</param>
	/// <returns>The selected folder path, or <see langword="null"/> if the user cancelled.</returns>
	string? PickFolder(string title);

	/// <summary>
	/// Opens a file-open dialog.
	/// </summary>
	/// <param name="title">Dialog title shown to the user.</param>
	/// <param name="filter">WPF/Win32 file filter string, e.g. <c>"Show files|*.show|All files|*.*"</c>.</param>
	/// <returns>The selected file path, or <see langword="null"/> if the user cancelled.</returns>
	string? PickOpenFile(string title, string filter);

	/// <summary>
	/// Opens a file-save dialog.
	/// </summary>
	/// <param name="title">Dialog title shown to the user.</param>
	/// <param name="filter">WPF/Win32 file filter string.</param>
	/// <param name="defaultExtension">Default file extension (without the leading dot, e.g. <c>"show"</c>).</param>
	/// <returns>The chosen save path, or <see langword="null"/> if the user cancelled.</returns>
	string? PickSaveFile(string title, string filter, string defaultExtension);

	/// <summary>
	/// Shows a Yes/No confirmation dialog.
	/// </summary>
	/// <param name="message">Message to display.</param>
	/// <param name="title">Dialog title.</param>
	/// <returns><see langword="true"/> if the user clicked Yes; <see langword="false"/> otherwise.</returns>
	bool Confirm(string message, string title);

	/// <summary>
	/// Shows an error message to the user.
	/// </summary>
	/// <param name="message">Error message to display.</param>
	/// <param name="title">Dialog title.</param>
	void ShowError(string message, string title);
}
