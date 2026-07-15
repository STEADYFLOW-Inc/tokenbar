using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace ClaudeTokenMeter
{
    // Windows display-settings style monitor picker. Renders each screen as
    // a rounded rectangle scaled to fit the control, highlights the selected
    // one, marks the primary, and raises SelectionChanged on click.
    //
    // Shared by SettingsForm and SetupForm. C# 5.0 syntax only.
    internal sealed class MonitorPicker : Control
    {
        private Screen[] screens;
        private int selectedNumber; // 0 = primary
        // Parallel to screens: the last-computed on-screen rect for hit-testing.
        private Rectangle[] hitRects;

        public event EventHandler SelectionChanged;

        public MonitorPicker()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint |
                ControlStyles.ResizeRedraw,
                true);
            screens = Screen.AllScreens;
            hitRects = new Rectangle[screens.Length];
            selectedNumber = 0;
            Cursor = Cursors.Hand;
            BackColor = Color.FromArgb(32, 32, 32);
        }

        public int GetSelectedNumber()
        {
            return selectedNumber;
        }

        public void SetSelectedNumber(int n)
        {
            selectedNumber = n;
            Invalidate();
        }

        // True when the given screen is the currently selected one.
        private bool IsSelected(Screen s)
        {
            if (s == null)
            {
                return false;
            }
            if (s.Primary)
            {
                return selectedNumber <= 0;
            }
            return WidgetForm.GetDisplayNumber(s) == selectedNumber;
        }

        // Computes the union of all screen bounds (in virtual-desktop coords).
        private Rectangle ComputeUnion()
        {
            if (screens == null || screens.Length == 0)
            {
                return new Rectangle(0, 0, 1, 1);
            }
            Rectangle u = screens[0].Bounds;
            for (int i = 1; i < screens.Length; i++)
            {
                u = Rectangle.Union(u, screens[i].Bounds);
            }
            if (u.Width <= 0)
            {
                u.Width = 1;
            }
            if (u.Height <= 0)
            {
                u.Height = 1;
            }
            return u;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            using (SolidBrush back = new SolidBrush(Color.FromArgb(32, 32, 32)))
            {
                g.FillRectangle(back, 0, 0, Width, Height);
            }

            if (screens == null || screens.Length == 0)
            {
                return;
            }

            const int margin = 8;
            Rectangle union = ComputeUnion();
            float availW = Width - margin * 2f;
            float availH = Height - margin * 2f;
            if (availW < 1f) availW = 1f;
            if (availH < 1f) availH = 1f;

            // Scale-fit preserving aspect ratio.
            float scale = Math.Min(availW / union.Width, availH / union.Height);
            float drawnW = union.Width * scale;
            float drawnH = union.Height * scale;
            float originX = margin + (availW - drawnW) / 2f;
            float originY = margin + (availH - drawnH) / 2f;

            for (int i = 0; i < screens.Length; i++)
            {
                Screen s = screens[i];
                Rectangle b = s.Bounds;
                // Map virtual-desktop coords into the control, with a 1px inset
                // so adjacent screens read as separate tiles.
                float rx = originX + (b.X - union.X) * scale;
                float ry = originY + (b.Y - union.Y) * scale;
                float rw = b.Width * scale;
                float rh = b.Height * scale;
                Rectangle rect = Rectangle.Round(new RectangleF(rx + 1f, ry + 1f,
                    Math.Max(1f, rw - 2f), Math.Max(1f, rh - 2f)));
                hitRects[i] = rect;

                bool selected = IsSelected(s);
                Color fill = selected ? Color.FromArgb(38, 80, 70) : Color.FromArgb(45, 45, 45);
                Color border = selected ? Color.FromArgb(0, 184, 148) : Color.FromArgb(90, 90, 90);
                float borderW = selected ? 2f : 1f;

                using (GraphicsPath path = RoundRect(rect, 5))
                {
                    using (SolidBrush fb = new SolidBrush(fill))
                    {
                        g.FillPath(fb, path);
                    }
                    using (Pen bp = new Pen(border, borderW))
                    {
                        g.DrawPath(bp, path);
                    }
                }

                // Tiny taskbar strip along the bottom edge for affordance.
                int stripH = 4;
                if (rect.Height > stripH + 4)
                {
                    Rectangle strip = new Rectangle(
                        rect.X + 2, rect.Bottom - stripH - 1,
                        Math.Max(1, rect.Width - 4), stripH);
                    using (SolidBrush sb = new SolidBrush(Color.FromArgb(70, 70, 70)))
                    {
                        g.FillRectangle(sb, strip);
                    }
                }

                // Display number, centered, bold white.
                int displayNo = WidgetForm.GetDisplayNumber(s);
                string numText = displayNo.ToString(CultureInfo.InvariantCulture);
                using (Font numFont = new Font("Segoe UI", 12f, FontStyle.Bold))
                using (SolidBrush numBrush = new SolidBrush(Color.White))
                {
                    SizeF sz = g.MeasureString(numText, numFont);
                    float tx = rect.X + (rect.Width - sz.Width) / 2f;
                    float ty = rect.Y + (rect.Height - sz.Height) / 2f;
                    g.DrawString(numText, numFont, numBrush, tx, ty);
                }

                // Primary marker: a small star at the top-right corner.
                if (s.Primary && rect.Width > 16 && rect.Height > 16)
                {
                    using (Font starFont = new Font("Segoe UI", 8.5f))
                    using (SolidBrush starBrush = new SolidBrush(Color.FromArgb(160, 160, 160)))
                    {
                        g.DrawString("★", starFont, starBrush, rect.Right - 14, rect.Y + 1);
                    }
                }
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (screens == null)
            {
                return;
            }
            for (int i = 0; i < screens.Length; i++)
            {
                if (hitRects[i].Contains(e.Location))
                {
                    Screen s = screens[i];
                    selectedNumber = s.Primary ? 0 : WidgetForm.GetDisplayNumber(s);
                    if (SelectionChanged != null)
                    {
                        SelectionChanged(this, EventArgs.Empty);
                    }
                    Invalidate();
                    return;
                }
            }
        }

        private static GraphicsPath RoundRect(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            if (d <= 0 || rect.Width <= d || rect.Height <= d)
            {
                path.AddRectangle(rect);
                return path;
            }
            path.AddArc(rect.X, rect.Y, d, d, 180f, 90f);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270f, 90f);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0f, 90f);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90f, 90f);
            path.CloseFigure();
            return path;
        }
    }
}
