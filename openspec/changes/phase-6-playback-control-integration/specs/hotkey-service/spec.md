## ADDED Requirements

### Requirement: HotkeyService installs a system-wide low-level keyboard hook
`HotkeyService` SHALL install a `WH_KEYBOARD_LL` hook via P/Invoke (`SetWindowsHookEx`) on construction. The hook SHALL be system-wide (`dwThreadId = 0`). The hook callback SHALL call `CallNextHookEx` unconditionally to pass all events to other hooks in the chain. `HotkeyService` SHALL implement `IDisposable`; `Dispose()` SHALL call `UnhookWindowsHookEx` to remove the hook.

The hook MUST be installed on the WPF UI thread so that it is serviced by the existing WPF message loop. Constructing `HotkeyService` on a background thread is invalid and SHALL throw `InvalidOperationException`.

#### Scenario: Hook is installed on construction
- **WHEN** `HotkeyService` is constructed on the UI thread
- **THEN** a non-zero hook handle is returned by `SetWindowsHookEx`, confirming the hook is active

#### Scenario: Hook is removed on dispose
- **WHEN** `HotkeyService.Dispose()` is called
- **THEN** `UnhookWindowsHookEx` is called with the hook handle and subsequent key presses no longer raise events

#### Scenario: Hook passes events to other hooks
- **WHEN** any key is pressed
- **THEN** `CallNextHookEx` is called from the hook callback regardless of whether the key matches Button A or Button B

---

### Requirement: HotkeyService raises ButtonAPressed and ButtonBPressed events
`HotkeyService` SHALL expose:
- `event EventHandler ButtonAPressed` — raised when the Button A key is pressed (key-down only, not key-up)
- `event EventHandler ButtonBPressed` — raised when the Button B key is pressed (key-down only)

Both events SHALL be raised on the WPF UI thread (via `Application.Current.Dispatcher.Invoke` or `BeginInvoke`).

#### Scenario: ButtonAPressed fires on Button A key-down
- **WHEN** the configured Button A key (default: Space) is pressed
- **THEN** `ButtonAPressed` is raised exactly once on the UI thread

#### Scenario: ButtonBPressed fires on Button B key-down
- **WHEN** the configured Button B key (default: Escape) is pressed
- **THEN** `ButtonBPressed` is raised exactly once on the UI thread

#### Scenario: Events do not fire on key-up
- **WHEN** the Button A or Button B key is released
- **THEN** neither `ButtonAPressed` nor `ButtonBPressed` is raised

#### Scenario: Other keys produce no events
- **WHEN** any key other than Button A or Button B is pressed
- **THEN** neither `ButtonAPressed` nor `ButtonBPressed` is raised

---

### Requirement: HotkeyService reads key bindings from appsettings.json
`HotkeyService` SHALL load key binding configuration from `appsettings.json` in the application directory at construction time. The configuration SHALL use the JSON structure:

```json
{
  "HotkeySettings": {
    "ButtonA": "Space",
    "ButtonB": "Escape"
  }
}
```

Values SHALL be parsed as `System.Windows.Input.Key` enum names (case-insensitive). If `appsettings.json` is absent, unreadable, or the keys are missing/invalid, the defaults SHALL be used silently (`ButtonA = Key.Space`, `ButtonB = Key.Escape`). `HotkeySettings` SHALL be a separate record/class in `VideoJam/Input/HotkeySettings.cs`.

#### Scenario: Custom key binding is applied
- **WHEN** `appsettings.json` contains `"ButtonA": "F1"`
- **THEN** pressing F1 raises `ButtonAPressed` and pressing Space does not

#### Scenario: Missing appsettings.json uses defaults
- **WHEN** no `appsettings.json` file exists in the app directory
- **THEN** `HotkeyService` constructs successfully with `ButtonA = Key.Space` and `ButtonB = Key.Escape`

#### Scenario: Invalid key name in config uses defaults
- **WHEN** `appsettings.json` contains `"ButtonA": "NotAKey"`
- **THEN** `HotkeyService` constructs successfully using the default `Key.Space` for Button A

---

### Requirement: HotkeyService hook callback is non-blocking
The `WH_KEYBOARD_LL` callback SHALL complete within 1 ms. It SHALL perform only:
1. Parse the `KBDLLHOOKSTRUCT` to extract the virtual key code.
2. Compare against the two configured virtual key codes.
3. If a match: call `Dispatcher.BeginInvoke` (non-blocking) to raise the event on the UI thread.
4. Call `CallNextHookEx` unconditionally.

No I/O, no locking, and no synchronous dispatcher calls (`Invoke`) SHALL occur inside the hook callback.

#### Scenario: Hook callback returns quickly
- **WHEN** a key matching Button A is pressed
- **THEN** the hook callback returns within 1 ms (Windows will log a warning and potentially remove the hook if the callback exceeds ~300 ms)
