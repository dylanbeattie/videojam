using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Extensions.Logging;
using VideoJam.Engine;
using VideoJam.Input;
using VideoJam.Model;
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

	// ── Disposable resources held for shutdown ────────────────────────────────

	private ILoggerFactory? _loggerFactory;
	private VideoEngine? _videoEngine;
	private HotkeyService? _hotkeyService;
	private PlaybackEngine? _playbackEngine;
	private MainViewModel? _mainViewModel;

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

		_loggerFactory = LoggerFactory.Create(b =>
			b.AddConsole().SetMinimumLevel(LogLevel.Information));

		_loggerFactory
			.CreateLogger<App>()
			.LogInformation(
				"VideoJam {Version} — built {Timestamp:yyyy-MM-dd HH:mm:ss}",
				version?.ToString(3) ?? "unknown",
				exeStamp);

		// ── Dependency wiring ─────────────────────────────────────────────────
		// Construct services → engines → ViewModel → View in dependency order.
		// No IoC container; the graph is small enough for explicit wiring.
		var dialogService = new WpfDialogService();

		_videoEngine = new VideoEngine(_loggerFactory.CreateLogger<VideoEngine>());
		var syncCoordinator = new SyncCoordinator(_loggerFactory.CreateLogger<SyncCoordinator>());

		// PlaybackEngine is constructed before the window so the ViewModel can reference it.
		// The MainWindow reference is back-filled via SetMainWindow() after the window is shown.
		// The Show placeholder is replaced by UpdateShow() when the operator loads a show.
		_playbackEngine = new PlaybackEngine(
			show: new Show(),
			videoEngine: _videoEngine,
			syncCoordinator: syncCoordinator,
			mainWindow: null,
			loggerFactory: _loggerFactory);

		_mainViewModel = new MainViewModel(dialogService, _playbackEngine);

		var mainWindow = new MainWindow {
			DataContext = _mainViewModel,
		};

		// Back-fill the window reference into the engine now that the window exists.
		_playbackEngine.SetMainWindow(mainWindow);

		MainWindow = mainWindow;
		mainWindow.Show();

		// HotkeyService must be constructed on the UI thread AFTER the window is shown,
		// because the low-level keyboard hook requires an active message loop.
		var appDirectory = AppContext.BaseDirectory;
		_hotkeyService = new HotkeyService(appDirectory);

		// Inject the hotkey service into the ViewModel.
		_mainViewModel.SetHotkeyService(_hotkeyService);

		Exit += OnApplicationExit;
	}

	// ── Shutdown ──────────────────────────────────────────────────────────────

	private void OnApplicationExit(object sender, ExitEventArgs e) {
		_mainViewModel?.Dispose();
		_hotkeyService?.Dispose();
		_playbackEngine?.Dispose();
		_videoEngine?.Dispose();
		_loggerFactory?.Dispose();
	}
}
