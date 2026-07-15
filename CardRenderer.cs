using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace ClaudeTokenMeter
{
    // Stateless renderer for the widget "card". All painting that used to live
    // in WidgetForm.OnPaint (and its helpers) moved here so both the live
    // widget and the settings-window preview can share identical drawing.
    //
    // Everything that was keyed off WidgetForm instance fields (cfg, usage,
    // scale, hovered) is now a parameter. The logical layout is authored in
    // logical units and multiplied by `scale` exactly as before; cfg.widgetWidth
    // still drives the horizontal layout, while widthPx/heightPx are the actual
    // paint bounds (client size for the widget, panel size for the preview).
    //
    // C# 5.0 syntax only.
    internal static class CardRenderer
    {
        // Content geometry (logical units, right of the icon slot).
        private const float ContentX = 31f;
        private const float ContentRightPad = 8f;
        private const float InnerPadTop = 4f;
        private const float InnerPadBottom = 4f;
        private const float TitleH = 13f;
        private const int MaxBars = 3;
        private const float MinRowH = 8f;

        // ---- Public entry point -------------------------------------------

        // Paints the full card into g. Mirrors the old WidgetForm.OnPaint body.
        // The caller is responsible for setting SmoothingMode / TextRenderingHint
        // (WidgetForm and the preview both do this before calling Draw).
        public static void Draw(Graphics g, Config cfg, UsageResult usage, float scale,
            int widthPx, int heightPx, bool hovered)
        {
            float s = scale > 0f ? scale : 1f;
            int w = widthPx;
            int h = heightPx;

            // Taskbar-matching background fill.
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(32, 32, 32)))
            {
                g.FillRectangle(bg, 0, 0, w, h);
            }

            // Rounded card covering the client minus 1px.
            Rectangle card = new Rectangle(0, 0, Math.Max(1, w - 1), Math.Max(1, h - 1));
            float radius = 6f * s;
            Color cardFill = hovered ? Color.FromArgb(52, 52, 52) : Color.FromArgb(45, 45, 45);
            Color cardBorderColor = hovered ? Color.FromArgb(96, 96, 96) : Color.FromArgb(70, 70, 70);
            using (GraphicsPath cardPath = RoundedRect(card, radius))
            {
                using (SolidBrush cardBrush = new SolidBrush(cardFill))
                {
                    g.FillPath(cardBrush, cardPath);
                }
                using (Pen cardBorder = new Pen(cardBorderColor, 1f))
                {
                    g.DrawPath(cardBorder, cardPath);
                }
                DrawSourceDot(g, cfg, usage, s);
            }

            DrawLogoIcon(g, s, h);

            bool titleVisible = cfg.showTitle;
            if (titleVisible)
            {
                DrawTitle(g, s);
            }

            DrawContent(g, cfg, usage, s, h, titleVisible);
        }

        // ---- Logo chip ----------------------------------------------------

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
        private static void DrawLogoIcon(Graphics g, float s, int h)
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

        private static void DrawTitle(Graphics g, float s)
        {
            using (Font titleFont = new Font("Segoe UI", 10.5f * s, GraphicsUnit.Pixel))
            using (SolidBrush titleBrush = new SolidBrush(Color.FromArgb(200, 200, 200)))
            {
                g.DrawString(Strings.Title, titleFont, titleBrush, ContentX * s, 5f * s);
            }
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
            public int PctValue;      // remaining percent as an integer
            public bool ShowReset;    // single-bar API reset annotation
            public string ResetText;
            public bool NoData;       // no API data at all -> render "—"
        }

        // Builds the ordered bar list from cfg + usage, capped at MaxBars.
        // Falls back to a synthesized session row if nothing else is visible.
        private static List<BarRow> BuildRows(Config cfg, UsageResult usage)
        {
            List<BarRow> rows = new List<BarRow>();
            bool fromApi = usage != null && usage.FromApi;

            if (cfg.showSessionBar)
            {
                rows.Add(BuildSessionRow(cfg, usage, fromApi));
            }

            if (cfg.showWeeklyBar && fromApi)
            {
                rows.Add(BuildWeeklyRow(usage));
            }

            if (cfg.showModelBars && fromApi && usage.ScopedLimits != null)
            {
                bool filterActive = cfg.selectedModels != null && cfg.selectedModels.Length > 0;
                foreach (ScopedLimit sl in usage.ScopedLimits)
                {
                    if (rows.Count >= MaxBars)
                    {
                        break;
                    }
                    if (filterActive)
                    {
                        string slModel = (sl != null) ? sl.Model : null;
                        bool matched = false;
                        foreach (string sel in cfg.selectedModels)
                        {
                            if (string.Equals(slModel, sel, StringComparison.OrdinalIgnoreCase))
                            {
                                matched = true;
                                break;
                            }
                        }
                        if (!matched)
                        {
                            continue;
                        }
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
                rows.Add(BuildSessionRow(cfg, usage, fromApi));
            }

            return rows;
        }

        private static BarRow BuildSessionRow(Config cfg, UsageResult usage, bool fromApi)
        {
            BarRow row = new BarRow();
            row.Label = Strings.BarLabelSession;

            if (fromApi)
            {
                double remainingPct = 100.0 - usage.SessionPct;
                row.Fraction = Clamp01(remainingPct / 100.0);
                int remainingInt = (int)Math.Round(remainingPct);
                row.PctText = remainingInt.ToString(CultureInfo.InvariantCulture) + "%";
                row.PctValue = remainingInt;
                if (usage.Stale)
                {
                    // Stale cache: replace the reset time with the cached-at time.
                    row.ShowReset = true;
                    row.ResetText = string.Format(
                        CultureInfo.InvariantCulture,
                        Strings.CachedAtFmt,
                        usage.FetchedAtUtc.ToLocalTime().ToString("H:mm"));
                }
                else if (usage.SessionResetUtc.HasValue)
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
                // No API data has ever been cached: show a no-data placeholder.
                row.NoData = true;
                row.Fraction = 0.0;
                row.PctText = "—";
                row.PctValue = 0;
            }

            return row;
        }

        private static BarRow BuildWeeklyRow(UsageResult usage)
        {
            BarRow row = new BarRow();
            row.Label = Strings.BarLabelWeekly;
            double remainingPct = 100.0 - usage.WeeklyPct;
            row.Fraction = Clamp01(remainingPct / 100.0);
            int remainingInt = (int)Math.Round(remainingPct);
            row.PctText = remainingInt.ToString(CultureInfo.InvariantCulture) + "%";
            return row;
        }

        private static BarRow BuildModelRow(ScopedLimit sl)
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

        // ---- Content dispatch ---------------------------------------------

        // Dispatches error / single-bar / multi-bar rendering.
        private static void DrawContent(Graphics g, Config cfg, UsageResult usage, float s, int h, bool titleVisible)
        {
            if (usage != null && usage.Error != null)
            {
                DrawError(g, usage, s, h, titleVisible);
                return;
            }

            List<BarRow> rows = BuildRows(cfg, usage);

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
                DrawSingleBar(g, cfg, usage, s, rows[0], titleVisible, barsTop, barsAreaH);
            }
            else
            {
                DrawMultiBar(g, cfg, s, rows, barsTop, barsAreaH);
            }
        }

        private static void DrawError(Graphics g, UsageResult usage, float s, int h, bool titleVisible)
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
        private static void DrawSingleBar(Graphics g, Config cfg, UsageResult usage, float s, BarRow row,
            bool titleVisible, float barsTop, float barsAreaH)
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
                DrawSingleValueText(g, cfg, usage, s, row, (barX + barW + 6f));
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
                    DrawResetSmall(g, s, row.ResetText, resetX, barY - 3f, ResetTextColor(usage, row));
                }
            }
        }

        // Big value text (+ optional reset time) for the single-bar layout.
        private static void DrawSingleValueText(Graphics g, Config cfg, UsageResult usage, float s, BarRow row, float valueXLogical)
        {
            float valueX = valueXLogical * s;
            float valueY = 18.5f * s;

            // No API data: render a plain "—" in gray instead of "残り —%".
            if (row.NoData)
            {
                using (Font naFont = new Font("Segoe UI", 12f * s, FontStyle.Bold, GraphicsUnit.Pixel))
                using (SolidBrush naBrush = new SolidBrush(Color.FromArgb(160, 160, 160)))
                {
                    g.DrawString("—", naFont, naBrush, valueX, valueY);
                }
                return;
            }

            string valueText = string.Format(
                CultureInfo.InvariantCulture, Strings.RemainingFmt, row.PctValue);
            using (Font valueFont = new Font("Segoe UI", 12f * s, FontStyle.Bold, GraphicsUnit.Pixel))
            using (SolidBrush valueBrush = new SolidBrush(Color.FromArgb(255, 255, 255)))
            {
                g.DrawString(valueText, valueFont, valueBrush, valueX, valueY);

                if (cfg.showResetTime && row.ShowReset && row.ResetText != null)
                {
                    // Right-align the annotation to the card's inner edge; skip
                    // it entirely when it would collide with the value text.
                    // GenericTypographic avoids MeasureString's ~15% padding.
                    StringFormat tight = StringFormat.GenericTypographic;
                    SizeF valueSize = g.MeasureString(valueText, valueFont, int.MaxValue, tight);
                    float valueEnd = valueX + valueSize.Width;
                    using (Font resetFont = new Font("Segoe UI", 9.5f * s, GraphicsUnit.Pixel))
                    {
                        SizeF resetSize = g.MeasureString(row.ResetText, resetFont, int.MaxValue, tight);
                        float rightEdge = (cfg.widgetWidth - ContentRightPad) * s;
                        float resetX = rightEdge - resetSize.Width - 2f * s;
                        if (resetX >= valueEnd + 4f * s)
                        {
                            DrawResetSmall(g, s, row.ResetText, resetX / s, 18.5f, ResetTextColor(usage, row));
                        }
                    }
                }
            }
        }

        // Amber-ish gray for a stale "as of HH:mm" label; plain gray otherwise.
        private static Color ResetTextColor(UsageResult usage, BarRow row)
        {
            bool stale = usage != null && usage.FromApi && usage.Stale;
            if (stale)
            {
                return Color.FromArgb(198, 168, 120);
            }
            return Color.FromArgb(160, 160, 160);
        }

        private static void DrawResetSmall(Graphics g, float s, string text, float xLogical, float yLogical, Color color)
        {
            using (Font resetFont = new Font("Segoe UI", 9.5f * s, GraphicsUnit.Pixel))
            using (SolidBrush resetBrush = new SolidBrush(color))
            {
                g.DrawString(text, resetFont, resetBrush, xLogical * s, yLogical * s);
            }
        }

        // 2-3 rows: [label][bar][pct]. Rows distributed evenly in barsAreaH.
        // Reset time is intentionally omitted per-row (tooltip carries it).
        private static void DrawMultiBar(Graphics g, Config cfg, float s, List<BarRow> rows, float barsTop, float barsAreaH)
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

        private static void DrawMultiLabel(Graphics g, float s, string label, float xLogical, float rowMidLogical)
        {
            using (Font labelFont = new Font("Segoe UI", 9f * s, GraphicsUnit.Pixel))
            using (SolidBrush labelBrush = new SolidBrush(Color.FromArgb(160, 160, 160)))
            {
                SizeF size = g.MeasureString(label, labelFont);
                float y = rowMidLogical * s - size.Height / 2f;
                g.DrawString(label, labelFont, labelBrush, xLogical * s, y);
            }
        }

        private static void DrawMultiPct(Graphics g, float s, string pctText, float xLogical, float widthLogical, float rowMidLogical)
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
        private static void DrawBar(Graphics g, float x, float y, float w, float h, double fraction)
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

        // Draws a small filled circle in the top-right interior of the card indicating the data source.
        private static void DrawSourceDot(Graphics g, Config cfg, UsageResult usage, float s)
        {
            Color dotColor;
            if (usage != null && usage.Error != null)
            {
                dotColor = Color.FromArgb(229, 83, 75);
            }
            else if (usage != null && usage.FromApi && !usage.Stale)
            {
                dotColor = Color.FromArgb(63, 185, 80);
            }
            else if (usage != null && usage.FromApi && usage.Stale)
            {
                dotColor = Color.FromArgb(210, 153, 34);
            }
            else
            {
                dotColor = Color.FromArgb(139, 148, 158);
            }

            float diameter = 5f * s;
            float dotX = (cfg.widgetWidth - 12f) * s - diameter / 2f;
            float dotY = 7f * s - diameter / 2f;
            using (SolidBrush dotBrush = new SolidBrush(dotColor))
            {
                g.FillEllipse(dotBrush, dotX, dotY, diameter, diameter);
            }
        }

        // ---- Shared geometry ----------------------------------------------

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
    }
}
