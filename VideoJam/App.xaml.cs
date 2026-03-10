using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Extensions.Logging;
using VideoJam.UI;
using VideoJam.UI.ViewModels;

namespace VideoJam;

/// <summary>
/// Application entry point and startup wiring.
/// Constructs the dependency graph and shows <see cref="MainWindow"/>.
/// </summary>
public partial class App : Application {
	// ── Console attachment (WinExe ↔ terminal) ────────────────────────────────

	/// <summary>
	/// Attaches the calling process to the console of its parent process.
	/// Returns <see langword="false"/> if no parent console exists (e.g. launched by double-click).
	/// </summary>
	[DllImport("kernel32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool AttachConsole(int dwProcessId);

	/// <summary>Sentinel value: attach to the console of the immediate parent process.</summary>
	private const int ATTACH_PARENT_PROCESS = -1;

	// ── Startup ───────────────────────────────────────────────────────────────

	/// <inheritdoc />
	protected override void OnStartup(StartupEventArgs e) {
		base.OnStartup(e);

		// Re-attach stdout/stderr to the parent terminal so the Console logger
		// sink has somewhere to write.
		if (AttachConsole(ATTACH_PARENT_PROCESS)) {
			Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
			Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
		}

		var exePath = Environment.ProcessPath;
		var version = Assembly.GetEntryAssembly()?.GetName().Version;
		var exeStamp = exePath is not null ? new FileInfo(exePath).LastWriteTime : DateTime.MinValue;

		using var loggerFactory = LoggerFactory.Create(b =>
			b.AddConsole().SetMinimumLevel(LogLevel.Information));

		loggerFactory
			.CreateLogger<App>()
			.LogInformation(
				"VideoJam {Version} — built {Timestamp:yyyy-MM-dd HH:mm:ss}",
				version?.ToString(3) ?? "unknown",
				exeStamp);

		// ── Dependency wiring ─────────────────────────────────────────────────
		// Construct services → ViewModel → View in dependency order.
		// No IoC container; the graph is small enough for explicit wiring.
		var dialogService = new WpfDialogService();
		var viewModel = new MainViewModel(dialogService);

		var mainWindow = new MainWindow {
			DataContext = viewModel,
		};

		MainWindow = mainWindow;
		mainWindow.Show();
	}
}
