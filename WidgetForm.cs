using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace ClaudeTokenMeter
{
    public class WidgetForm : Form
    {
        // Logical layout units (multiplied by dpi scale s).
        private const int LogicalWidth = 240;
        private const int LogicalHeight = 40;

        // Content geometry (logical units, right of the icon slot).
        private const float ContentX = 31f;
        private const float ContentRightPad = 8f;
        private const float InnerPadTop = 4f;
        private const float InnerPadBottom = 4f;
        private const float TitleH = 13f;
        private const int MaxBars = 3;
        private const float MinRowH = 8f;

        // Win32 constants.
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x80;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_HIDEWINDOW = 0x0080;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        private readonly Config cfg;
        private readonly MeterAppContext owner;
        private readonly ToolTip tip;

        private UsageResult usage;
        private float scale = 1f;

        public WidgetForm(Config cfg, MeterAppContext owner)
        {
            this.cfg = cfg;
            this.owner = owner;
            this.usage = new UsageResult();
            this.tip = new ToolTip();

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint |
                ControlStyles.ResizeRedraw,
                true);

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            // Off-screen so it never flashes at (0,0) before placement.
            Location = new Point(-3000, -3000);
            Size = new Size(LogicalWidth, LogicalHeight);
            BackColor = Color.FromArgb(32, 32, 32);

            BuildContextMenu();

            Click += WidgetForm_Click;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && tip != null)
            {
                tip.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_NOACTIVATE;
                cp.ExStyle |= WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        private void WidgetForm_Click(object sender, EventArgs e)
        {
            MouseEventArgs me = e as MouseEventArgs;
            if (me != null && me.Button != MouseButtons.Left)
            {
                return;
            }
            if (owner != null)
            {
                owner.OpenSettings();
            }
        }

        // ---- Context menu -------------------------------------------------

        private void BuildContextMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();

            ToolStripMenuItem settingsItem = new ToolStripMenuItem(Strings.MenuSettings);
            settingsItem.Click += delegate { if (owner != null) owner.OpenSettings(); };

            ToolStripMenuItem refreshItem = new ToolStripMenuItem(Strings.MenuRefresh);
            refreshItem.Click += delegate { if (owner != null) owner.RefreshData(); };

            ToolStripMenuItem openItem = new ToolStripMenuItem(Strings.MenuOpenConfig);
            openItem.Click += delegate { if (owner != null) owner.OpenConfig(); };

            ToolStripMenuItem reloadItem = new ToolStripMenuItem(Strings.MenuReloadConfig);
            reloadItem.Click += delegate { if (owner != null) owner.ReloadConfig(); };

            ToolStripMenuItem startupItem = new ToolStripMenuItem(Strings.MenuStartup);
            startupItem.CheckOnClick = false;
            startupItem.Click += delegate
            {
                if (owner != null)
                {
                    owner.SetStartup(!owner.IsStartupEnabled());
                }
            };

            ToolStripMenuItem exitItem = new ToolStripMenuItem(Strings.MenuExit);
            exitItem.Click += delegate { if (owner != null) owner.ExitApp(); };

            menu.Opening += delegate
            {
                if (owner != null)
                {
                    startupItem.Checked = owner.IsStartupEnabled();
                }
            };

            menu.Items.Add(settingsItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(refreshItem);
            menu.Items.Add(openItem);
            menu.Items.Add(reloadItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(startupItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exitItem);

            ContextMenuStrip = menu;
        }

        // ---- Data / tooltip ----------------------------------------------

        public void SetUsage(UsageResult u)
        {
            usage = u != null ? u : new UsageResult();
            tip.SetToolTip(this, BuildTooltip(usage));
            Invalidate();
        }

        private string BuildTooltip(UsageResult u)
        {
            if (u == null)
            {
                return Strings.TipNoData;
            }
            if (u.Error != null)
            {
                return u.Error;
            }

            string updatedLine = "\r\n" + string.Format(
                CultureInfo.InvariantCulture,
                Strings.TipUpdatedFmt,
                DateTime.Now.ToString("H:mm:ss"));

            if (u.FromApi)
            {
                StringBuilder api = new StringBuilder();

                if (u.SessionResetUtc.HasValue)
                {
                    api.Append(string.Format(
                        CultureInfo.InvariantCulture,
                        Strings.TipSessionFmt,
                        (int)Math.Round(u.SessionPct),
                        u.SessionResetUtc.Value.ToLocalTime().ToString("H:mm")));
                }
                else
                {
                    api.Append(string.Format(
                        CultureInfo.InvariantCulture,
                        Strings.TipSessionNoResetFmt,
                        (int)Math.Round(u.SessionPct)));
                }

                api.Append("\r\n");
                if (u.WeeklyResetUtc.HasValue)
                {
                    api.Append(string.Format(
                        CultureInfo.InvariantCulture,
                        Strings.TipWeeklyFmt,
                        (int)Math.Round(u.WeeklyPct),
                        u.WeeklyResetUtc.Value.ToLocalTime().ToString("M/d H:mm")));
                }
                else
                {
                    api.Append(string.Format(
                        CultureInfo.InvariantCulture,
                        Strings.TipWeeklyNoResetFmt,
                        (int)Math.Round(u.WeeklyPct)));
                }

                if (u.HasWeeklyScoped)
                {
                    api.Append("\r\n");
                    api.Append(string.Format(
                        CultureInfo.InvariantCulture,
                        Strings.TipWeeklyScopedFmt,
                        u.WeeklyScopedModel,
                        (int)Math.Round(u.WeeklyScopedPct)));
                }

                api.Append("\r\n");
                api.Append(Strings.TipSourceApi);
                if (u.Stale)
                {
                    api.Append("\r\n");
                    api.Append(Strings.TipStale);
                }
                api.Append(updatedLine);
                return api.ToString();
            }

            if (!u.Active)
            {
                return Strings.TipNoActiveBlock;
            }

            StringBuilder sb = new StringBuilder();
            sb.Append(string.Format(
                CultureInfo.InvariantCulture,
                Strings.TipUsedFmt,
                u.UsedTokens.ToString("N0", CultureInfo.InvariantCulture)));

            if (u.BlockStartUtc.HasValue)
            {
                sb.Append("\r\n");
                sb.Append(string.Format(
                    CultureInfo.InvariantCulture,
                    Strings.TipBlockStartFmt,
                    u.BlockStartUtc.Value.ToLocalTime().ToString("HH:mm")));
            }
            if (u.ResetAtUtc.HasValue)
            {
                sb.Append("\r\n");
                sb.Append(string.Format(
                    CultureInfo.InvariantCulture,
                    Strings.TipResetFmt,
                    u.ResetAtUtc.Value.ToLocalTime().ToString("HH:mm")));
            }

            sb.Append("\r\n");
            sb.Append(Strings.TipSourceLocal);
            sb.Append(updatedLine);

            return sb.ToString();
        }

        // ---- Row model ----------------------------------------------------

        // A single bar row: label + fill fraction (remaining 0-1) + optional
        // percent text. ShowResetInline/ResetText drive the single-bar reset
        // annotation only.
        private sealed class BarRow
        {
            public string Label;
            public double Fraction;   // remaining fraction 0-1
            public string PctText;    // e.g. "42%"
            public bool ShowReset;    // single-bar API reset annotation
            public string ResetText;
        }

        // Builds the ordered bar list from cfg + usage, capped at MaxBars.
        // Falls back to a synthesized session row if nothing else is visible.
        private List<BarRow> BuildRows()
        {
            List<BarRow> rows = new List<BarRow>();
            bool fromApi = usage != null && usage.FromApi;

            if (cfg.showSessionBar)
            {
                rows.Add(BuildSessionRow(fromApi));
            }

            if (cfg.showWeeklyBar && fromApi)
            {
                rows.Add(BuildWeeklyRow());
            }

            if (cfg.showModelBars && fromApi && usage.ScopedLimits != null)
            {
                foreach (ScopedLimit sl in usage.ScopedLimits)
                {
                    if (rows.Count >= MaxBars)
                    {
                        break;
                    }
                    rows.Add(BuildModelRow(sl));
                }
            }

            if (rows.Count > MaxBars)
            {
                rows.RemoveRange(MaxBars, rows.Count - MaxBars);
            }

            if (rows.Count == 0)
            {
                rows.Add(BuildSessionRow(fromApi));
            }

            return rows;
        }

        private BarRow BuildSessionRow(bool fromApi)
        {
            BarRow row = new BarRow();
            row.Label = Strings.BarLabelSession;

            if (fromApi)
            {
                double remainingPct = 100.0 - usage.SessionPct;
                row.Fraction = Clamp01(remainingPct / 100.0);
                int remainingInt = (int)Math.Round(remainingPct);
                row.PctText = remainingInt.ToString(CultureInfo.InvariantCulture) + "%";
                if (usage.SessionResetUtc.HasValue)
                {
                    row.ShowReset = true;
                    row.ResetText = string.Format(
                        CultureInfo.InvariantCulture,
                        Strings.ResetFmt,
                        usage.SessionResetUtc.Value.ToLocalTime().ToString("H:mm"));
                }
            }
            else
            {
                long limit = cfg.tokenLimit;
                long used = (usage != null && usage.Active) ? usage.UsedTokens : 0L;
                long remaining = Math.Max(0L, limit - used);
                double fraction = limit > 0 ? (double)remaining / limit : 0.0;
                row.Fraction = Clamp01(fraction);
                int percent = (int)Math.Round(row.Fraction * 100.0);
                row.PctText = percent.ToString(CultureInfo.InvariantCulture) + "%";
            }

            return row;
        }

        private BarRow BuildWeeklyRow()
        {
            BarRow row = new BarRow();
            row.Label = Strings.BarLabelWeekly;
            double remainingPct = 100.0 - usage.WeeklyPct;
            row.Fraction = Clamp01(remainingPct / 100.0);
            int remainingInt = (int)Math.Round(remainingPct);
            row.PctText = remainingInt.ToString(CultureInfo.InvariantCulture) + "%";
            return row;
        }

        private BarRow BuildModelRow(ScopedLimit sl)
        {
            BarRow row = new BarRow();
            string model = (sl != null && sl.Model != null) ? sl.Model : Strings.ScopedModelFallback;
            row.Label = model.Length > 5 ? model.Substring(0, 5) : model;
            double pct = sl != null ? sl.Pct : 0.0;
            double remainingPct = 100.0 - pct;
            row.Fraction = Clamp01(remainingPct / 100.0);
            int remainingInt = (int)Math.Round(remainingPct);
            row.PctText = remainingInt.ToString(CultureInfo.InvariantCulture) + "%";
            return row;
        }

        private static double Clamp01(double v)
        {
            if (v < 0.0) return 0.0;
            if (v > 1.0) return 1.0;
            return v;
        }

        // ---- Painting -----------------------------------------------------

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            float s = scale > 0f ? scale : 1f;
            int w = ClientSize.Width;
            int h = ClientSize.Height;

            // Taskbar-matching background fill.
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(32, 32, 32)))
            {
                g.FillRectangle(bg, 0, 0, w, h);
            }

            // Rounded card covering the client minus 1px.
            Rectangle card = new Rectangle(0, 0, Math.Max(1, w - 1), Math.Max(1, h - 1));
            float radius = 6f * s;
            using (GraphicsPath cardPath = RoundedRect(card, radius))
            {
                using (SolidBrush cardBrush = new SolidBrush(Color.FromArgb(45, 45, 45)))
                {
                    g.FillPath(cardBrush, cardPath);
                }
                using (Pen cardBorder = new Pen(Color.FromArgb(70, 70, 70), 1f))
                {
                    g.DrawPath(cardBorder, cardPath);
                }
            }

            DrawLogoIcon(g, s, h);

            bool titleVisible = cfg.showTitle;
            if (titleVisible)
            {
                DrawTitle(g, s);
            }

            DrawContent(g, s, h, titleVisible);
        }

        // Official Claude logo, embedded as a manifest resource ("claude_logo.png"
        // via csc /res). Loaded once lazily; the stream is copied into a fresh
        // bitmap so the source stream can be disposed safely (GDI+ otherwise
        // requires the stream to stay open for the Bitmap's lifetime).
        private static Bitmap logoBitmap;
        private static bool logoLoadAttempted;

        private static Bitmap GetLogoBitmap()
        {
            if (!logoLoadAttempted)
            {
                logoLoadAttempted = true;
                try
                {
                    Stream resource = Assembly.GetExecutingAssembly()
                        .GetManifestResourceStream("claude_logo.png");
                    if (resource != null)
                    {
                        using (resource)
                        using (Bitmap streamBound = new Bitmap(resource))
                        {
                            logoBitmap = new Bitmap(streamBound);
                        }
                    }
                }
                catch (Exception)
                {
                    logoBitmap = null;
                }
            }
            return logoBitmap;
        }

        // Draws the logo in the icon slot as a rounded-corner square chip.
        private void DrawLogoIcon(Graphics g, float s, int h)
        {
            Bitmap logo = GetLogoBitmap();
            if (logo == null)
            {
                return;
            }

            float size = 18f * s;
            float cx = 17f * s;
            float cy = h / 2f;
            RectangleF rect = new RectangleF(cx - size / 2f, cy - size / 2f, size, size);

            InterpolationMode oldMode = g.InterpolationMode;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            using (GraphicsPath chip = RoundedRect(rect, 4f * s))
            {
                g.SetClip(chip);
                g.DrawImage(logo, rect);
                g.ResetClip();
            }
            g.InterpolationMode = oldMode;
        }

        private void DrawTitle(Graphics g, float s)
        {
            using (Font titleFont = new Font("Segoe UI", 10.5f * s, GraphicsUnit.Pixel))
            using (SolidBrush titleBrush = new SolidBrush(Color.FromArgb(200, 200, 200)))
            {
                g.DrawString(Strings.Title, titleFont, titleBrush, ContentX * s, 5f * s);
            }
        }

        // Dispatches error / single-bar / multi-bar rendering.
        private void DrawContent(Graphics g, float s, int h, bool titleVisible)
        {
            if (usage != null && usage.Error != null)
            {
                DrawError(g, s, h, titleVisible);
                return;
            }

            List<BarRow> rows = BuildRows();

            // Bars area in logical units.
            float innerH = (h / s) - InnerPadTop - InnerPadBottom;
            float barsAreaH = innerH - (titleVisible ? TitleH : 0f);
            float barsTop = InnerPadTop + (titleVisible ? TitleH : 0f);

            int barCount = rows.Count;
            // Cap count further if rows would be too short.
            while (barCount > 1 && (barsAreaH / barCount) < MinRowH)
            {
                barCount--;
            }
            if (barCount < rows.Count)
            {
                rows.RemoveRange(barCount, rows.Count - barCount);
            }

            if (rows.Count == 1)
            {
                DrawSingleBar(g, s, rows[0], titleVisible, barsTop, barsAreaH);
            }
            else
            {
                DrawMultiBar(g, s, rows, barsTop, barsAreaH);
            }
        }

        private void DrawError(Graphics g, float s, int h, bool titleVisible)
        {
            float y = titleVisible ? 18.5f * s : (h - 9f * s) / 2f;
            using (Font errFont = new Font("Segoe UI", 9f * s, GraphicsUnit.Pixel))
            using (SolidBrush errBrush = new SolidBrush(Color.FromArgb(160, 160, 160)))
            {
                g.DrawString(usage.Error, errFont, errBrush, ContentX * s, y);
            }
        }

        // The classic single-bar look: bar left, value text right. Preserves the
        // near-current layout when a title is shown; vertically centers otherwise.
        private void DrawSingleBar(Graphics g, float s, BarRow row, bool titleVisible,
            float barsTop, float barsAreaH)
        {
            float contentW = cfg.widgetWidth - ContentX - ContentRightPad;
            float barX = ContentX;
            float barH = titleVisible ? 7f : 8f;
            float barY;
            if (titleVisible)
            {
                barY = 22f;
            }
            else
            {
                barY = barsTop + (barsAreaH - barH) / 2f;
            }

            if (cfg.showValueText)
            {
                // Bar occupies the classic left portion; value text to its right.
                float barW = 82f;
                DrawBar(g, barX * s, barY * s, barW * s, barH * s, row.Fraction);
                DrawSingleValueText(g, s, row, (barX + barW + 6f));
            }
            else
            {
                // No value text: stretch bar across the full content width.
                // Optionally reserve room for a compact reset annotation (API mode).
                bool drawReset = cfg.showResetTime && row.ShowReset && row.ResetText != null;
                float barW = contentW;
                if (drawReset)
                {
                    barW -= 46f;
                }
                DrawBar(g, barX * s, barY * s, barW * s, barH * s, row.Fraction);

                if (drawReset)
                {
                    float resetX = barX + barW + 6f;
                    DrawResetSmall(g, s, row.ResetText, resetX, barY - 3f);
                }
            }
        }

        // Big value text (+ optional reset time) for the single-bar layout.
        private void DrawSingleValueText(Graphics g, float s, BarRow row, float valueXLogical)
        {
            float valueX = valueXLogical * s;
            float valueY = 18.5f * s;

            using (Font valueFont = new Font("Segoe UI", 12f * s, FontStyle.Bold, GraphicsUnit.Pixel))
            using (SolidBrush valueBrush = new SolidBrush(Color.FromArgb(255, 255, 255)))
            {
                g.DrawString(row.PctText, valueFont, valueBrush, valueX, valueY);

                if (cfg.showResetTime && row.ShowReset && row.ResetText != null)
                {
                    SizeF valueSize = g.MeasureString(row.PctText, valueFont);
                    float resetX = valueX + valueSize.Width + 6f * s;
                    DrawResetSmall(g, s, row.ResetText, resetX / s, 18.5f);
                }
            }
        }

        private void DrawResetSmall(Graphics g, float s, string text, float xLogical, float yLogical)
        {
            using (Font resetFont = new Font("Segoe UI", 9.5f * s, GraphicsUnit.Pixel))
            using (SolidBrush resetBrush = new SolidBrush(Color.FromArgb(160, 160, 160)))
            {
                g.DrawString(text, resetFont, resetBrush, xLogical * s, yLogical * s);
            }
        }

        // 2-3 rows: [label][bar][pct]. Rows distributed evenly in barsAreaH.
        // Reset time is intentionally omitted per-row (tooltip carries it).
        private void DrawMultiBar(Graphics g, float s, List<BarRow> rows, float barsTop, float barsAreaH)
        {
            int count = rows.Count;
            float rowH = barsAreaH / count;

            float labelW = 26f;
            float pctW = cfg.showValueText ? 30f : 0f;
            float gap = 4f;

            float contentW = cfg.widgetWidth - ContentX - ContentRightPad;
            float barX = ContentX + labelW + gap;
            float barW = contentW - labelW - gap - (cfg.showValueText ? (gap + pctW) : 0f);
            if (barW < 10f)
            {
                barW = 10f;
            }
            float barH = Math.Min(7f, rowH - 3f);
            if (barH < 3f)
            {
                barH = 3f;
            }

            for (int i = 0; i < count; i++)
            {
                BarRow row = rows[i];
                float rowTop = barsTop + rowH * i;
                float rowMid = rowTop + rowH / 2f;
                float barY = rowMid - barH / 2f;

                DrawMultiLabel(g, s, row.Label, ContentX, rowMid);
                DrawBar(g, barX * s, barY * s, barW * s, barH * s, row.Fraction);

                if (cfg.showValueText)
                {
                    DrawMultiPct(g, s, row.PctText, barX + barW + gap, pctW, rowMid);
                }
            }
        }

        private void DrawMultiLabel(Graphics g, float s, string label, float xLogical, float rowMidLogical)
        {
            using (Font labelFont = new Font("Segoe UI", 9f * s, GraphicsUnit.Pixel))
            using (SolidBrush labelBrush = new SolidBrush(Color.FromArgb(160, 160, 160)))
            {
                SizeF size = g.MeasureString(label, labelFont);
                float y = rowMidLogical * s - size.Height / 2f;
                g.DrawString(label, labelFont, labelBrush, xLogical * s, y);
            }
        }

        private void DrawMultiPct(Graphics g, float s, string pctText, float xLogical, float widthLogical, float rowMidLogical)
        {
            using (Font pctFont = new Font("Segoe UI", 9.5f * s, FontStyle.Bold, GraphicsUnit.Pixel))
            using (SolidBrush pctBrush = new SolidBrush(Color.FromArgb(240, 240, 240)))
            {
                SizeF size = g.MeasureString(pctText, pctFont);
                float y = rowMidLogical * s - size.Height / 2f;
                float rightEdge = (xLogical + widthLogical) * s;
                float x = rightEdge - size.Width;
                g.DrawString(pctText, pctFont, pctBrush, x, y);
            }
        }

        // Draws a rounded progress bar at the given pixel rect. Preserves the
        // green gradient / orange-red under-20% behavior of the original.
        private void DrawBar(Graphics g, float x, float y, float w, float h, double fraction)
        {
            float r = h / 2f;

            RectangleF track = new RectangleF(x, y, w, h);
            using (GraphicsPath trackPath = RoundedRect(track, r))
            using (SolidBrush trackBrush = new SolidBrush(Color.FromArgb(74, 74, 74)))
            {
                g.FillPath(trackBrush, trackPath);
            }

            float fillW = (float)(w * fraction);
            if (fillW < 1f)
            {
                return;
            }

            RectangleF fillRect = new RectangleF(x, y, fillW, h);
            Color c1;
            Color c2;
            if (fraction < 0.2)
            {
                c1 = Color.FromArgb(230, 126, 34);
                c2 = Color.FromArgb(231, 76, 60);
            }
            else
            {
                c1 = Color.FromArgb(0, 184, 148);
                c2 = Color.FromArgb(120, 224, 143);
            }

            using (GraphicsPath fillPath = RoundedRect(fillRect, Math.Min(r, fillW / 2f)))
            using (LinearGradientBrush grad = new LinearGradientBrush(
                new RectangleF(x, y, w, h), c1, c2, LinearGradientMode.Horizontal))
            {
                g.FillPath(grad, fillPath);
            }
        }

        private static GraphicsPath RoundedRect(Rectangle rect, float radius)
        {
            return RoundedRect(new RectangleF(rect.X, rect.Y, rect.Width, rect.Height), radius);
        }

        private static GraphicsPath RoundedRect(RectangleF rect, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float d = radius * 2f;
            if (d <= 0f || rect.Width <= d || rect.Height <= d)
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

        // ---- Taskbar placement --------------------------------------------

        public void UpdatePlacement()
        {
            try
            {
                if (ShouldHideForFullscreen())
                {
                    SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0,
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_HIDEWINDOW);
                    return;
                }

                IntPtr tray = FindWindow("Shell_TrayWnd", null);
                if (tray != IntPtr.Zero)
                {
                    PlaceOverTaskbar(tray);
                }
                else
                {
                    PlaceFallback();
                }
            }
            catch
            {
            }
            Invalidate();
        }

        private bool ShouldHideForFullscreen()
        {
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero || fg == Handle)
            {
                return false;
            }

            StringBuilder cls = new StringBuilder(256);
            int len = GetClassName(fg, cls, cls.Capacity);
            if (len <= 0)
            {
                return false;
            }
            string className = cls.ToString();
            if (className == "Progman" ||
                className == "WorkerW" ||
                className == "Shell_TrayWnd" ||
                className == "XamlExplorerHostIslandWindow")
            {
                return false;
            }

            RECT fr;
            if (!GetWindowRect(fg, out fr))
            {
                return false;
            }

            Rectangle bounds = Screen.PrimaryScreen.Bounds;
            return fr.left <= 0 && fr.top <= 0 &&
                fr.right >= bounds.Width && fr.bottom >= bounds.Height;
        }

        private float ResolveScale(IntPtr hwnd)
        {
            try
            {
                uint dpi = GetDpiForWindow(hwnd);
                if (dpi > 0)
                {
                    return dpi / 96f;
                }
            }
            catch (EntryPointNotFoundException)
            {
            }
            catch
            {
            }
            using (Graphics g = CreateGraphics())
            {
                return g.DpiX / 96f;
            }
        }

        private void PlaceOverTaskbar(IntPtr tray)
        {
            float sc = ResolveScale(tray);
            scale = sc;

            RECT trayRect;
            if (!GetWindowRect(tray, out trayRect))
            {
                PlaceFallback();
                return;
            }
            int trayH = trayRect.bottom - trayRect.top;

            int h = Math.Min((int)(40 * sc), trayH - (int)(4 * sc));
            if (h < 20)
            {
                h = trayH;
            }
            int w = (int)(cfg.widgetWidth * sc);

            // All coordinates below are SCREEN coordinates.
            int x;
            if (cfg.position == "left")
            {
                x = trayRect.left + (int)(8 * sc);
            }
            else
            {
                int rightEdge;
                IntPtr notify = FindWindowEx(tray, IntPtr.Zero, "TrayNotifyWnd", null);
                RECT nr;
                if (notify != IntPtr.Zero && GetWindowRect(notify, out nr))
                {
                    rightEdge = nr.left;
                }
                else
                {
                    rightEdge = trayRect.right - (int)(180 * sc);
                }

                x = rightEdge - w - (int)(8 * sc);
            }

            x += (int)(cfg.offsetX * sc);
            int y = trayRect.top + (trayH - h) / 2;

            // Reassert topmost on every call so we stay above the taskbar.
            SetWindowPos(Handle, HWND_TOPMOST, x, y, w, h,
                SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        private void PlaceFallback()
        {
            float sc;
            using (Graphics g = CreateGraphics())
            {
                sc = g.DpiX / 96f;
            }
            scale = sc;

            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            Rectangle sb = Screen.PrimaryScreen.Bounds;
            int w = (int)(cfg.widgetWidth * sc);
            int h = (int)(40 * sc);

            int y;
            if (sb.Bottom > wa.Bottom)
            {
                y = wa.Bottom + (sb.Bottom - wa.Bottom - h) / 2;
            }
            else
            {
                y = sb.Bottom - h - (int)(8 * sc);
            }
            int x = sb.Right - w - (int)(220 * sc);

            SetWindowPos(Handle, HWND_TOPMOST, x, y, w, h,
                SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        // ---- P/Invoke ------------------------------------------------------

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
    }
}
