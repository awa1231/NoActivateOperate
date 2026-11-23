using System;
using System.Drawing;
using System.Threading;
using System.Windows.Automation;

namespace NoActivateClick
{
    /// <summary>Tells whether a click landed in something that accepts typed text.</summary>
    internal static class TextFieldDetector
    {
        public static bool LooksLikeTextInput(IntPtr hwnd)
        {
            IntPtr current = hwnd;
            for (int i = 0; i < 6 && current != IntPtr.Zero; i++)
            {
                if (IsEditClass(Native.ClassName(current))) return true;
                current = Native.GetParent(current);
            }
            return false;
        }

        public static bool IsEditClass(string className)
        {
            if (string.IsNullOrEmpty(className)) return false;
            if (className.IndexOf("edit", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (className.IndexOf("textbox", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (className.IndexOf("scintilla", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        /// <summary>UI Automation check; returns false on timeout or any failure.</summary>
        public static bool IsUiAutomationEdit(int x, int y, int timeoutMs)
        {
            bool result = false;
            ManualResetEvent done = new ManualResetEvent(false);
            Thread worker = new Thread(delegate()
            {
                try { result = Probe(new Point(x, y)); }
                catch { }
                finally { done.Set(); }
            });
            worker.IsBackground = true;
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();
            bool finished = done.WaitOne(timeoutMs);
            return finished && result;
        }

        private static bool Probe(Point point)
        {
            try
            {
                AutomationElement element = AutomationElement.FromPoint(new System.Windows.Point(point.X, point.Y));
                if (element == null) return false;
                object type = element.GetCurrentPropertyValue(AutomationElement.ControlTypeProperty);
                if (type is ControlType && (ControlType)type == ControlType.Edit) return true;
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}