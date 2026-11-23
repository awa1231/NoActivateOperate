using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace NoActivateClick
{
    /// <summary>Thin P/Invoke layer. Everything the app (and the tests) need.</summary>
    internal static class Native
    {
        // ---------------- window messages ----------------
        public const int WM_MOUSEMOVE = 0x0200;
        public const int WM_LBUTTONDOWN = 0x0201;
        public const int WM_LBUTTONUP = 0x0202;
        public const int WM_LBUTTONDBLCLK = 0x0203;
        public const int WM_RBUTTONDOWN = 0x0204;
        public const int WM_RBUTTONUP = 0x0205;
        public const int WM_RBUTTONDBLCLK = 0x0206;
        public const int WM_MBUTTONDOWN = 0x0207;
        public const int WM_MBUTTONUP = 0x0208;
        public const int WM_MBUTTONDBLCLK = 0x0209;
        public const int WM_MOUSEWHEEL = 0x020A;
        public const int WM_XBUTTONDOWN = 0x020B;
        public const int WM_XBUTTONUP = 0x020C;
        public const int WM_MOUSEHWHEEL = 0x020E;
        public const int WM_GETTEXT = 0x000D;
        public const int WM_HOTKEY = 0x0312;
        public const uint SMTO_ABORTIFHUNG = 0x0002;
        public const int WM_STYLECHANGING = 0x007C;
        public const int WM_STYLECHANGED = 0x007D;

        // ---------------- mouse keys ----------------
        public const int MK_LBUTTON = 0x0001;
        public const int MK_RBUTTON = 0x0002;
        public const int MK_SHIFT = 0x0004;
        public const int MK_CONTROL = 0x0008;
        public const int MK_MBUTTON = 0x0010;

        // ---------------- styles / z-order ----------------
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_NOACTIVATE = 0x08000000;
        public const int WS_EX_TOPMOST = 0x00000008;
        public const int WS_EX_TRANSPARENT = 0x00000020;

        public static readonly IntPtr HWND_TOP = IntPtr.Zero;
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

        public const int SW_SHOW = 5;
        public const int SW_MINIMIZE = 6;
        public const int SW_RESTORE = 9;
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_NOACTIVATE = 0x0010;

        public const uint GA_ROOT = 2;
        public const uint GW_HWNDNEXT = 2;
        public const uint GW_HWNDPREV = 3;

        // ---------------- hooks / hotkeys ----------------
        public const int WH_MOUSE_LL = 14;
        public const int WH_KEYBOARD_LL = 13;
        public const int WM_KEYDOWN = 0x0100;
        public const int WM_KEYUP = 0x0101;
        public const int WM_SYSKEYDOWN = 0x0104;
        public const int WM_SYSKEYUP = 0x0105;
        public const int VK_CONTROL = 0x11;
        public const int VK_MENU = 0x12;
        public const int VK_LWIN = 0x5B;
        public const int VK_RWIN = 0x5C;
        public const uint KEYEVENTF_KEYUP = 0x0002;
        public const int MOD_ALT = 0x0001;
        public const int MOD_CONTROL = 0x0002;
        public const int MOD_SHIFT = 0x0004;
        public const int MOD_WIN = 0x0008;
        public const int MOD_NOREPEAT = 0x4000;

        // ---------------- DPI ----------------
        public const int DPI_AWARENESS_UNAWARE = 0;
        public const int DPI_AWARENESS_SYSTEM_AWARE = 1;
        public const int DPI_AWARENESS_PER_MONITOR_AWARE = 2;

        // ---------------- SendInput ----------------
        public const uint MOUSEEVENTF_MOVE = 0x0001;
        public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        public const uint MOUSEEVENTF_LEFTUP = 0x0004;
        public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        public const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        public const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        public const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
        public const uint MOUSEEVENTF_WHEEL = 0x0800;
        public const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

        // ---------------- structs ----------------
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
            public POINT(int x, int y) { X = x; Y = y; }
            public override string ToString() { return "(" + X + "," + Y + ")"; }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
            public int Width { get { return Right - Left; } }
            public int Height { get { return Bottom - Top; } }
            public override string ToString()
            {
                return "[" + Left + "," + Top + " " + Width + "x" + Height + "]";
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct GUITHREADINFO
        {
            public int cbSize;
            public uint flags;
            public IntPtr hwndActive;
            public IntPtr hwndFocus;
            public IntPtr hwndCapture;
            public IntPtr hwndMenuOwner;
            public IntPtr hwndMoveSize;
            public IntPtr hwndCaret;
            public RECT rcCaret;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public int mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct INPUTUNION
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public uint type;
            public INPUTUNION u;
        }

        public const uint INPUT_MOUSE = 0;

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        public delegate bool EnumChildProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool EnumChildWindows(IntPtr hWndParent, EnumChildProc lpEnumFunc, IntPtr lParam);

        // ---------------- user32 ----------------
        [DllImport("user32.dll")]
        public static extern IntPtr WindowFromPoint(POINT point);

        [DllImport("user32.dll")]
        public static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

        [DllImport("user32.dll")]
        public static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        public static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr SetActiveWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr SendMessageTimeout(IntPtr hWnd, int msg, IntPtr wParam,
            StringBuilder lParam, uint flags, uint timeout, out IntPtr result);

        [DllImport("user32.dll")]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO gui);

        [DllImport("user32.dll")]
        public static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
            int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetTopWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsHungAppWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern uint GetDoubleClickTime();

        [DllImport("user32.dll")]
        public static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [DllImport("user32.dll")]
        public static extern uint MapVirtualKey(uint code, uint mapType);

        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

        public const int SM_CXDOUBLECLK = 36;
        public const int SM_CYDOUBLECLK = 37;
        public const int SM_CXDRAG = 68;
        public const int SM_CYDRAG = 69;
        public const int SM_XVIRTUALSCREEN = 76;
        public const int SM_YVIRTUALSCREEN = 77;
        public const int SM_CXVIRTUALSCREEN = 78;
        public const int SM_CYVIRTUALSCREEN = 79;

        [DllImport("user32.dll")]
        public static extern IntPtr GetMessageExtraInfo();

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        public const int VK_LBUTTON = 0x01;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        public static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindowDpiAwarenessContext(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern int GetAwarenessFromDpiAwarenessContext(IntPtr value);

        [DllImport("user32.dll")]
        public static extern uint GetDpiForWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern uint GetDpiForSystem();

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
        private static extern IntPtr GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
        private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        // ---------------- kernel32 ----------------
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        // ---------------- helpers ----------------
        public static void TryEnableDpiAwareness()
        {
            try
            {
                // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 == -4
                if (SetProcessDpiAwarenessContext(new IntPtr(-4))) return;
            }
            catch (EntryPointNotFoundException) { }
            catch (DllNotFoundException) { }

            try { SetProcessDPIAware(); }
            catch (EntryPointNotFoundException) { }
            catch (DllNotFoundException) { }
        }

        public static int GetAwareness(IntPtr hwnd)
        {
            try
            {
                IntPtr ctx = GetWindowDpiAwarenessContext(hwnd);
                if (ctx == IntPtr.Zero) return -1;
                return GetAwarenessFromDpiAwarenessContext(ctx);
            }
            catch (EntryPointNotFoundException) { return -1; }
            catch (DllNotFoundException) { return -1; }
        }

        public static uint GetDpiForWindowSafe(IntPtr hwnd)
        {
            try
            {
                uint dpi = GetDpiForWindow(hwnd);
                if (dpi != 0) return dpi;
            }
            catch (EntryPointNotFoundException) { }
            catch (DllNotFoundException) { }
            return 96;
        }

        public static uint GetDpiForSystemSafe()
        {
            try
            {
                uint dpi = GetDpiForSystem();
                if (dpi != 0) return dpi;
            }
            catch (EntryPointNotFoundException) { }
            catch (DllNotFoundException) { }
            return 96;
        }

        public static int GetExStyle(IntPtr hwnd)
        {
            IntPtr value = IntPtr.Size == 8
                ? GetWindowLongPtr64(hwnd, GWL_EXSTYLE)
                : GetWindowLong32(hwnd, GWL_EXSTYLE);
            return unchecked((int)value.ToInt64());
        }

        public static bool SetExStyle(IntPtr hwnd, int style)
        {
            IntPtr value = new IntPtr(style);
            if (IntPtr.Size == 8) SetWindowLongPtr64(hwnd, GWL_EXSTYLE, value);
            else SetWindowLong32(hwnd, GWL_EXSTYLE, value);
            int now = GetExStyle(hwnd);
            return now == style;
        }

        public static IntPtr MakeLParam(POINT p)
        {
            int packed = ((p.Y & 0xFFFF) << 16) | (p.X & 0xFFFF);
            return new IntPtr(packed);
        }

        public static string ClassName(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return "";
            StringBuilder sb = new StringBuilder(256);
            int n = GetClassName(hwnd, sb, sb.Capacity);
            return n > 0 ? sb.ToString(0, n) : "";
        }

        public static uint Pid(IntPtr hwnd)
        {
            uint pid;
            GetWindowThreadProcessId(hwnd, out pid);
            return pid;
        }

        public static string ProcessName(uint pid)
        {
            try
            {
                using (Process p = Process.GetProcessById((int)pid))
                {
                    return p.ProcessName;
                }
            }
            catch
            {
                return "";
            }
        }

        /// <summary>Milliseconds since the last keyboard or mouse input in this session.</summary>
        public static int IdleMilliseconds()
        {
            LASTINPUTINFO info = new LASTINPUTINFO();
            info.cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO));
            if (!GetLastInputInfo(ref info)) return 0;
            return unchecked(Environment.TickCount - (int)info.dwTime);
        }

        public static IntPtr Root(IntPtr hwnd)
        {
            IntPtr root = GetAncestor(hwnd, GA_ROOT);
            return root != IntPtr.Zero ? root : hwnd;
        }

        /// <summary>Screen rect of the window's client area.</summary>
        public static RECT ClientScreenRect(IntPtr hwnd)
        {
            RECT rc;
            GetClientRect(hwnd, out rc);
            POINT tl = new POINT(rc.Left, rc.Top);
            POINT br = new POINT(rc.Right, rc.Bottom);
            ClientToScreen(hwnd, ref tl);
            ClientToScreen(hwnd, ref br);
            RECT result;
            result.Left = tl.X;
            result.Top = tl.Y;
            result.Right = br.X;
            result.Bottom = br.Y;
            return result;
        }

        /// <summary>
        /// Bring a window to the foreground even when this process is in the background.
        /// The foreground lock normally refuses SetForegroundWindow; temporarily attaching to
        /// the current foreground thread lifts that restriction.
        /// </summary>
        public static bool ForceForeground(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !IsWindow(hwnd)) return false;
            if (GetForegroundWindow() == hwnd) return true;

            IntPtr foreground = GetForegroundWindow();
            uint fgThread = 0;
            if (foreground != IntPtr.Zero)
            {
                uint fgPid;
                fgThread = GetWindowThreadProcessId(foreground, out fgPid);
            }
            uint ourThread = GetCurrentThreadId();
            bool attached = false;
            try
            {
                if (fgThread != 0 && fgThread != ourThread)
                {
                    attached = AttachThreadInput(fgThread, ourThread, true);
                }
                if (SetForegroundWindow(hwnd)) return true;

                BringWindowToTop(hwnd);
                SetActiveWindow(hwnd);
                if (GetForegroundWindow() == hwnd) return true;

                SwitchToThisWindow(hwnd, true);
                return GetForegroundWindow() == hwnd;
            }
            finally
            {
                if (attached)
                {
                    try { AttachThreadInput(fgThread, ourThread, false); }
                    catch { }
                }
            }
        }

        /// <summary>Which window of that thread currently holds the keyboard focus (diagnostics).</summary>
        public static string FocusInfo(IntPtr hwnd)
        {
            uint pid;
            uint thread = GetWindowThreadProcessId(hwnd, out pid);
            if (thread == 0) return "focus=?";
            GUITHREADINFO info = new GUITHREADINFO();
            info.cbSize = Marshal.SizeOf(typeof(GUITHREADINFO));
            if (!GetGUIThreadInfo(thread, ref info)) return "focus=?";
            return "focus=" + Describe(info.hwndFocus);
        }

        /// <summary>Window caption, or "" when the app does not answer in time.</summary>
        public static string WindowTitle(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return "";
            try
            {
                StringBuilder buffer = new StringBuilder(512);
                IntPtr ignored;
                IntPtr ok = SendMessageTimeout(hwnd, WM_GETTEXT, new IntPtr(511), buffer,
                    SMTO_ABORTIFHUNG, 150, out ignored);
                if (ok == IntPtr.Zero) return "";
                return buffer.ToString().Trim();
            }
            catch
            {
                return "";
            }
        }

        public static string Describe(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return "(null)";
            uint pid = Pid(hwnd);
            return "0x" + hwnd.ToInt64().ToString("X") + " " + ClassName(hwnd) + " pid=" + pid +
                   " " + ProcessName(pid);
        }

        /// <summary>
        /// One atomic event: jump to the screen point and press/release a button in the same
        /// SendInput batch, so a stray mouse movement can never separate the two.
        /// </summary>
        public static uint SendMouse(int screenX, int screenY, uint buttonFlags, IntPtr extraInfo)
        {
            int vx = GetSystemMetrics(SM_XVIRTUALSCREEN);
            int vy = GetSystemMetrics(SM_YVIRTUALSCREEN);
            int vw = GetSystemMetrics(SM_CXVIRTUALSCREEN);
            int vh = GetSystemMetrics(SM_CYVIRTUALSCREEN);
            if (vw <= 1) vw = 2;
            if (vh <= 1) vh = 2;

            INPUT[] inputs = new INPUT[1];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].u.mi.dx = (int)Math.Round((screenX - vx) * 65535.0 / (vw - 1));
            inputs[0].u.mi.dy = (int)Math.Round((screenY - vy) * 65535.0 / (vh - 1));
            inputs[0].u.mi.dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | buttonFlags;
            inputs[0].u.mi.dwExtraInfo = extraInfo;
            return SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        /// <summary>Press and release a virtual key with one SendInput batch.</summary>
        public static uint SendKey(int vk, IntPtr extraInfo)
        {
            INPUT[] inputs = new INPUT[2];
            inputs[0].type = 1;
            inputs[0].u.ki.wVk = (ushort)vk;
            inputs[0].u.ki.dwExtraInfo = extraInfo;
            inputs[1].type = 1;
            inputs[1].u.ki.wVk = (ushort)vk;
            inputs[1].u.ki.dwFlags = KEYEVENTF_KEYUP;
            inputs[1].u.ki.dwExtraInfo = extraInfo;
            return SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        public static uint SendWheel(int screenX, int screenY, int delta, IntPtr extraInfo)
        {
            int vx = GetSystemMetrics(SM_XVIRTUALSCREEN);
            int vy = GetSystemMetrics(SM_YVIRTUALSCREEN);
            int vw = GetSystemMetrics(SM_CXVIRTUALSCREEN);
            int vh = GetSystemMetrics(SM_CYVIRTUALSCREEN);
            if (vw <= 1) vw = 2;
            if (vh <= 1) vh = 2;

            INPUT[] inputs = new INPUT[2];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].u.mi.dx = (int)Math.Round((screenX - vx) * 65535.0 / (vw - 1));
            inputs[0].u.mi.dy = (int)Math.Round((screenY - vy) * 65535.0 / (vh - 1));
            inputs[0].u.mi.dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE;
            inputs[0].u.mi.dwExtraInfo = extraInfo;

            inputs[1].type = INPUT_MOUSE;
            inputs[1].u.mi.mouseData = delta;
            inputs[1].u.mi.dwFlags = MOUSEEVENTF_WHEEL;
            inputs[1].u.mi.dwExtraInfo = extraInfo;
            return SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        public static void SendMouseButton(bool down, bool left, bool right, IntPtr extraInfo)
        {
            INPUT[] inputs = new INPUT[1];
            inputs[0].type = INPUT_MOUSE;
            uint flags;
            if (left) flags = down ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_LEFTUP;
            else if (right) flags = down ? MOUSEEVENTF_RIGHTDOWN : MOUSEEVENTF_RIGHTUP;
            else flags = down ? MOUSEEVENTF_MIDDLEDOWN : MOUSEEVENTF_MIDDLEUP;
            inputs[0].u.mi.dwFlags = flags;
            inputs[0].u.mi.dwExtraInfo = extraInfo;
            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }
    }
}