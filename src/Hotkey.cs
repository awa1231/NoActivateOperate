using System;
using System.Text;
using System.Windows.Forms;

namespace NonActivatingOperate
{
    [Flags]
    internal enum HotkeyModifiers
    {
        None = 0,
        Alt = 1,
        Control = 2,
        Shift = 4,
        Win = 8
    }

    internal sealed class HotkeyBinding
    {
        public Keys Key;
        public HotkeyModifiers Modifiers;

        public HotkeyBinding(Keys key, HotkeyModifiers modifiers)
        {
            Key = key;
            Modifiers = modifiers;
        }

        public static HotkeyBinding Default()
        {
            return new HotkeyBinding(Keys.F, HotkeyModifiers.Control);
        }

        public static HotkeyBinding BorrowDefault()
        {
            return new HotkeyBinding(Keys.T, HotkeyModifiers.Control | HotkeyModifiers.Alt);
        }

        public bool IsValid
        {
            get { return Key != Keys.None && Modifiers != HotkeyModifiers.None && !IsModifierKey(Key); }
        }

        public uint NativeModifiers
        {
            get
            {
                uint value = 0;
                if ((Modifiers & HotkeyModifiers.Alt) != 0) value |= Native.MOD_ALT;
                if ((Modifiers & HotkeyModifiers.Control) != 0) value |= Native.MOD_CONTROL;
                if ((Modifiers & HotkeyModifiers.Shift) != 0) value |= Native.MOD_SHIFT;
                if ((Modifiers & HotkeyModifiers.Win) != 0) value |= Native.MOD_WIN;
                return value;
            }
        }

        public string ToText()
        {
            StringBuilder sb = new StringBuilder();
            if ((Modifiers & HotkeyModifiers.Control) != 0) sb.Append("Ctrl+");
            if ((Modifiers & HotkeyModifiers.Alt) != 0) sb.Append("Alt+");
            if ((Modifiers & HotkeyModifiers.Shift) != 0) sb.Append("Shift+");
            if ((Modifiers & HotkeyModifiers.Win) != 0) sb.Append("Win+");
            sb.Append(KeyText(Key));
            return sb.ToString();
        }

        public static string KeyText(Keys key)
        {
            if (key >= Keys.A && key <= Keys.Z) return ((char)('A' + ((int)key - (int)Keys.A))).ToString();
            if (key >= Keys.D0 && key <= Keys.D9) return ((char)('0' + ((int)key - (int)Keys.D0))).ToString();
            if (key >= Keys.NumPad0 && key <= Keys.NumPad9) return "Num" + ((int)key - (int)Keys.NumPad0);
            return key.ToString();
        }

        public static bool IsModifierKey(Keys key)
        {
            return key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu ||
                   key == Keys.LControlKey || key == Keys.RControlKey ||
                   key == Keys.LShiftKey || key == Keys.RShiftKey ||
                   key == Keys.LMenu || key == Keys.RMenu ||
                   key == Keys.LWin || key == Keys.RWin ||
                   key == Keys.None;
        }

        public static HotkeyBinding FromKeys(Keys key, Keys modifiers)
        {
            HotkeyModifiers mods = HotkeyModifiers.None;
            if ((modifiers & Keys.Control) == Keys.Control) mods |= HotkeyModifiers.Control;
            if ((modifiers & Keys.Alt) == Keys.Alt) mods |= HotkeyModifiers.Alt;
            if ((modifiers & Keys.Shift) == Keys.Shift) mods |= HotkeyModifiers.Shift;
            return new HotkeyBinding(key, mods);
        }

        public static bool TryParse(string text, out HotkeyBinding binding)
        {
            binding = null;
            if (string.IsNullOrEmpty(text)) return false;

            string[] parts = text.Split('+');
            HotkeyModifiers mods = HotkeyModifiers.None;
            Keys key = Keys.None;

            for (int i = 0; i < parts.Length; i++)
            {
                string token = parts[i].Trim();
                if (token.Length == 0) return false;

                string lower = token.ToLowerInvariant();
                if (lower == "ctrl" || lower == "control") { mods |= HotkeyModifiers.Control; continue; }
                if (lower == "alt") { mods |= HotkeyModifiers.Alt; continue; }
                if (lower == "shift") { mods |= HotkeyModifiers.Shift; continue; }
                if (lower == "win" || lower == "windows") { mods |= HotkeyModifiers.Win; continue; }

                if (key != Keys.None) return false;
                if (!TryParseKey(token, out key)) return false;
            }

            binding = new HotkeyBinding(key, mods);
            return binding.IsValid;
        }

        private static bool TryParseKey(string token, out Keys key)
        {
            key = Keys.None;
            if (token.Length == 1)
            {
                char c = char.ToUpperInvariant(token[0]);
                if (c >= 'A' && c <= 'Z')
                {
                    key = (Keys)(int)c;
                    return true;
                }
                if (c >= '0' && c <= '9')
                {
                    key = (Keys)((int)Keys.D0 + (c - '0'));
                    return true;
                }
            }

            try
            {
                key = (Keys)Enum.Parse(typeof(Keys), token, true);
                return key != Keys.None;
            }
            catch
            {
                return false;
            }
        }

        public override string ToString()
        {
            return ToText();
        }
    }
}
