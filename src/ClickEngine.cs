using System;
using System.Collections.Generic;

namespace NoActivateClick
{
    internal enum ClickOutcome
    {
        PassThrough = 0,
        Posted = 1,
        Blocked = 2
    }

    /// <summary>Decides which windows may be clicked without activating them.</summary>
    internal sealed class ClickTargetPolicy
    {
        private static readonly HashSet<string> ShellClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Shell_TrayWnd",
            "Shell_SecondaryTrayWnd",
            "NotifyIconOverflowWindow",
            "TopLevelWindowForOverflowXamlIsland",
            "TaskListThumbnailWnd",
            "TaskListOverlayWnd",
            "MultitaskingViewFrame",
            "XamlExplorerHostIslandWindow",
            "Shell_InputSwitchTopLevelWindow",
            "DV2ControlHost",
            "ForegroundStaging",
            "Windows.UI.Core.CoreWindow"
        };

        private static readonly HashSet<string> ShellHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "StartMenuExperienceHost",
            "ShellExperienceHost",
            "SearchHost",
            "SearchUI",
            "TextInputHost",
            "LockApp",
            "explorer"
        };

        private readonly uint _ownPid;

        public bool ExcludeOwnProcess = true;
        public Func<IntPtr, bool> ExtraFilter = null;

        public ClickTargetPolicy()
        {
            _ownPid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        }

        public bool IsAllowed(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return false;
            if (ExcludeOwnProcess && Native.Pid(hwnd) == _ownPid) return false;

            IntPtr root = Native.Root(hwnd);
            string cls = Native.ClassName(root);

            if (ShellClasses.Contains(cls))
            {
                // "Windows.UI.Core.CoreWindow" is also used by ordinary UWP app windows, so only
                // reject it when a shell host owns it.
                if (cls != "Windows.UI.Core.CoreWindow") return false;
                if (ShellHosts.Contains(Native.ProcessName(Native.Pid(root)))) return false;
            }

            if (ExtraFilter != null && !ExtraFilter(hwnd)) return false;
            return true;
        }
    }

    /// <summary>
    /// Mode A: turn a swallowed physical click into window messages addressed to the window
    /// under the cursor. The target is never activated, so the z-order cannot change.
    /// </summary>
    internal sealed class ClickEngine
    {
        private readonly ClickTargetPolicy _policy;

        public bool DpiScaling = true;
        public string LastTarget = "";
        /// <summary>Top level window of the last click that was posted (for "borrow foreground").</summary>
        public IntPtr LastClickedRoot = IntPtr.Zero;
        /// <summary>Screen point of the last posted click (used to restore focus when borrowing).</summary>
        public Native.POINT LastClickPoint = new Native.POINT();
        /// <summary>Tick count of the last posted click.</summary>
        public int LastClickTick = 0;

        public ClickEngine(ClickTargetPolicy policy)
        {
            _policy = policy;
        }

        public ClickOutcome MouseDown(int message, int screenX, int screenY, out IntPtr target)
        {
            target = IntPtr.Zero;
            Native.POINT pt = new Native.POINT(screenX, screenY);
            IntPtr hwnd = Native.WindowFromPoint(pt);
            if (hwnd == IntPtr.Zero) return ClickOutcome.PassThrough;
            if (!_policy.IsAllowed(hwnd)) return ClickOutcome.PassThrough;

            IntPtr root = Native.Root(hwnd);

            // Only background windows are treated specially. Clicking the already active window
            // stays a completely normal click (drag, text selection, everything).
            if (IsForegroundTarget(hwnd)) return ClickOutcome.PassThrough;

            if (root == hwnd && !IsInClientArea(root, pt))
            {
                // Caption, border or scrollbar: a real click here would activate and raise the
                // window, and a posted one cannot drive the modal move loop. Ignore it instead.
                return ClickOutcome.Blocked;
            }

            Native.POINT client = ToClient(hwnd, pt);
            if (!Post(hwnd, Native.WM_MOUSEMOVE, 0, client)) return ClickOutcome.PassThrough;
            if (!Post(hwnd, message, ButtonState(message), client)) return ClickOutcome.PassThrough;

            target = hwnd;
            LastClickedRoot = root;
            LastClickPoint = pt;
            LastClickTick = Environment.TickCount;
            LastTarget = Native.Describe(hwnd);
            return ClickOutcome.Posted;
        }

        /// <summary>True when the window (or its top level parent) is the active foreground window.</summary>
        public bool IsForegroundTarget(IntPtr hwnd)
        {
            IntPtr foreground = Native.GetForegroundWindow();
            if (foreground == IntPtr.Zero) return false;
            return Native.Root(foreground) == Native.Root(hwnd);
        }

        public bool PostTo(IntPtr target, int message, int screenX, int screenY, int keyState)
        {
            if (target == IntPtr.Zero || !Native.IsWindow(target)) return false;
            Native.POINT client = ToClient(target, new Native.POINT(screenX, screenY));
            return Post(target, message, keyState, client);
        }

        public ClickOutcome Wheel(int message, int screenX, int screenY, int delta)
        {
            Native.POINT pt = new Native.POINT(screenX, screenY);
            IntPtr hwnd = Native.WindowFromPoint(pt);
            if (hwnd == IntPtr.Zero) return ClickOutcome.PassThrough;
            if (!_policy.IsAllowed(hwnd)) return ClickOutcome.PassThrough;

            if (IsForegroundTarget(hwnd)) return ClickOutcome.PassThrough;

            IntPtr wParam = new IntPtr((delta & 0xFFFF) << 16);
            if (!Native.PostMessage(hwnd, message, wParam, Native.MakeLParam(pt)))
            {
                return ClickOutcome.PassThrough;
            }
            return ClickOutcome.Posted;
        }

        public static int ButtonState(int message)
        {
            switch (message)
            {
                case Native.WM_LBUTTONDOWN:
                case Native.WM_LBUTTONDBLCLK:
                    return Native.MK_LBUTTON;
                case Native.WM_RBUTTONDOWN:
                case Native.WM_RBUTTONDBLCLK:
                    return Native.MK_RBUTTON;
                case Native.WM_MBUTTONDOWN:
                case Native.WM_MBUTTONDBLCLK:
                    return Native.MK_MBUTTON;
                default:
                    return 0;
            }
        }

        public Native.POINT ToClient(IntPtr hwnd, Native.POINT screen)
        {
            Native.POINT p = screen;
            Native.ScreenToClient(hwnd, ref p);
            if (!DpiScaling) return p;

            int baseDpi = 0;
            int awareness = Native.GetAwareness(hwnd);
            if (awareness == Native.DPI_AWARENESS_UNAWARE) baseDpi = 96;
            else if (awareness == Native.DPI_AWARENESS_SYSTEM_AWARE) baseDpi = (int)Native.GetDpiForSystemSafe();
            if (baseDpi <= 0) return p;

            int monitorDpi = (int)Native.GetDpiForWindowSafe(hwnd);
            if (monitorDpi <= 0 || monitorDpi == baseDpi) return p;

            double scale = (double)baseDpi / monitorDpi;
            p.X = (int)Math.Round(p.X * scale);
            p.Y = (int)Math.Round(p.Y * scale);
            return p;
        }

        private static bool IsInClientArea(IntPtr root, Native.POINT screen)
        {
            Native.RECT rc = Native.ClientScreenRect(root);
            return screen.X >= rc.Left && screen.X < rc.Right &&
                   screen.Y >= rc.Top && screen.Y < rc.Bottom;
        }

        private static bool Post(IntPtr hwnd, int message, int keyState, Native.POINT client)
        {
            return Native.PostMessage(hwnd, message, new IntPtr(keyState), Native.MakeLParam(client));
        }
    }
}