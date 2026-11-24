using System;
using System.Drawing;
using System.Windows.Forms;

namespace NonActivatingOperate
{
    /// <summary>Invisible window that owns the global hotkey registrations.</summary>
    internal sealed class HotkeyHost : Form
    {
        public const int ToggleHotkeyId = 0x4E41;


        public event EventHandler TogglePressed;

        public HotkeyHost()
        {
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = new Rectangle(-32000, -32000, 1, 1);
            ShowIcon = false;
            Text = "Non Activating Operate";
            IntPtr unused = Handle; // force handle creation for RegisterHotKey
        }

        public bool Register(int id, HotkeyBinding binding, out int error)
        {
            error = 0;
            Unregister(id);
            if (binding == null) return false;

            uint modifiers = binding.NativeModifiers | Native.MOD_NOREPEAT;
            if (!Native.RegisterHotKey(Handle, id, modifiers, (uint)binding.Key))
            {
                error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                return false;
            }
            Log.Write("hotkey registered: " + binding.ToText());
            return true;
        }

        public void Unregister(int id)
        {
            Native.UnregisterHotKey(Handle, id);
        }

        public void UnregisterAll()
        {
            Unregister(ToggleHotkeyId);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                EventHandler handler = id == ToggleHotkeyId ? TogglePressed : null;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                    return;
                }
            }
            base.WndProc(ref m);
        }
    }
}
