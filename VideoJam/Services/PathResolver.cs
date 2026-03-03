namespace VideoJam.Services;

/// <summary>
/// Converts between absolute paths and paths relative to a <c>.show</c> file's directory.
/// All methods are pure functions — no I/O, no state.
/// </summary>
/// <remarks>
/// Paths stored in <c>.show</c> JSON always use forward slashes for portability.
/// On Windows, <see cref="Resolve"/> accepts both forward and back slashes.
/// </remarks>
internal static class PathResolver {
	/// <summary>
	/// Computes the path of <paramref name="absoluteTargetPath"/> relative to
	/// <paramref name="showFileDirectory"/>, using forward slashes as separators.
	/// </summary>
	/// <param name="absoluteTargetPath">The absolute path to make relative.</param>
	/// <param name="showFileDirectory">
	/// The directory containing the <c>.show</c> file — used as the base for the relative path.
	/// </param>
	/// <returns>
	/// A relative path from <paramref name="showFileDirectory"/> to
	/// <paramref name="absoluteTargetPath"/>, with forward slashes.
	/// </returns>
	public static string MakeRelative(string absoluteTargetPath, string showFileDirectory) {
		string relative = Path.GetRelativePath(showFileDirectory, absoluteTargetPath);
		// Normalise to forward slashes so .show files are portable between tools.
		return relative.Replace('\\', '/');
	}

	/// <summary>
	/// Resolves a show-file-relative <paramref name="relativePath"/> against
	/// <paramref name="showFileDirectory"/>, returning a normalised absolute path.
	/// Parent-directory (<c>..</c>) segments are collapsed.
	/// </summary>
	/// <param name="relativePath">
	/// A path relative to the <c>.show</c> file's directory.
	/// Forward and back slashes are both accepted.
	/// </param>
	/// <param name="showFileDirectory">
	/// The directory containing the <c>.show</c> file.
	/// </param>
	/// <returns>A normalised absolute path.</returns>
	public static string Resolve(string relativePath, string showFileDirectory) {
		string combined = Path.Combine(showFileDirectory, relativePath);
		return Path.GetFullPath(combined);
	}
}
