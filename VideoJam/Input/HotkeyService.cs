using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace VideoJam.Input;

/// <summary>
/// Registers a global low-level keyboard hook (<c>WH_KEYBOARD_LL</c>) and raises
/// <see cref="ButtonAPressed"/> / <see cref="ButtonBPressed"/> on the WPF UI thread
/// when the configured hotkeys are pressed.
/// </summary>
/// <remarks>
/// <para>
/// Must be constructed on the WPF UI thread; an <see cref="InvalidOperationException"/>
/// is thrown otherwise.
/// </para>
/// <para>
/// Call <see cref="Dispose"/> when the application exits to unhook the global hook.
/// </para>
/// </remarks>
internal sealed class HotkeyService : IDisposable {
	// ── P/Invoke ──────────────────────────────────────────────────────────────

	private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool UnhookWindowsHookEx(IntPtr hhk);

	[DllImport("user32.dll")]
	private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern IntPtr GetModuleHandle(string? lpModuleName);

	[StructLayout(LayoutKind.Sequential)]
	private struct KBDLLHOOKSTRUCT {
		public uint vkCode;
		public uint scanCode;
		public uint flags;
		public uint time;
		public IntPtr dwExtraInfo;
	}

	/// <summary>Low-level keyboard hook identifier.</summary>
	private const int WH_KEYBOARD_LL = 13;

	/// <summary>Windows message: key pressed.</summary>
	private const int WM_KEYDOWN = 0x0100;

	// ── State ─────────────────────────────────────────────────────────────────

	private readonly HotkeySettings _settings;
	private readonly Dispatcher _dispatcher;
	private readonly LowLevelKeyboardProc _callback;
	private readonly GCHandle _callbackHandle;
	private readonly IntPtr _hookHandle;
	private bool _disposed;

	// ── Construction ──────────────────────────────────────────────────────────

	/// <summary>
	/// Initialises a new <see cref="HotkeyService"/> and installs the global keyboard hook.
	/// </summary>
	/// <param name="appDirectory">
	/// The application directory used by <see cref="HotkeySettings.Load"/> to locate
	/// <c>appsettings.json</c>.
	/// </param>
	/// <exception cref="InvalidOperationException">
	/// Thrown if the constructor is not called on the WPF UI thread.
	/// </exception>
	public HotkeyService(string appDirectory) {
		if (Application.Current?.Dispatcher is not { } dispatcher ||
		    !dispatcher.CheckAccess())
			throw new InvalidOperationException(
				"HotkeyService must be constructed on the WPF UI thread.");

		_dispatcher = dispatcher;
		_settings = HotkeySettings.Load(appDirectory);

		// Pin the delegate so the GC never moves it — the unmanaged hook holds a raw function pointer.
		_callback = HookCallback;
		_callbackHandle = GCHandle.Alloc(_callback);

		using var module = System.Diagnostics.Process.GetCurrentProcess().MainModule!;
		_hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _callback, GetModuleHandle(module.ModuleName), 0);

		if (_hookHandle == IntPtr.Zero) {
			var errorCode = Marshal.GetLastWin32Error();
			throw new InvalidOperationException(
				$"Failed to install keyboard hook. Win32 error: {errorCode}");
		}
	}

	// ── Public API ────────────────────────────────────────────────────────────

	/// <summary>Raised on the WPF UI thread when <see cref="HotkeySettings.ButtonA"/> is pressed.</summary>
	public event EventHandler? ButtonAPressed;

	/// <summary>Raised on the WPF UI thread when <see cref="HotkeySettings.ButtonB"/> is pressed.</summary>
	public event EventHandler? ButtonBPressed;

	/// <inheritdoc />
	public void Dispose() {
		if (_disposed) return;
		_disposed = true;

		if (_hookHandle != IntPtr.Zero)
			UnhookWindowsHookEx(_hookHandle);

		if (_callbackHandle.IsAllocated)
			_callbackHandle.Free();
	}

	// ── Hook callback ─────────────────────────────────────────────────────────

	private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam) {
		if (nCode >= 0 && wParam == WM_KEYDOWN) {
			var kbStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
			var pressedKey = KeyInterop.KeyFromVirtualKey((int)kbStruct.vkCode);

			if (pressedKey == _settings.ButtonA)
				_dispatcher.BeginInvoke(() => ButtonAPressed?.Invoke(this, EventArgs.Empty));
			else if (pressedKey == _settings.ButtonB)
				_dispatcher.BeginInvoke(() => ButtonBPressed?.Invoke(this, EventArgs.Empty));
		}

		return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
	}
}
