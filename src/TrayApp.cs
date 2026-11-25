using System;
using System.Drawing;
using System.Media;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace NonActivatingOperate
{
    internal sealed class TrayContext : ApplicationContext
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "Non Activating Operate";

        private readonly AppConfig _config;
        private readonly ClickTargetPolicy _policy;
        private readonly ClickEngine _engine;
        private readonly MouseHook _hook;
        private readonly HotkeyHost _hotkeyHost;
        private readonly ForegroundBorrower _borrower = new ForegroundBorrower();
        private readonly KeepFrontPinner _pinner = new KeepFrontPinner();
        private readonly NotifyIcon _tray;
        private readonly ContextMenuStrip _menu;
        private readonly ToolStripMenuItem _enableItem;
        private readonly ToolStripMenuItem _clickModeItem;
        private readonly ToolStripMenuItem _keepFrontItem;
        private readonly ToolStripMenuItem _unpinItem;
        private readonly ToolStripMenuItem _idleItem;
        private readonly ToolStripMenuItem _typingItem;
        private readonly ToolStripMenuItem _dragItem;
        private readonly ToolStripMenuItem _wheelItem;
        private readonly ToolStripMenuItem _soundItem;
        private readonly ToolStripMenuItem _startupItem;
        private readonly ToolStripMenuItem _manualItem;
        private readonly ToolStripMenuItem _restoreItem;
        private readonly ToolStripMenuItem _pinItem;

        private Icon _iconOn;
        private Icon _iconBorrow;
        private Icon _iconOff;
        private bool _disposed;
        private int _pendingDetect;
        private bool _typingHintShown;
        private bool _pinnedHintShown;
        private const int IdleReleaseMs = 6000;
        private IntPtr _pinnedFor = IntPtr.Zero;
        private readonly System.Windows.Forms.Timer _pinTimer = new System.Windows.Forms.Timer();

        public TrayContext()
        {
            bool firstRun;
            _config = AppConfig.Load(out firstRun);

            _policy = new ClickTargetPolicy();
            _engine = new ClickEngine(_policy);
            _engine.DpiScaling = _config.DpiScaling;

            _hook = new MouseHook(_engine);
            _hook.WheelRedirect = _config.WheelRedirect;
            _hook.RealDragHandover = _config.RealDragHandover;
            _hook.ClickPosted = OnClickPosted;
            _hook.BeforeActivate = OnBeforeActivate;

            _pinTimer.Interval = 300;
            _pinTimer.Tick += delegate { CheckPinnedSession(); };

            _hotkeyHost = new HotkeyHost();
            _hotkeyHost.TogglePressed += delegate { SetEnabled(!_config.Enabled, true); };

            _iconOn = AppIcons.Create(true);
            _iconBorrow = AppIcons.CreateBorrowing();
            _iconOff = AppIcons.Create(false);

            _menu = new ContextMenuStrip();
            _menu.ShowImageMargin = false;
            _menu.ShowItemToolTips = true;

            _enableItem = new ToolStripMenuItem("Enable: click background windows without activating them");
            _enableItem.Click += delegate { SetEnabled(!_config.Enabled, true); };
            _menu.Items.Add(_enableItem);
            _menu.Items.Add(new ToolStripSeparator());

            _clickModeItem = new ToolStripMenuItem("Click behavior: do not activate the window");
            _clickModeItem.ToolTipText = "Clicks in background windows are delivered without activation. Clicking a text field brings its window forward so you can type.";
            _clickModeItem.Click += delegate { SetBehavior(false); };
            _menu.Items.Add(_clickModeItem);

            _keepFrontItem = new ToolStripMenuItem("Click behavior: preserve current window stacking");
            _keepFrontItem.ToolTipText = "The clicked window activates normally so you can type, while the windows above it stay in front and the visible layout does not change.";
            _keepFrontItem.Click += delegate { SetBehavior(true); };
            _menu.Items.Add(_keepFrontItem);

            _menu.Items.Add(new ToolStripSeparator());

            _typingItem = new ToolStripMenuItem("Bring background text fields forward");
            _typingItem.ToolTipText = "Clicking a text field in a background window brings that window forward so you can type. Buttons and links still do not activate the window.";
            _typingItem.Click += delegate
            {
                _config.BorrowOnTextClick = !_config.BorrowOnTextClick;
                _config.Save();
                UpdateUi();
            };
            _menu.Items.Add(_typingItem);

            _dragItem = new ToolStripMenuItem("Use real drag and drop while dragging (activates the window)");
            _dragItem.ToolTipText = "Synthetic clicks cannot start file or OLE drag and drop. Once the pointer moves while dragging, the tool switches to real mouse input.";
            _dragItem.Click += delegate
            {
                _config.RealDragHandover = !_config.RealDragHandover;
                _hook.RealDragHandover = _config.RealDragHandover;
                _config.Save();
                UpdateUi();
            };
            _menu.Items.Add(_dragItem);

            _wheelItem = new ToolStripMenuItem("Scroll the background window under the pointer");
            _wheelItem.Click += delegate
            {
                _config.WheelRedirect = !_config.WheelRedirect;
                _hook.WheelRedirect = _config.WheelRedirect;
                _config.Save();
                UpdateUi();
            };
            _menu.Items.Add(_wheelItem);

            _soundItem = new ToolStripMenuItem("Sounds");
            _soundItem.Click += delegate
            {
                _config.Sound = !_config.Sound;
                _config.Save();
                UpdateUi();
            };
            _menu.Items.Add(_soundItem);

            _menu.Items.Add(new ToolStripSeparator());

            _manualItem = new ToolStripMenuItem("Temporarily foreground the window under the pointer");
            _manualItem.Click += delegate { ManualBorrow(); };
            _menu.Items.Add(_manualItem);

            _restoreItem = new ToolStripMenuItem("Restore the temporarily foregrounded window");
            _restoreItem.Click += delegate { RestoreBorrow(true); };
            _menu.Items.Add(_restoreItem);

            _idleItem = new ToolStripMenuItem("Release the hold after " + (IdleReleaseMs / 1000) + " seconds without typing");
            _idleItem.ToolTipText = "After you stop typing, windows held in front are restored automatically.";
            _idleItem.Click += delegate
            {
                _config.AutoReleaseIdle = !_config.AutoReleaseIdle;
                _config.Save();
                UpdateUi();
            };
            _menu.Items.Add(_idleItem);

            _unpinItem = new ToolStripMenuItem("Release the preserved stacking order");
            _unpinItem.Click += delegate
            {
                int count = _pinner.Count;
                ReleasePins(false);
                Notify("Stacking restored",
                    count > 0 ? count + " window(s) are normal windows again." : "No windows are pinned right now.",
                    ToolTipIcon.Info);
            };
            _menu.Items.Add(_unpinItem);

            _pinItem = new ToolStripMenuItem("Pin the window under the pointer (keep on top)");
            _pinItem.ToolTipText = "Pin a reference window on top, then interact normally with the window below it.";
            _pinItem.Click += delegate { PinUnderCursor(); };
            _menu.Items.Add(_pinItem);

            ToolStripMenuItem hotkeyItem = new ToolStripMenuItem("Change toggle shortcut…");
            hotkeyItem.Click += delegate { ChangeHotkey(); };
            _menu.Items.Add(hotkeyItem);

            _startupItem = new ToolStripMenuItem("Launch at Windows startup");
            _startupItem.Click += delegate
            {
                bool wanted = !IsStartupEnabled();
                if (!SetStartup(wanted))
                {
                    MessageBox.Show("Could not write the startup entry. A security tool may have blocked it.", "Non Activating Operate",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                UpdateUi();
            };
            _menu.Items.Add(_startupItem);

            _menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem helpItem = new ToolStripMenuItem("Help…");
            helpItem.Click += delegate { ShowHelp(); };
            _menu.Items.Add(helpItem);

            ToolStripMenuItem exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += delegate { ExitThread(); };
            _menu.Items.Add(exitItem);

            _tray = new NotifyIcon();
            _tray.ContextMenuStrip = _menu;
            _tray.Icon = _iconOff;
            _tray.Text = "Non Activating Operate";
            _tray.Visible = true;
            _tray.DoubleClick += delegate { SetEnabled(!_config.Enabled, true); };

            int hotkeyError;
            if (!_hotkeyHost.Register(HotkeyHost.ToggleHotkeyId, _config.Hotkey, out hotkeyError))
            {
                Log.Write("startup hotkey registration failed, error=" + hotkeyError);
                Notify("Shortcut unavailable",
                    "The shortcut " + _config.Hotkey.ToText() + " is already used by another program. Right-click the tray icon and choose \"Change toggle shortcut…\" to pick another one.",
                    ToolTipIcon.Warning);
            }

            if (_config.Enabled)
            {
                SetEnabled(true, false);
            }
            UpdateUi();

            if (firstRun)
            {
                Notify("Non Activating Operate is running",
                    "Press " + _config.Hotkey.ToText() + " to enable or disable. Background clicks stay in the background; clicking a text field brings its window forward so you can type.",
                    ToolTipIcon.Info);
            }
        }

        private void SetEnabled(bool enabled, bool announce)
        {
            if (enabled)
            {
                int error;
                if (!_hook.Install(out error))
                {
                    MessageBox.Show(
                        "Could not install the global mouse hook (error " + error + ").\nA security tool may be blocking it, or this session does not allow it.",
                        "Non Activating Operate", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Log.Write("enable failed, hook error=" + error);
                    enabled = false;
                }
                else
                {
                    _hook.ResetDownState();
                    _hook.KeepFrontMode = _config.KeepFront;
                }
            }
            else
            {
                RestoreBorrow(false);
                ReleasePins(false);
                _hook.Uninstall();
            }

            _config.Enabled = enabled;
            _config.Save();
            UpdateUi();
            Log.Write(enabled ? "state=enabled" : "state=disabled");

            if (announce)
            {
                if (_config.Sound) SystemSounds.Asterisk.Play();
                Notify(enabled ? "Enabled" : "Disabled",
                    enabled
                        ? "Clicks on background buttons and links no longer bring those windows forward. Clicking a text field brings its window forward so you can type. Press " +
                          _config.Hotkey.ToText() + " to disable."
                        : "Normal clicking is back. Press " + _config.Hotkey.ToText() + " to enable again.",
                    ToolTipIcon.Info);
            }
        }

        /// <summary>
        /// Keep-front mode: before a click activates a window, pin whatever covers it. This only
        /// lasts for the current typing session: switching to any other window releases it.
        /// </summary>
        private void OnBeforeActivate(IntPtr root)
        {
            if (!_config.KeepFront) return;

            if (_pinner.Count > 0)
            {
                // same window (typing session continues) -> keep; anything else -> back to normal
                if (root == _pinnedFor) return;
                ReleasePins(false);
                return;
            }

            int added = _pinner.PinAbove(root, _policy.IsAllowed);
            if (added <= 0) return;

            _pinnedFor = root;
            _pinTimer.Start();
            Log.Write("keep front: pinned " + added + " window(s) above 0x" + root.ToInt64().ToString("X"));
            _unpinItem.Enabled = true;
            if (!_pinnedHintShown)
            {
                _pinnedHintShown = true;
                Notify("Stacking preserved",
                    added + " window(s) above the target stay in front while you type. Switching to another window restores them automatically; you can also release them from the menu.",
                    ToolTipIcon.Info);
            }
        }

        /// <summary>Watches for "the user moved on" so the pins never stick around.</summary>
        private void CheckPinnedSession()
        {
            if (_pinner.Count == 0)
            {
                _pinTimer.Stop();
                return;
            }

            IntPtr foreground = Native.GetForegroundWindow();
            if (_pinnedFor == IntPtr.Zero || !Native.IsWindow(_pinnedFor))
            {
                ReleasePins(false);
                return;
            }

            // still in the window we pinned for -> keep the session alive
            if (foreground != _pinnedFor)
            {
                ReleasePins(false);
                return;
            }

            if (_config.AutoReleaseIdle)
            {
                int idle = Native.IdleMilliseconds();
                if (idle >= IdleReleaseMs)
                {
                    Log.Write("keep front: idle " + idle + " ms, releasing");
                    ReleasePins(false);
                }
            }
        }

        private void ReleasePins(bool announce)
        {
            int count = _pinner.Count;
            if (count == 0)
            {
                _pinTimer.Stop();
                _pinnedFor = IntPtr.Zero;
                return;
            }

            _pinner.UnpinAll();
            _pinnedFor = IntPtr.Zero;
            _pinTimer.Stop();

            // the window the user just moved to must end up on top again, as usual
            IntPtr foreground = Native.GetForegroundWindow();
            if (foreground != IntPtr.Zero && Native.IsWindow(foreground))
            {
                Native.SetWindowPos(foreground, Native.HWND_TOP, 0, 0, 0, 0,
                    Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
            }

            UpdateUi();
            Log.Write("keep front: released " + count + " window(s)");
            if (announce)
            {
                Notify("Stacking restored",
                    count + " window(s) are normal windows again; the active window is on top.",
                    ToolTipIcon.Info);
            }
        }

        private void SetBehavior(bool keepFront)
        {
            if (_config.KeepFront == keepFront)
            {
                UpdateUi();
                return;
            }
            _config.KeepFront = keepFront;
            _config.Save();
            ReleasePins(false);
            _hook.KeepFrontMode = keepFront && _config.Enabled;
            UpdateUi();
            Log.Write("behavior=" + (keepFront ? "keepfront" : "click"));
            if (_config.Enabled && keepFront)
            {
                Notify("Switched to \"Preserve stacking\"",
                    "The clicked window activates normally so you can type, and the windows above it stay in front. Turning the feature off restores them.",
                    ToolTipIcon.Info);
            }
            else if (_config.Enabled)
            {
                Notify("Switched to \"Do not activate\"",
                    "Background clicks do not activate windows; clicking a text field brings its window forward so you can type.",
                    ToolTipIcon.Info);
            }
        }

        /// <summary>Runs on the hook thread: keep it short and do the detection on a worker.</summary>
        private void OnClickPosted(IntPtr root, Native.POINT point)
        {
            if (!_config.BorrowOnTextClick) return;
            if (_borrower.IsBorrowing) return;
            if (Interlocked.CompareExchange(ref _pendingDetect, 1, 0) != 0) return;

            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    if (!Native.IsWindow(root)) return;
                    IntPtr under = Native.WindowFromPoint(point);
                    bool isText = TextFieldDetector.LooksLikeTextInput(under) ||
                                  TextFieldDetector.IsUiAutomationEdit(point.X, point.Y, 500);
                    if (!isText) return;
                    if (!Native.IsWindow(root)) return;
                    _hotkeyHost.BeginInvoke(new MethodInvoker(delegate { BorrowForTyping(root, point); }));
                }
                catch (Exception ex)
                {
                    Log.Error("text field detection", ex);
                }
                finally
                {
                    Interlocked.Exchange(ref _pendingDetect, 0);
                }
            });
        }

        /// <summary>Clicking an input box in a background window hands the foreground to it.</summary>
        private void BorrowForTyping(IntPtr root, Native.POINT point)
        {
            if (_borrower.IsBorrowing) return;
            if (!Native.IsWindow(root)) return;
            if (Native.GetForegroundWindow() == root) return;

            IntPtr rootWindow = Native.Root(root);
            string title = WindowLabel(rootWindow);
            string info;
            if (!_borrower.Borrow(rootWindow, rootWindow, point, out info))
            {
                Log.Write("typing borrow failed: " + info);
                return;
            }

            Log.Write("typing borrow target=0x" + rootWindow.ToInt64().ToString("X") + " title='" + title + "'");
            ShowBorrowed(title);
            if (_config.Sound) SystemSounds.Asterisk.Play();

            if (!_typingHintShown)
            {
                _typingHintShown = true;
                Notify("Text field detected",
                    "Clicking a background text field brings its window forward so you can type. Buttons and links still do not activate the window. " +
                    "You can turn this behaviour off in the menu.",
                    ToolTipIcon.Info);
            }
        }

        private void ManualBorrow()
        {
            if (_borrower.IsBorrowing)
            {
                RestoreBorrow(true);
                return;
            }

            IntPtr target = WindowUnderCursor();
            if (target == IntPtr.Zero || !_policy.IsAllowed(target))
            {
                Notify("No window to bring forward", "Move the pointer over the window you want to type in, then choose this item again.", ToolTipIcon.Warning);
                return;
            }

            string title = WindowLabel(target);
            IntPtr clickRoot = _engine.LastClickedRoot == target ? target : IntPtr.Zero;
            string info;
            if (!_borrower.Borrow(target, clickRoot, _engine.LastClickPoint, out info))
            {
                Log.Write("manual borrow failed: " + info);
                if (_config.Sound) SystemSounds.Hand.Play();
                Notify("Cannot bring the window forward", info, ToolTipIcon.Warning);
                return;
            }

            Log.Write("manual borrow target=0x" + target.ToInt64().ToString("X") + " title='" + title + "'");
            ShowBorrowed(title);
            if (_config.Sound) SystemSounds.Asterisk.Play();
            Notify("Brought forward: " + title,
                "Type normally; Chinese input works. Restore it from the menu (\"Restore the temporarily foregrounded window\") or by turning the feature off.",
                ToolTipIcon.Info);
        }

        /// <summary>Pin/unpin the window under the cursor so a reference window stays visible.</summary>
        private void PinUnderCursor()
        {
            IntPtr target = WindowUnderCursor();
            if (target == IntPtr.Zero || !_policy.IsAllowed(target))
            {
                Notify("No window to pin", "Move the pointer over the window, then choose this item again.", ToolTipIcon.Warning);
                return;
            }

            bool topmost = (Native.GetExStyle(target) & Native.WS_EX_TOPMOST) != 0;
            bool ok = Native.SetWindowPos(target,
                topmost ? Native.HWND_NOTOPMOST : Native.HWND_TOPMOST,
                0, 0, 0, 0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
            string title = WindowLabel(target);
            Log.Write("pin " + (topmost ? "off" : "on") + " title='" + title + "' ok=" + ok);

            if (!ok)
            {
                Notify("Operation failed", "Windows refused to change this window's topmost state.", ToolTipIcon.Warning);
                return;
            }
            if (_config.Sound) SystemSounds.Asterisk.Play();
            Notify(topmost ? "Unpinned: " + title : "Pinned on top: " + title,
                topmost
                    ? "It behaves like a normal window again."
                    : "It stays above other windows. Interact with the lower window normally; choose this item again to unpin it.",
                ToolTipIcon.Info);
        }

        private void RestoreBorrow(bool announce)
        {
            if (!_borrower.IsBorrowing) return;
            _borrower.Restore();
            Log.Write("borrow restored");
            UpdateUi();
            if (announce)
            {
                if (_config.Sound) SystemSounds.Asterisk.Play();
                Notify("Restored", "The window is back in its original position and the previous window has focus again.", ToolTipIcon.Info);
            }
        }

        private void ShowBorrowed(string title)
        {
            _tray.Icon = _iconBorrow;
            string text = "Brought forward: " + title;
            _tray.Text = text.Length > 63 ? text.Substring(0, 63) : text;
            _restoreItem.Enabled = true;
        }

        private static string WindowLabel(IntPtr hwnd)
        {
            string title = Native.WindowTitle(hwnd);
            if (title.Length == 0) title = Native.ProcessName(Native.Pid(hwnd));
            if (title.Length > 40) title = title.Substring(0, 40) + "…";
            return title;
        }

        private void ChangeHotkey()
        {
            HotkeyBinding previous = _config.Hotkey;
            _hotkeyHost.Unregister(HotkeyHost.ToggleHotkeyId);
            bool changed = false;
            try
            {
                using (HotkeyDialog dialog = new HotkeyDialog(previous))
                {
                    if (dialog.ShowDialog() == DialogResult.OK && dialog.Result != null)
                    {
                        int error;
                        if (_hotkeyHost.Register(HotkeyHost.ToggleHotkeyId, dialog.Result, out error))
                        {
                            _config.Hotkey = dialog.Result;
                            _config.Save();
                            changed = true;
                            Log.Write("hotkey changed to " + dialog.Result.ToText());
                        }
                        else
                        {
                            Log.Write("hotkey change failed, error=" + error);
                            MessageBox.Show(
                                "Could not register " + dialog.Result.ToText() + " (another program may be using it). Keeping " + previous.ToText() + ".",
                                "Change shortcut", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                }
            }
            finally
            {
                if (!changed)
                {
                    int error;
                    if (!_hotkeyHost.Register(HotkeyHost.ToggleHotkeyId, previous, out error))
                    {
                        Log.Write("hotkey re-register failed, error=" + error);
                    }
                }
                UpdateUi();
            }
        }

        private void UpdateUi()
        {
            _enableItem.Checked = _config.Enabled;
            _enableItem.Text = _config.Enabled
                ? "Disable: restore normal clicking"
                : "Enable: click background windows without activating them";
            _enableItem.ShortcutKeyDisplayString = _config.Hotkey.ToText();

            _clickModeItem.Checked = !_config.KeepFront;
            _keepFrontItem.Checked = _config.KeepFront;
            _typingItem.Enabled = !_config.KeepFront;
            _unpinItem.Enabled = _pinner.Count > 0;
            _idleItem.Checked = _config.AutoReleaseIdle;
            _typingItem.Checked = _config.BorrowOnTextClick;
            _dragItem.Checked = _config.RealDragHandover;
            _wheelItem.Checked = _config.WheelRedirect;
            _soundItem.Checked = _config.Sound;
            _startupItem.Checked = IsStartupEnabled();
            _restoreItem.Enabled = _borrower.IsBorrowing;

            _tray.Icon = _borrower.IsBorrowing ? _iconBorrow : (_config.Enabled ? _iconOn : _iconOff);

            string text;
            if (_borrower.IsBorrowing)
            {
                text = "Brought forward (restore from the menu)";
            }
            else
            {
                text = (_config.KeepFront ? "Preserve stacking: " : "Do not activate: ") +
                       (_config.Enabled ? "on" : "off") +
                       " (" + _config.Hotkey.ToText() + ")";
            }
            if (text.Length > 63) text = text.Substring(0, 63);
            _tray.Text = text;
        }

        private void Notify(string title, string text, ToolTipIcon icon)
        {
            try
            {
                _tray.BalloonTipTitle = title;
                _tray.BalloonTipText = text;
                _tray.BalloonTipIcon = icon;
                _tray.ShowBalloonTip(4000);
            }
            catch (Exception ex)
            {
                Log.Error("balloon", ex);
            }
        }

        private void ShowHelp()
        {
            string hotkey = _config.Hotkey.ToText();
            string text =
                hotkey + " toggles the feature on and off. You can also double-click the tray icon.\n\n" +
                "When enabled:\n" +
                "- Clicking buttons, links, or list items in a background window does not bring that window forward and does not steal keyboard focus.\n" +
                "- Clicking a text field in a background window brings its window forward, places the caret, and lets you type normally (Chinese input works).\n" +
                "- The active window, taskbar, notification area, and Start menu behave exactly as usual.\n" +
                "- Clicks on the title bar or border of a background window are ignored, because they always require activation.\n" +
                "- Dragging a file switches to a real drag after 10 pixels, which activates the window once.\n\n" +
                "Restoring:\n" +
                "- Right-click the tray icon and choose \"Restore the temporarily foregrounded window\", or press " + hotkey + " twice.\n\n" +
                "Limitations:\n" +
                "- Message-based applications work; games and some UWP apps read raw input and may ignore these clicks.\n" +
                "- To interact with elevated windows, run this tool as administrator.\n\n" +
                "Configuration and logs: %APPDATA%\\Non Activating Operate";
            MessageBox.Show(text, "Non Activating Operate - Help", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static IntPtr WindowUnderCursor()
        {
            Native.POINT point;
            if (!Native.GetCursorPos(out point)) return IntPtr.Zero;
            IntPtr under = Native.WindowFromPoint(point);
            return under == IntPtr.Zero ? IntPtr.Zero : Native.Root(under);
        }

        private static bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (key == null) return false;
                    return key.GetValue(RunValueName) != null;
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool SetStartup(bool enabled)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath))
                {
                    if (key == null) return false;
                    if (enabled)
                    {
                        key.SetValue(RunValueName, "\"" + Application.ExecutablePath + "\"");
                    }
                    else
                    {
                        key.DeleteValue(RunValueName, false);
                    }
                }
                Log.Write("startup=" + enabled);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("startup", ex);
                return false;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
                try { if (_borrower.IsBorrowing) _borrower.Restore(); } catch { }
                try { _config.Save(); } catch { }
                try { _hook.Dispose(); } catch { }
                try { _hotkeyHost.UnregisterAll(); _hotkeyHost.Dispose(); } catch { }
                try { _tray.Visible = false; _tray.Dispose(); } catch { }
                try { if (_iconOn != null) _iconOn.Dispose(); } catch { }
                try { if (_iconBorrow != null) _iconBorrow.Dispose(); } catch { }
                try { if (_iconOff != null) _iconOff.Dispose(); } catch { }
                Log.Write("tray context disposed");
            }
            base.Dispose(disposing);
        }
    }
}