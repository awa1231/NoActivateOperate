using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace NoActivateClick
{
    internal static class AppIcons
    {
        /// <summary>Two stacked windows: the front one turns green when the mode is on.</summary>
        public static Icon Create(bool active)
        {
            return Create(active ? Color.FromArgb(255, 56, 210, 122) : Color.FromArgb(255, 158, 163, 170), active);
        }

        /// <summary>Blue front window: a background window is currently borrowed.</summary>
        public static Icon CreateBorrowing()
        {
            return Create(Color.FromArgb(255, 64, 156, 255), true);
        }

        private static Icon Create(Color frontColor, bool active)
        {
            using (Bitmap bitmap = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);

                    Rectangle backRect = new Rectangle(4, 4, 17, 14);
                    using (SolidBrush backBrush = new SolidBrush(Color.FromArgb(255, 88, 98, 112)))
                    using (Pen backPen = new Pen(Color.FromArgb(255, 32, 36, 42), 1.2f))
                    {
                        g.FillRectangle(backBrush, backRect);
                        g.DrawRectangle(backPen, backRect);
                    }

                    Rectangle frontRect = new Rectangle(11, 14, 17, 14);
                    using (SolidBrush frontBrush = new SolidBrush(frontColor))
                    using (Pen frontPen = new Pen(Color.FromArgb(255, 22, 26, 32), 1.4f))
                    {
                        g.FillRectangle(frontBrush, frontRect);
                        g.DrawRectangle(frontPen, frontRect);
                    }

                    if (active)
                    {
                        using (SolidBrush dot = new SolidBrush(Color.FromArgb(255, 255, 255, 255)))
                        {
                            g.FillEllipse(dot, 25, 4, 6, 6);
                        }
                    }
                }

                IntPtr handle = bitmap.GetHicon();
                try
                {
                    using (Icon temporary = Icon.FromHandle(handle))
                    {
                        return (Icon)temporary.Clone();
                    }
                }
                finally
                {
                    Native.DestroyIcon(handle);
                }
            }
        }
    }
}