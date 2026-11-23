using System;
using System.Collections.Generic;

namespace NoActivateClick
{
    /// <summary>
    /// Keeps the current stacking order: before a click activates a window, every visible window
    /// that currently covers it is made topmost (in its current order). The clicked window then
    /// activates normally but stays underneath them, so the picture on screen does not change.
    /// </summary>
    internal sealed class KeepFrontPinner
    {
        private readonly List<IntPtr> _pinned = new List<IntPtr>();
        private readonly HashSet<IntPtr> _pinnedSet = new HashSet<IntPtr>();

        public int Count { get { return _pinned.Count; } }

        /// <summary>Pins the windows that cover <paramref name="target"/>. Returns how many were added.</summary>
        public int PinAbove(IntPtr target, Func<IntPtr, bool> allowed)
        {
            if (target == IntPtr.Zero || !Native.IsWindow(target)) return 0;
            if (allowed != null && !allowed(target)) return 0;

            Native.RECT targetRect;
            if (!Native.GetWindowRect(target, out targetRect)) return 0;
            if (targetRect.Width <= 0 || targetRect.Height <= 0) return 0;

            // Everything above the target keeps its place, even windows that do not overlap it:
            // otherwise activating the target would push them below it and the order would change.
            List<IntPtr> cover = new List<IntPtr>();
            IntPtr current = Native.GetTopWindow(IntPtr.Zero);
            int guard = 0;
            while (current != IntPtr.Zero && current != target && guard++ < 600)
            {
                if (current != target &&
                    !_pinnedSet.Contains(current) &&
                    Native.IsWindowVisible(current) &&
                    !Native.IsIconic(current) &&
                    (Native.GetExStyle(current) & Native.WS_EX_TOPMOST) == 0 &&
                    (allowed == null || allowed(current)))
                {
                    cover.Add(current);
                }
                current = Native.GetWindow(current, Native.GW_HWNDNEXT);
            }

            int added = 0;
            // bottom-most first, so the group keeps its original order at the top
            for (int i = cover.Count - 1; i >= 0; i--)
            {
                IntPtr window = cover[i];
                if (!Native.IsWindow(window)) continue;
                if (Native.SetWindowPos(window, Native.HWND_TOPMOST, 0, 0, 0, 0,
                        Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE))
                {
                    _pinned.Add(window);
                    _pinnedSet.Add(window);
                    added++;
                }
            }
            return added;
        }

        /// <summary>Gives every pinned window its normal stacking back.</summary>
        public void UnpinAll()
        {
            // same order they were pinned in, so the group lands back in the original order
            for (int i = 0; i < _pinned.Count; i++)
            {
                IntPtr window = _pinned[i];
                if (!Native.IsWindow(window)) continue;
                Native.SetWindowPos(window, Native.HWND_NOTOPMOST, 0, 0, 0, 0,
                    Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
            }
            _pinned.Clear();
            _pinnedSet.Clear();
        }

        private static bool Intersects(IntPtr hwnd, Native.RECT target)
        {
            Native.RECT rect;
            if (!Native.GetWindowRect(hwnd, out rect)) return false;
            return rect.Left < target.Right && rect.Right > target.Left &&
                   rect.Top < target.Bottom && rect.Bottom > target.Top;
        }
    }
}