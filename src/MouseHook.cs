using System;
using System.Runtime.InteropServices;

namespace NoActivateClick
{
    /// <summary>Global low level mouse hook that reroutes clicks instead of activating windows.</summary>
    internal sealed class MouseHook : IDisposable
    {
        private const int BtnLeft = 0;
        private const int BtnRight = 1;
        private const int BtnMiddle = 2;

        /// <summary>Marks the single left-down that we inject when a synthetic press turns into a drag.</summary>
        internal const int HandoverSignature = 0x4E414348; // "NACH"

        private readonly Native.HookProc _proc;
        private readonly ClickEngine _engine;
        private readonly bool[] _downSwallowed = new bool[3];
        private readonly bool[] _downPosted = new bool[3];
        private readonly IntPtr[] _downTarget = new IntPtr[3];
        private readonly int[] _clickCount = new int[3];
        private readonly int[] _lastDownTick = new int[3];
        private readonly Native.POINT[] _lastDownPoint = new Native.POINT[3];
        private readonly IntPtr[] _lastDownHwnd = new IntPtr[3];
        private readonly bool[] _handover = new bool[3];
        private IntPtr _hook;
        private int _lastMoveTick;
        private int _dragThreshold;
        private bool _disposed;

        public bool WheelRedirect;
        /// <summary>Keep the current stacking: pin the covering windows, then let the click through.</summary>
        public bool KeepFrontMode;
        public Action<IntPtr> BeforeActivate = null;
        public bool RealDragHandover = true;
        public Action<int> Observer = null;
        /// <summary>Called right after a click was posted to a background window.</summary>
        public Action<IntPtr, Native.POINT> ClickPosted = null;
        public Action<string> Diagnostics = null;

        public MouseHook(ClickEngine engine)
        {
            _engine = engine;
            _proc = Callback;
        }

        public bool IsInstalled { get { return _hook != IntPtr.Zero; } }

        public bool Install(out int error)
        {
            error = 0;
            if (_hook != IntPtr.Zero) return true;
            _hook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, _proc, Native.GetModuleHandle(null), 0);
            if (_hook == IntPtr.Zero)
            {
                error = Marshal.GetLastWin32Error();
                Log.Write("SetWindowsHookEx failed, error=" + error);
                return false;
            }
            Log.Write("mouse hook installed");
            return true;
        }

        public void Uninstall()
        {
            if (_hook == IntPtr.Zero) return;
            Native.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
            Log.Write("mouse hook removed");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Uninstall();
        }

        private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0)
                {
                    int message = wParam.ToInt32();
                    if (Observer != null) Observer(message);
                    Native.MSLLHOOKSTRUCT data = (Native.MSLLHOOKSTRUCT)
                        Marshal.PtrToStructure(lParam, typeof(Native.MSLLHOOKSTRUCT));
                    if (Handle(message, data)) return new IntPtr(1);
                }
            }
            catch (Exception ex)
            {
                Log.Error("mouse hook callback", ex);
            }
            return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        private bool Handle(int message, Native.MSLLHOOKSTRUCT data)
        {
            // Never intercept input that we produced ourselves (drag handover).
            if (data.dwExtraInfo.ToInt32() == HandoverSignature) return false;

            switch (message)
            {
                case Native.WM_LBUTTONDOWN:
                    return OnDown(BtnLeft, message, data);
                case Native.WM_LBUTTONUP:
                    return OnUp(BtnLeft, message, data);
                case Native.WM_RBUTTONDOWN:
                    return OnDown(BtnRight, message, data);
                case Native.WM_RBUTTONUP:
                    return OnUp(BtnRight, message, data);
                case Native.WM_MBUTTONDOWN:
                    return OnDown(BtnMiddle, message, data);
                case Native.WM_MBUTTONUP:
                    return OnUp(BtnMiddle, message, data);
                case Native.WM_MOUSEMOVE:
                    return OnMove(data);
                case Native.WM_MOUSEWHEEL:
                case Native.WM_MOUSEHWHEEL:
                    return OnWheel(message, data);
                default:
                    return false;
            }
        }

        private bool OnDown(int button, int message, Native.MSLLHOOKSTRUCT data)
        {
            if (KeepFrontMode)
            {
                if (message == Native.WM_LBUTTONDOWN && BeforeActivate != null)
                {
                    IntPtr root = Native.Root(Native.WindowFromPoint(data.pt));
                    IntPtr foreground = Native.GetForegroundWindow();
                    if (root != IntPtr.Zero && (foreground == IntPtr.Zero || Native.Root(foreground) != root))
                    {
                        try { BeforeActivate(root); }
                        catch (Exception ex) { Log.Error("keep front", ex); }
                    }
                }
                return false;
            }

            // A low level hook only reports the raw button transition; the WM_*BUTTONDBLCLK
            // conversion happens later in the input queue. Re-create it here so double clicks
            // still work on a window that never gets real input.
            IntPtr under = Native.WindowFromPoint(data.pt);
            int effective = message;
            int now = Environment.TickCount;
            bool sameTarget = under != IntPtr.Zero && under == _lastDownHwnd[button];
            bool inTime = sameTarget && unchecked(now - _lastDownTick[button]) <= (int)Native.GetDoubleClickTime();
            bool inRange = inTime && WithinDoubleClickRange(data.pt, _lastDownPoint[button]);
            if (inRange)
            {
                _clickCount[button] = _clickCount[button] + 1;
                if (_clickCount[button] >= 2) effective = DoubleClickMessage(message);
            }
            else
            {
                _clickCount[button] = 1;
            }

            IntPtr target;
            ClickOutcome outcome = _engine.MouseDown(effective, data.pt.X, data.pt.Y, out target);
            _downSwallowed[button] = outcome != ClickOutcome.PassThrough;
            _downPosted[button] = outcome == ClickOutcome.Posted;
            _downTarget[button] = target;
            _handover[button] = false;

            if (outcome == ClickOutcome.Posted)
            {
                _lastDownHwnd[button] = under;
                _lastDownTick[button] = now;
                _lastDownPoint[button] = data.pt;
            }
            else
            {
                _clickCount[button] = 0;
                _lastDownHwnd[button] = IntPtr.Zero;
            }

            if (outcome == ClickOutcome.Posted && ClickPosted != null)
            {
                ClickPosted(target, data.pt);
            }

            if (Diagnostics != null)
            {
                Diagnostics("down msg=0x" + message.ToString("X") + " effective=0x" + effective.ToString("X") +
                            " at=" + data.pt + " target=" + Native.Describe(target) +
                            " outcome=" + outcome + " count=" + _clickCount[button]);
            }
            return _downSwallowed[button];
        }

        private bool OnUp(int button, int message, Native.MSLLHOOKSTRUCT data)
        {
            if (KeepFrontMode) return false;

            bool swallowed = _downSwallowed[button];
            bool posted = _downPosted[button];
            IntPtr target = _downTarget[button];
            _downSwallowed[button] = false;
            _downPosted[button] = false;
            _downTarget[button] = IntPtr.Zero;
            _handover[button] = false;

            if (posted) _engine.PostTo(target, message, data.pt.X, data.pt.Y, 0);
            if (Diagnostics != null && (swallowed || posted))
            {
                Diagnostics("up   msg=0x" + message.ToString("X") + " at=" + data.pt +
                            " posted=" + posted + " swallowed=" + swallowed);
            }
            return swallowed;
        }

        /// <summary>Keep a synthetic drag alive, or hand the drag over to real input.</summary>
        private bool OnMove(Native.MSLLHOOKSTRUCT data)
        {
            if (KeepFrontMode) return false;

            int now = Environment.TickCount;
            if (unchecked(now - _lastMoveTick) < 4) return false;

            bool any = false;
            for (int i = 0; i < 3; i++)
            {
                if (_downPosted[i]) { any = true; break; }
            }
            if (!any) return false;

            _lastMoveTick = now;

            if (RealDragHandover && _downPosted[BtnLeft] && !_handover[BtnLeft] &&
                Distance(data.pt, _lastDownPoint[BtnLeft]) > DragThreshold())
            {
                BeginHandover(BtnLeft, data.pt);
                if (Diagnostics != null) Diagnostics("handover to real drag at=" + data.pt);
                return false;
            }

            for (int i = 0; i < 3; i++)
            {
                if (!_downPosted[i]) continue;
                int keyState = i == BtnLeft ? Native.MK_LBUTTON
                             : i == BtnRight ? Native.MK_RBUTTON
                             : Native.MK_MBUTTON;
                _engine.PostTo(_downTarget[i], Native.WM_MOUSEMOVE, data.pt.X, data.pt.Y, keyState);
            }
            if (Diagnostics != null) Diagnostics("move forwarded at=" + data.pt);
            return false;
        }

        private bool OnWheel(int message, Native.MSLLHOOKSTRUCT data)
        {
            if (!WheelRedirect) return false;
            int delta = unchecked((short)((data.mouseData >> 16) & 0xFFFF));
            ClickOutcome outcome = _engine.Wheel(message, data.pt.X, data.pt.Y, delta);
            if (Diagnostics != null) Diagnostics("wheel delta=" + delta + " outcome=" + outcome);
            return outcome == ClickOutcome.Posted;
        }

        /// <summary>
        /// A real drag (files, OLE) cannot be faked with messages: the source has to own the
        /// mouse and run its own drag loop. When the press turns into a drag, cancel the fake
        /// press and replay it as real input so drag and drop works. The target window will be
        /// activated by that click - unavoidable, a drag needs the foreground.
        /// </summary>
        private void BeginHandover(int button, Native.POINT at)
        {
            IntPtr target = _downTarget[button];
            Native.POINT start = _lastDownPoint[button];
            if (target != IntPtr.Zero)
            {
                // cancel the fake press so the control does not stay in a pressed state
                _engine.PostTo(target, Native.WM_LBUTTONUP, start.X, start.Y, 0);
            }

            _handover[button] = true;
            _downPosted[button] = false;
            _downSwallowed[button] = false;
            _downTarget[button] = IntPtr.Zero;
            _clickCount[button] = 0;
            _lastDownHwnd[button] = IntPtr.Zero;

            Native.SendMouse(at.X, at.Y, Native.MOUSEEVENTF_LEFTDOWN, new IntPtr(HandoverSignature));
            Log.Write("drag handover at " + at);
        }

        private int DragThreshold()
        {
            if (_dragThreshold == 0)
            {
                int cx = Native.GetSystemMetrics(Native.SM_CXDRAG);
                int cy = Native.GetSystemMetrics(Native.SM_CYDRAG);
                if (cx < 4) cx = 4;
                if (cy < 4) cy = 4;
                _dragThreshold = Math.Max(10, Math.Max(cx, cy));
            }
            return _dragThreshold;
        }

        private static int Distance(Native.POINT a, Native.POINT b)
        {
            int dx = a.X - b.X;
            int dy = a.Y - b.Y;
            return (int)Math.Sqrt(dx * dx + dy * dy);
        }

        private static bool WithinDoubleClickRange(Native.POINT a, Native.POINT b)
        {
            int cx = Native.GetSystemMetrics(Native.SM_CXDOUBLECLK);
            int cy = Native.GetSystemMetrics(Native.SM_CYDOUBLECLK);
            if (cx <= 0) cx = 4;
            if (cy <= 0) cy = 4;
            return Math.Abs(a.X - b.X) <= cx && Math.Abs(a.Y - b.Y) <= cy;
        }

        private static int DoubleClickMessage(int downMessage)
        {
            switch (downMessage)
            {
                case Native.WM_LBUTTONDOWN: return Native.WM_LBUTTONDBLCLK;
                case Native.WM_RBUTTONDOWN: return Native.WM_RBUTTONDBLCLK;
                case Native.WM_MBUTTONDOWN: return Native.WM_MBUTTONDBLCLK;
                default: return downMessage;
            }
        }

        internal void ResetDownState()
        {
            for (int i = 0; i < 3; i++)
            {
                _downSwallowed[i] = false;
                _downPosted[i] = false;
                _downTarget[i] = IntPtr.Zero;
                _clickCount[i] = 0;
                _lastDownHwnd[i] = IntPtr.Zero;
                _handover[i] = false;
            }
        }
    }
}