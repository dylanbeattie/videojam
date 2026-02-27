using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Extensions.Logging;

namespace VideoJam;

/// <summary>
/// Interaction logic for App.xaml
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
	private const int AttachParentProcess = -1;

	// ── Startup ───────────────────────────────────────────────────────────────

	/// <inheritdoc />
	protected override void OnStartup(StartupEventArgs e) {
		base.OnStartup(e);

		// Re-attach stdout/stderr to the parent terminal so the Console logger
		// sink has somewhere to write. WinExe processes are spawned detached from
		// the parent console; AttachConsole(-1) reconnects them. If the app was
		// launched by double-click there is no parent console and this is a no-op.
		if (AttachConsole(AttachParentProcess)) {
			Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
			Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
		}

		string?  exePath  = Environment.ProcessPath;
		Version? version  = Assembly.GetEntryAssembly()?.GetName().Version;
		DateTime exeStamp = exePath is not null ? new FileInfo(exePath).LastWriteTime : DateTime.MinValue;

		using ILoggerFactory loggerFactory = LoggerFactory.Create(b =>
			b.AddConsole().SetMinimumLevel(LogLevel.Information));

		loggerFactory
			.CreateLogger<App>()
			.LogInformation(
				"VideoJam {Version} — built {Timestamp:yyyy-MM-dd HH:mm:ss}",
				version?.ToString(3) ?? "unknown",
				exeStamp);
	}
}
