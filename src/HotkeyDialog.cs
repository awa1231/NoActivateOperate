using System;
using System.Drawing;
using System.Windows.Forms;

namespace NoActivateClick
{
    /// <summary>Small dialog that captures a new hotkey. Layout is scaled by hand because
    /// WinForms (without its DPI opt-in) keeps 96 dpi metrics while GDI+ draws at the real dpi.</summary>
    internal sealed class HotkeyDialog : Form
    {
        private readonly Label _combo;
        private readonly Label _hint;
        private readonly float _scale;
        private HotkeyBinding _captured;

        public HotkeyBinding Result;

        public HotkeyDialog(HotkeyBinding current)
        {
            Text = "设置快捷键";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;
            KeyPreview = true;

            IntPtr unused = Handle; // dpi is only known once the window exists
            _scale = UiScale.For(Handle);

            ClientSize = new Size(S(440), S(208));

            Label title = new Label();
            title.Text = "按下要使用的快捷键组合（至少包含 Ctrl / Alt / Shift 之一）：";
            title.AutoSize = false;
            title.Bounds = new Rectangle(S(18), S(16), S(404), S(40));
            Controls.Add(title);

            Panel box = new Panel();
            box.BorderStyle = BorderStyle.FixedSingle;
            box.Bounds = new Rectangle(S(18), S(62), S(404), S(56));
            Controls.Add(box);

            _combo = new Label();
            _combo.Dock = DockStyle.Fill;
            _combo.TextAlign = ContentAlignment.MiddleCenter;
            _combo.Font = new Font(Font.FontFamily, 14f, FontStyle.Bold);
            _combo.Text = current != null ? current.ToText() : "未设置";
            box.Controls.Add(_combo);

            _hint = new Label();
            _hint.Text = "当前：" + (current != null ? current.ToText() : "未设置");
            _hint.AutoSize = false;
            _hint.Bounds = new Rectangle(S(18), S(126), S(404), S(20));
            _hint.ForeColor = SystemColors.GrayText;
            Controls.Add(_hint);

            Button ok = new Button();
            ok.Text = "确定";
            ok.Bounds = new Rectangle(S(246), S(158), S(88), S(32));
            ok.Click += delegate { Accept(); };
            Controls.Add(ok);

            Button cancel = new Button();
            cancel.Text = "取消";
            cancel.Bounds = new Rectangle(S(342), S(158), S(80), S(32));
            cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
            _captured = current;

            KeyDown += OnDialogKeyDown;
        }

        private int S(int value)
        {
            return (int)Math.Round(value * _scale);
        }

        private void OnDialogKeyDown(object sender, KeyEventArgs e)
        {
            if (HotkeyBinding.IsModifierKey(e.KeyCode))
            {
                e.SuppressKeyPress = true;
                return;
            }
            if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Enter) return;

            HotkeyBinding binding = HotkeyBinding.FromKeys(e.KeyCode, e.Modifiers);
            e.SuppressKeyPress = true;
            if (!binding.IsValid)
            {
                _hint.Text = "请同时按住 Ctrl / Alt / Shift 中的一个键";
                return;
            }

            _captured = binding;
            _combo.Text = binding.ToText();
            _hint.Text = "当前：" + _captured.ToText();
        }

        private void Accept()
        {
            if (_captured == null || !_captured.IsValid)
            {
                MessageBox.Show(this, "请先按下一个包含 Ctrl / Alt / Shift 的快捷键组合。", "设置快捷键",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Result = _captured;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    internal static class UiScale
    {
        public static float For(IntPtr hwnd)
        {
            uint dpi = Native.GetDpiForWindowSafe(hwnd);
            if (dpi < 72) dpi = 96;
            if (dpi > 480) dpi = 480;
            return dpi / 96f;
        }
    }
}
