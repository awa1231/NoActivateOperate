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

            _enableItem = new ToolStripMenuItem("开启：点击后台窗口不把它叫到前面");
            _enableItem.Click += delegate { SetEnabled(!_config.Enabled, true); };
            _menu.Items.Add(_enableItem);
            _menu.Items.Add(new ToolStripSeparator());

            _clickModeItem = new ToolStripMenuItem("点击后：不激活窗口（点按钮、链接）");
            _clickModeItem.ToolTipText = "点后台窗口不会把它叫到前面。要在后台窗口里打字，再点输入框会自动切前台。";
            _clickModeItem.Click += delegate { SetBehavior(false); };
            _menu.Items.Add(_clickModeItem);

            _keepFrontItem = new ToolStripMenuItem("点击后：保持当前遮挡关系（点下面窗口打字）");
            _keepFrontItem.ToolTipText = "点下面的窗口会正常激活、可以直接打字；上面盖着它的窗口会被保持在前，画面不变。";
            _keepFrontItem.Click += delegate { SetBehavior(true); };
            _menu.Items.Add(_keepFrontItem);

            _menu.Items.Add(new ToolStripSeparator());

            _typingItem = new ToolStripMenuItem("点击后台输入框时自动切到前台，方便打字");
            _typingItem.ToolTipText = "点后台窗口的输入框会把它借到前台，直接打字就行；点按钮、链接仍然不会激活窗口。";
            _typingItem.Click += delegate
            {
                _config.BorrowOnTextClick = !_config.BorrowOnTextClick;
                _config.Save();
                UpdateUi();
            };
            _menu.Items.Add(_typingItem);

            _dragItem = new ToolStripMenuItem("拖拽时改用真实拖放（会激活窗口）");
            _dragItem.ToolTipText = "合成点击无法启动文件/OLE 拖放。开启后，一旦按住拖动，就切换成真实鼠标拖动。";
            _dragItem.Click += delegate
            {
                _config.RealDragHandover = !_config.RealDragHandover;
                _hook.RealDragHandover = _config.RealDragHandover;
                _config.Save();
                UpdateUi();
            };
            _menu.Items.Add(_dragItem);

            _wheelItem = new ToolStripMenuItem("滚轮滚动光标下的后台窗口");
            _wheelItem.Click += delegate
            {
                _config.WheelRedirect = !_config.WheelRedirect;
                _hook.WheelRedirect = _config.WheelRedirect;
                _config.Save();
                UpdateUi();
            };
            _menu.Items.Add(_wheelItem);

            _soundItem = new ToolStripMenuItem("提示音");
            _soundItem.Click += delegate
            {
                _config.Sound = !_config.Sound;
                _config.Save();
                UpdateUi();
            };
            _menu.Items.Add(_soundItem);

            _menu.Items.Add(new ToolStripSeparator());

            _manualItem = new ToolStripMenuItem("临时前置光标下的窗口（打字用）");
            _manualItem.Click += delegate { ManualBorrow(); };
            _menu.Items.Add(_manualItem);

            _restoreItem = new ToolStripMenuItem("把临时前置的窗口放回原处");
            _restoreItem.Click += delegate { RestoreBorrow(true); };
            _menu.Items.Add(_restoreItem);

            _idleItem = new ToolStripMenuItem("停止打字 " + (IdleReleaseMs / 1000) + " 秒后自动解除保持");
            _idleItem.ToolTipText = "打字停下后，被保持在前面的窗口会自动恢复普通叠层，不用手动解除。";
            _idleItem.Click += delegate
            {
                _config.AutoReleaseIdle = !_config.AutoReleaseIdle;
                _config.Save();
                UpdateUi();
            };
            _menu.Items.Add(_idleItem);

            _unpinItem = new ToolStripMenuItem("解除“保持遮挡”（恢复正常叠层）");
            _unpinItem.Click += delegate
            {
                int count = _pinner.Count;
                ReleasePins(false);
                Notify("已恢复普通叠层",
                    count > 0 ? "那 " + count + " 个窗口已经恢复成普通窗口。" : "现在没有钉住的窗口。",
                    ToolTipIcon.Info);
            };
            _menu.Items.Add(_unpinItem);

            _pinItem = new ToolStripMenuItem("钉住光标下的窗口（保持最前，再点取消）");
            _pinItem.ToolTipText = "把上面的窗口钉在最前，然后正常点下面那个窗口打字：上面的窗口不会被挡住。";
            _pinItem.Click += delegate { PinUnderCursor(); };
            _menu.Items.Add(_pinItem);

            ToolStripMenuItem hotkeyItem = new ToolStripMenuItem("更改开关快捷键…");
            hotkeyItem.Click += delegate { ChangeHotkey(); };
            _menu.Items.Add(hotkeyItem);

            _startupItem = new ToolStripMenuItem("开机自动启动");
            _startupItem.Click += delegate
            {
                bool wanted = !IsStartupEnabled();
                if (!SetStartup(wanted))
                {
                    MessageBox.Show("写入开机启动项失败，可能被安全软件拦截了。", "不激活点击",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                UpdateUi();
            };
            _menu.Items.Add(_startupItem);

            _menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem helpItem = new ToolStripMenuItem("使用说明…");
            helpItem.Click += delegate { ShowHelp(); };
            _menu.Items.Add(helpItem);

            ToolStripMenuItem exitItem = new ToolStripMenuItem("退出");
            exitItem.Click += delegate { ExitThread(); };
            _menu.Items.Add(exitItem);

            _tray = new NotifyIcon();
            _tray.ContextMenuStrip = _menu;
            _tray.Icon = _iconOff;
            _tray.Text = "不激活点击";
            _tray.Visible = true;
            _tray.DoubleClick += delegate { SetEnabled(!_config.Enabled, true); };

            int hotkeyError;
            if (!_hotkeyHost.Register(HotkeyHost.ToggleHotkeyId, _config.Hotkey, out hotkeyError))
            {
                Log.Write("startup hotkey registration failed, error=" + hotkeyError);
                Notify("快捷键被占用",
                    "快捷键 " + _config.Hotkey.ToText() + " 已被其他程序占用。请右键托盘图标，用“更改开关快捷键…”换一个。",
                    ToolTipIcon.Warning);
            }

            if (_config.Enabled)
            {
                SetEnabled(true, false);
            }
            UpdateUi();

            if (firstRun)
            {
                Notify("不激活点击已启动",
                    "按 " + _config.Hotkey.ToText() + " 开启/关闭。开启后点后台窗口不会把它叫到前面；点输入框会自动切到前台方便打字。",
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
                        "无法安装全局鼠标钩子（错误码 " + error + "）。\n可能被安全软件拦截，或当前会话不允许。",
                        "不激活点击", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                Notify(enabled ? "已开启" : "已关闭",
                    enabled
                        ? "点后台窗口的按钮/链接不会把它带到前面；点输入框会自动切到前台，直接打字即可。再按 " +
                          _config.Hotkey.ToText() + " 关闭。"
                        : "点击已恢复普通行为。按 " + _config.Hotkey.ToText() + " 可再次开启。",
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
                Notify("已保持当前遮挡关系",
                    "上面盖着它的 " + added + " 个窗口会在你打字期间保持在前；换到别的窗口会自动恢复。菜单里也能手动解除。",
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
                Notify("已恢复正常叠层",
                    count + " 个窗口恢复成普通窗口，现在的窗口在上面。",
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
                Notify("已切换到“保持遮挡”",
                    "点下面的窗口会正常激活、可以直接打字，上面盖着它的窗口保持在前。关掉开关会解除。",
                    ToolTipIcon.Info);
            }
            else if (_config.Enabled)
            {
                Notify("已切换到“不激活点击”",
                    "点后台窗口不会把它叫到前面；点输入框会自动切前台方便打字。",
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
                Notify("已切到输入框所在的窗口",
                    "点后台输入框会自动把窗口借到前台，直接打字就行；点按钮、链接仍然不会激活窗口。" +
                    "不需要这个行为可以在菜单里关掉。",
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
                Notify("没有可借的窗口", "把鼠标移到要打字的窗口上，再点这一项。", ToolTipIcon.Warning);
                return;
            }

            string title = WindowLabel(target);
            IntPtr clickRoot = _engine.LastClickedRoot == target ? target : IntPtr.Zero;
            string info;
            if (!_borrower.Borrow(target, clickRoot, _engine.LastClickPoint, out info))
            {
                Log.Write("manual borrow failed: " + info);
                if (_config.Sound) SystemSounds.Hand.Play();
                Notify("没法临时前置", info, ToolTipIcon.Warning);
                return;
            }

            Log.Write("manual borrow target=0x" + target.ToInt64().ToString("X") + " title='" + title + "'");
            ShowBorrowed(title);
            if (_config.Sound) SystemSounds.Asterisk.Play();
            Notify("已临时前置：" + title,
                "直接打字就行（中文也可以）。放回原处可以用菜单里的“把临时前置的窗口放回原处”，或者关掉开关。",
                ToolTipIcon.Info);
        }

        /// <summary>Pin/unpin the window under the cursor so a reference window stays visible.</summary>
        private void PinUnderCursor()
        {
            IntPtr target = WindowUnderCursor();
            if (target == IntPtr.Zero || !_policy.IsAllowed(target))
            {
                Notify("没有可钉住的窗口", "把鼠标移到要钉住的窗口上，再点这一项。", ToolTipIcon.Warning);
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
                Notify("操作失败", "系统拒绝修改这个窗口的置顶状态。", ToolTipIcon.Warning);
                return;
            }
            if (_config.Sound) SystemSounds.Asterisk.Play();
            Notify(topmost ? "已取消置顶：" + title : "已钉在最前面：" + title,
                topmost
                    ? "它恢复成普通窗口，会随点击被盖住。"
                    : "它会一直浮在最上层。现在正常点下面那个窗口打字，也不会挡住它；再点这一项可以取消。",
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
                Notify("已放回原处", "窗口回到了原来的层级，焦点也还回去了。", ToolTipIcon.Info);
            }
        }

        private void ShowBorrowed(string title)
        {
            _tray.Icon = _iconBorrow;
            string text = "已切到前台：" + title;
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
                                "快捷键 " + dialog.Result.ToText() + " 注册失败（可能已被其他程序占用），保留原来的 " +
                                previous.ToText() + "。",
                                "设置快捷键", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                ? "关闭：点击恢复普通行为"
                : "开启：点击后台窗口不把它叫到前面";
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
                text = "已切到前台（菜单里可以放回）";
            }
            else
            {
                text = (_config.KeepFront ? "保持遮挡：" : "不激活点击：") +
                       (_config.Enabled ? "已开启" : "已关闭") +
                       "（" + _config.Hotkey.ToText() + "）";
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
                "快捷键 " + hotkey + "：开启 / 关闭。也可以双击托盘图标。\n\n" +
                "开启后：\n" +
                "· 点后台窗口里的按钮、链接、列表，不会把这个窗口带到前面，也不会抢走键盘焦点。\n" +
                "· 点后台窗口里的输入框，会自动把它切到前台并定位光标，直接打字就行（中文也可以）。\n" +
                "· 点当前正在使用的窗口、任务栏、托盘、开始菜单，行为和平时完全一样。\n" +
                "· 后台窗口的标题栏和边框上的点击会被忽略（那类点击必然要激活窗口）。\n" +
                "· 拖文件/拖放时，按住拖出 10 像素会自动切换成真实拖动（窗口会被激活一次）。\n\n" +
                "放回原处：\n" +
                "· 右键托盘图标 →“把临时前置的窗口放回原处”；或者按 " + hotkey + " 关掉开关再打开。\n\n" +
                "限制：\n" +
                "· 依赖窗口消息的程序都能用；游戏、部分 UWP/商店应用自己读原始输入，可能没反应。\n" +
                "· 想点管理员权限的窗口，需要以管理员身份运行本程序。\n\n" +
                "配置和日志：%APPDATA%\\Non Activating Operate";
            MessageBox.Show(text, "不激活点击 - 使用说明", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
