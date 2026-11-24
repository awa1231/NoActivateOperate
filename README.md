# Non Activating Operate

[Project website](https://awa1231.github.io/NoActivateOperate/) · [Download for Windows](https://github.com/awa1231/NoActivateOperate/releases)

Click buttons, links, and video controls in background windows without bringing those windows to the front or taking keyboard focus away from the window you are currently typing in.

Clicking a text field in a background window automatically brings that window forward, so you can start typing normally, including with Chinese input methods.

- **Portable:** The application compiles into a single executable of approximately 55 KB. It does not write to system directories. Configuration is stored in `%APPDATA%\Non Activating Operate`.
- **No build artifacts in the repository:** Run `build.bat` to generate the executable. It uses the .NET Framework compiler included with Windows, so no SDK is required.
- **System tray utility:** The tray icon is green when the feature is enabled and blue when a window is temporarily being borrowed.
- **One shortcut:** Press **Ctrl+F** to toggle the feature. You can assign a different shortcut from the tray menu.

> ⚠️ `Ctrl+F` is the default Find shortcut in most applications. Once this global shortcut is registered, `Ctrl+F` is captured by Non Activating Operate while the application is running, so Find will no longer work in browsers, editors, and other applications.
>
> If this causes problems, right-click the tray icon and select **Change toggle shortcut…** to assign a less common key combination.

---

## Usage

1. Run `build.bat` to create `Non Activating Operate.exe`. Double-click the executable. A small two-window icon appears in the notification area.
2. Press **Ctrl+F** to enable the feature. The icon turns green and a confirmation sound may play.
3. Click the play button, link, or list item in a background window. The window is not brought to the front, and the current window keeps keyboard focus.
4. **To type:** Click a text field in the background window. That window is brought to the front temporarily, the cursor is placed where you clicked, and you can start typing normally.
5. Press **Ctrl+F** again to disable the feature and restore normal click behavior. If a window is still being temporarily borrowed, it is restored automatically.

You can disable **Bring background text fields forward** from the tray menu. Non-activating clicks will still work, but typing across windows will no longer be automatic.

## Tray menu

| Item | Description |
| --- | --- |
| Enable / Disable | You can also use `Ctrl+F` or double-click the tray icon. |
| Click behavior: Do not activate the window / Preserve current window stacking | Two click modes. See “Click behavior modes” below. |
| **Bring background text fields forward** (enabled by default) | Clicking a background text field brings its window forward so you can type immediately. |
| Use real drag and drop while dragging (enabled by default) | File dragging and drop operations require real mouse input. After the pointer moves 10 pixels, the tool switches to a real drag, which activates the target window once. |
| Scroll the background window under the pointer (disabled by default) | Route wheel input to the background window beneath the pointer. |
| Sounds | Play a confirmation sound when enabling, borrowing, or restoring a window. |
| Temporarily foreground the window under the pointer | Manual fallback for applications whose text fields cannot be detected automatically. |
| Release the hold after six seconds without typing (enabled by default) | After six seconds without keyboard or mouse input, restore the windows held in the current stacking group to normal behavior. |
| Restore the temporarily foregrounded window | Return the borrowed window to its original position in the window stack and restore the previous foreground window. |
| Pin the window under the pointer (keep on top) | Pin a reference window to the top, then interact normally with a lower window without covering it. Select the item again to unpin the window. |
| Change toggle shortcut… | Replace `Ctrl+F` with another shortcut. |
| Launch at Windows startup | Add an entry to `HKCU\...\CurrentVersion\Run`. Administrator privileges are not required. |

## Behavior details

- **The utility only acts on windows that are not currently active.** When you click the window you are already using, behavior remains completely normal: dragging, text selection, and text-field clicks all work as usual.
- Left-click, right-click, middle-click, and double-click are supported.
- **Clicking a text field automatically borrows the foreground.** When you click a text field in a background window, including `Edit`, `RichEdit`, and text controls in browsers, Electron, and WPF applications, the utility temporarily brings the window to the foreground and sends a **real click** at the selected position so the caret is placed correctly. This changes the window order because Windows sends keyboard input only to the foreground window; typing with an input method is not possible without foreground activation.
- **Drag and drop:** Synthetic clicks cannot start system OLE drag and drop. After the pointer moves 10 pixels while the mouse is held down, the utility switches to a real mouse drag so files and other drag-and-drop targets work normally. This also activates the target window once. You can disable this behavior from the menu.
- **“Look at the upper window while typing in the lower one”:** In **Preserve current window stacking** mode, clicking a lower window activates it normally and lets you type directly, while the windows above it remain above it and the visible layout stays unchanged.
- Clicking the **taskbar, notification area, Start menu, or desktop** behaves normally and is unaffected.
- Clicking the **title bar, border, or scrollbar** of a background window is ignored. Windows must activate a window for these interactions, so the only way to preserve the current stacking order is to ignore them.
- The tray icon always remains clickable, even while the feature is enabled, so you can disable it at any time.

## Click behavior modes

The first group in the tray menu controls what happens after a click. `Ctrl+F` only enables or disables the feature.

### A. Do not activate the window (default)

Clicks in background windows are delivered without activation. This is useful for play buttons, links, and list items. Clicking a text field automatically brings its window forward so you can type.

### B. Preserve current window stacking

Clicking a lower window **activates it normally**, which allows the caret to enter the field and typing to work directly. Before activation, the utility identifies the windows covering the target window and pins them in their current order at the top. The selected window can then become active while remaining beneath the pinned group, so the visible stacking relationship does not appear to change.

- Typing works normally with real keyboard input and a real input method, including Chinese.
- **All** windows above the target window, not only those that overlap it, are kept in their original order at the top. With three windows, for example, the middle window is not pushed behind the others.
- The preserved stacking group remains active only for the **current typing session**. It is restored automatically in either of these situations:
  1. You switch to another window by clicking it, pressing `Alt+Tab`, or selecting a window above the target.
  2. You stop using the keyboard and mouse for six seconds.
- New windows created after restoration also appear normally above the target window.
- You can release the group manually by pressing `Ctrl+F` to disable the feature or by selecting **Release the preserved stacking order** from the menu. To disable the six-second timeout, turn off **Release the hold after six seconds without typing** in the menu.

## How it works

Windows normally combines a mouse click on a window with window activation and foreground promotion. When the real mouse button is pressed, Windows activates the target window before dispatching the click, which brings the window to the front.

This utility installs a global low-level mouse hook (`WH_MOUSE_LL`) and intercepts the click before Windows dispatches it. It then converts the pointer position to the target window's client coordinates and uses `PostMessage` to send `WM_MOUSEMOVE`, `WM_LBUTTONDOWN`, and `WM_LBUTTONUP` directly to the control beneath the pointer. It also handles double-clicks, right-clicks, middle-clicks, and wheel events.

The target window receives one click, but it is never activated. **The Z-order, foreground window, and keyboard focus therefore remain unchanged.** This is different from bringing a window forward and then moving it back: the window is never moved in the first place.

Text fields are the exception. After detecting a text field, the utility records the current window stack and foreground window, brings the target window to the foreground, and sends a real click to position the caret. You can then type with a real keyboard and input method. The tray menu can restore the original stacking order and foreground window after borrowing it.

Text-field detection works in two stages. It first checks the control class, such as `Edit`, `RichEdit`, `Scintilla`, and various `TextBox` controls. If the class is not recognized, it uses UI Automation to check whether the control type at the clicked position is `Edit`, covering browsers, Electron, WPF, and similar applications.

> Another approach that initially appears more realistic was also tested: temporarily adding `WS_EX_NOACTIVATE` to the target window so that a real mouse click would not activate it.
>
> Testing showed that this approach **does not work on Windows**. The style takes effect only when a window is created. Applying it at runtime has no effect on click activation, and clicks still activated and raised both WinForms windows and windows using the native `DefWindowProc`. The final implementation therefore uses posted mouse messages.

## Limitations

- Applications that process window messages generally work, including Chrome, Edge, Word, Excel, Notepad, File Explorer, VLC, PotPlayer, Electron applications, and WinForms or WPF applications.
- **Games** and **some UWP or Microsoft Store applications** read raw input through RawInput or CoreWindow. They may not receive these clicks and can appear unresponsive.
- A small number of applications, especially those using custom-rendered UI frameworks, may bring themselves to the foreground after receiving a click. The utility cannot prevent this because the application genuinely received the click.
- Some applications expose neither a standard text-field control class nor a usable UI Automation type. Use **Temporarily foreground the window under the pointer** from the tray menu as a manual fallback.
- To interact with a window running with administrator privileges, Non Activating Operate must also run as administrator. Otherwise Windows blocks message delivery and the utility falls back to a normal click.

## Files

| Path | Description |
| --- | --- |
| `build.bat` | Compiles `Non Activating Operate.exe` using the .NET Framework 4.x compiler included with Windows. No SDK is required. |
| `src\` | Source code (C# 5 / WinForms, no third-party dependencies). |
| `tests\` | Automated acceptance tests described below. |

Configuration and logs:

```text
%APPDATA%\Non Activating Operate\config.ini
%APPDATA%\Non Activating Operate\log.txt
```

## Requirements

- Windows 10 or Windows 11 (x64 or x86)
- .NET Framework 4.x, included with Windows; no separate installation is required

> The executable produced by this repository is not code-signed. Windows SmartScreen may report an **Unknown publisher** warning on first launch. Select **More info → Run anyway** to continue.

## Rebuild

Double-click `build.bat`, or run:

```bat
build.bat -nopause
```

## Automated acceptance tests

`tests\Harness.cs` creates several overlapping and stacked windows and verifies that:

- In the control group, ordinary clicks raise background windows and steal foreground focus when the hook is disabled, proving that the test method is valid.
- Direct engine calls deliver clicks at the correct coordinates without changing Z-order or the foreground window.
- The end-to-end hook swallows real clicks, delivers posted messages to the target, and preserves Z-order and the foreground window.
- Synthetic dragging without activation switches correctly to real drag and drop, and true OLE drag and drop works when dragging a file from a background window to another window.
- Foreground borrowing brings the target window to the front, restores the original foreground window and stacking order afterward, and allows text to be typed into the borrowed window.
- Double-click, button clicks, wheel input, title-bar ignoring, active-window exemptions, and DPI conversion work correctly.

Run the tests with:

```bat
tests\run-tests.bat
```

The test suite opens several windows in the lower-right area of the screen and injects real mouse clicks for about six seconds. The test windows close automatically when the run finishes.
