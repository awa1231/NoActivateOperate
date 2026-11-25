using System;
using System.Threading;

namespace NonActivatingOperate
{
    /// <summary>
    /// "Borrow the foreground": bring a background window forward so it can be typed into with
    /// the real keyboard (IME included), then put it back where it was.
    /// </summary>
    internal sealed class ForegroundBorrower
    {
        private IntPtr _borrowed = IntPtr.Zero;
        private IntPtr _previous = IntPtr.Zero;
        private IntPtr _above = IntPtr.Zero;
        private bool _wasMinimized;

        public bool IsBorrowing { get { return _borrowed != IntPtr.Zero; } }
        public IntPtr BorrowedWindow { get { return _borrowed; } }

        /// <summary>Takes the window root. Returns false (with a message) if it cannot be activated.</summary>
        public bool Borrow(IntPtr hwnd, out string message)
        {
            return Borrow(hwnd, IntPtr.Zero, new Native.POINT(), out message);
        }

        /// <summary>
        /// clickPoint is the screen point of the click the user made in that window; after the
        /// window is in front the click is replayed for real, so the text field really gets the
        /// keyboard focus (synthetic clicks are ignored by some toolkits).
        /// </summary>
        public bool Borrow(IntPtr hwnd, IntPtr clickRoot, Native.POINT clickPoint, out string message)
        {
            message = "";
            if (IsBorrowing) Restore();

            if (hwnd == IntPtr.Zero || !Native.IsWindow(hwnd))
            {
                message = "No window to bring forward";
                return false;
            }

            IntPtr root = Native.Root(hwnd);
            IntPtr foreground = Native.GetForegroundWindow();
            if (foreground == root)
            {
                message = "It is already in the foreground - just type";
                return false;
            }

            _previous = foreground;
            _above = VisibleWindowAbove(root);
            _wasMinimized = Native.IsIconic(root);
            Log.Write("borrow target=" + Native.Describe(root) + " previous=" + Native.Describe(foreground) +
                      " above=" + Native.Describe(_above));
            if (_wasMinimized) Native.ShowWindow(root, Native.SW_RESTORE);

            if (!Native.ForceForeground(root))
            {
                _previous = IntPtr.Zero;
                _above = IntPtr.Zero;
                message = "Windows refused the foreground switch. Try again.";
                return false;
            }

            _borrowed = root;

            if (clickRoot == root && Native.IsWindowVisible(root))
            {
                Native.RECT windowRect;
                Native.GetWindowRect(root, out windowRect);
                if (clickPoint.X >= windowRect.Left && clickPoint.X < windowRect.Right &&
                    clickPoint.Y >= windowRect.Top && clickPoint.Y < windowRect.Bottom)
                {
                    Thread.Sleep(120);
                    Native.SendMouse(clickPoint.X, clickPoint.Y, Native.MOUSEEVENTF_LEFTDOWN,
                        new IntPtr(MouseHook.HandoverSignature));
                    Thread.Sleep(35);
                    Native.SendMouse(clickPoint.X, clickPoint.Y, Native.MOUSEEVENTF_LEFTUP,
                        new IntPtr(MouseHook.HandoverSignature));
                    Log.Write("borrow re-click at " + clickPoint);
                }
            }

            Log.Write("borrow state: " + Native.FocusInfo(root));
            message = Native.ProcessName(Native.Pid(root));
            return true;
        }

        /// <summary>The nearest visible window directly above the target in z-order.</summary>
        private static IntPtr VisibleWindowAbove(IntPtr hwnd)
        {
            IntPtr current = Native.GetWindow(hwnd, Native.GW_HWNDPREV);
            int guard = 0;
            while (current != IntPtr.Zero && guard++ < 50)
            {
                if (Native.IsWindowVisible(current)) return current;
                current = Native.GetWindow(current, Native.GW_HWNDPREV);
            }
            return IntPtr.Zero;
        }

        public void Restore()
        {
            IntPtr target = _borrowed;
            IntPtr previous = _previous;
            IntPtr above = _above;
            bool minimized = _wasMinimized;

            _borrowed = IntPtr.Zero;
            _previous = IntPtr.Zero;
            _above = IntPtr.Zero;
            _wasMinimized = false;

            if (target == IntPtr.Zero || !Native.IsWindow(target)) return;

            // Only take the focus back if the borrowed window still has it; if the user already
            // moved on by themselves, leave the focus alone.
            bool stillActive = Native.GetForegroundWindow() == target;
            if (stillActive && previous != IntPtr.Zero && Native.IsWindow(previous) && previous != target)
            {
                Native.ForceForeground(previous);
            }

            if (above != IntPtr.Zero && Native.IsWindow(above) && above != target)
            {
                bool moved = Native.SetWindowPos(target, above, 0, 0, 0, 0,
                    Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
                Log.Write("borrow restore below=" + Native.Describe(above) + " moved=" + moved);
            }
            else
            {
                Log.Write("borrow restore: no anchor window (above=" + Native.Describe(above) + ")");
            }

            if (minimized && Native.IsWindow(target))
            {
                Native.ShowWindow(target, Native.SW_MINIMIZE);
            }
        }
    }
}