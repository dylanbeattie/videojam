using System.Text.Json;
using System.Windows.Input;

namespace VideoJam.Input;

/// <summary>
/// Hotkey assignments for the global keyboard shortcuts.
/// Loaded from <c>appsettings.json</c> at startup; falls back to defaults on any error.
/// </summary>
internal sealed class HotkeySettings {
	/// <summary>Key mapped to the GO action (default: Space).</summary>
	public Key ButtonA { get; set; } = Key.Space;

	/// <summary>Key mapped to the STOP/REWIND action (default: Escape).</summary>
	public Key ButtonB { get; set; } = Key.Escape;

	// ── Factory ───────────────────────────────────────────────────────────────

	/// <summary>
	/// Loads hotkey settings from <c>appsettings.json</c> in <paramref name="appDirectory"/>.
	/// Falls back to defaults silently if the file is missing, malformed, or contains
	/// unrecognised key names.
	/// </summary>
	/// <param name="appDirectory">The directory that contains <c>appsettings.json</c>.</param>
	/// <returns>A populated <see cref="HotkeySettings"/>; never <see langword="null"/>.</returns>
	public static HotkeySettings Load(string appDirectory) {
		var result = new HotkeySettings();
		try {
			var path = Path.Combine(appDirectory, "appsettings.json");
			if (!File.Exists(path)) return result;

			using var stream = File.OpenRead(path);
			using var doc = JsonDocument.Parse(stream);

			if (!doc.RootElement.TryGetProperty("HotkeySettings", out var section))
				return result;

			if (section.TryGetProperty("ButtonA", out var buttonA) &&
			    buttonA.ValueKind == JsonValueKind.String &&
			    Enum.TryParse<Key>(buttonA.GetString(), ignoreCase: true, out var keyA))
				result.ButtonA = keyA;

			if (section.TryGetProperty("ButtonB", out var buttonB) &&
			    buttonB.ValueKind == JsonValueKind.String &&
			    Enum.TryParse<Key>(buttonB.GetString(), ignoreCase: true, out var keyB))
				result.ButtonB = keyB;
		}
		catch {
			// Any I/O or JSON failure — silently return defaults.
		}
		return result;
	}
}
