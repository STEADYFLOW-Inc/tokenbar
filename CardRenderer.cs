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
        private const int MaxBars = 6;
        private const float MinRowH = 8f;
        private const float ColumnGap = 8f;

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

            Theme theme = ThemeManager.Current;

            // Taskbar-matching background fill.
            using (SolidBrush bg = new SolidBrush(theme.Background))
            {
                g.FillRectangle(bg, 0, 0, w, h);
            }

            // Rounded card covering the client minus 1px.
            Rectangle card = new Rectangle(0, 0, Math.Max(1, w - 1), Math.Max(1, h - 1));
            float radius = 6f * s;
            Color cardFill = hovered ? theme.CardFillHover : theme.CardFill;
            Color cardBorderColor = hovered ? theme.CardBorderHover : theme.CardBorder;
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
                DrawTitleAnnotation(g, cfg, usage, s);
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
            using (SolidBrush titleBrush = new SolidBrush(ThemeManager.Current.TitleText))
            {
                g.DrawString(Strings.Title, titleFont, titleBrush, ContentX * s, 5f * s);
            }
        }

        // Draws the reset-time (or cached-at) annotation on the title row,
        // right-aligned in the free space to the right of the title text.
        // Used by ALL layouts (single bar, multi, two-column) whenever the
        // title is visible, so the reset time is shown consistently.
        private static void DrawTitleAnnotation(Graphics g, Config cfg, UsageResult usage, float s)
        {
            if (!cfg.showResetTime)
            {
                return;
            }
            if (usage == null)
            {
                return;
            }

            // Clean mode: no API data, annotate from the local block reset time.
            if (!usage.FromApi)
            {
                if (cfg.useApi || !usage.Active || !usage.ResetAtUtc.HasValue)
                {
                    return;
                }
                DrawCleanTitleAnnotation(g, cfg, usage, s);
                return;
            }

            // Choose the annotation text and color. Stale cache wins: show the
            // cached-at time (compact already) in amber-gray. Otherwise show the
            // long-form reset time in gray, with a compact fallback if it won't fit.
            string longText;
            string compactText;
            Color color;
            if (usage.Stale)
            {
                string cachedAt = string.Format(
                    CultureInfo.InvariantCulture,
                    Strings.CachedAtFmt,
                    usage.FetchedAtUtc.ToLocalTime().ToString("H:mm"));
                longText = cachedAt;
                compactText = cachedAt; // already compact
                color = ThemeManager.Current.StaleText;
            }
            else if (usage.SessionResetUtc.HasValue)
            {
                string local = usage.SessionResetUtc.Value.ToLocalTime().ToString("H:mm");
                longText = string.Format(CultureInfo.InvariantCulture, Strings.TitleResetFmt, local);
                compactText = string.Format(CultureInfo.InvariantCulture, Strings.ResetFmt, local);
                color = ThemeManager.Current.DimText;
            }
            else
            {
                return;
            }

            StringFormat tight = StringFormat.GenericTypographic;

            // Measure the title text to find where it ends, so the annotation
            // never overlaps it.
            float titleEnd;
            using (Font titleFont = new Font("Segoe UI", 10.5f * s, GraphicsUnit.Pixel))
            {
                SizeF titleSize = g.MeasureString(Strings.Title, titleFont, int.MaxValue, tight);
                titleEnd = ContentX * s + titleSize.Width;
            }

            float rightEdge = (cfg.widgetWidth - 20f) * s;
            float minStart = titleEnd + 6f * s;
            float y = 6f * s;

            using (Font annFont = new Font("Segoe UI", 9f * s, GraphicsUnit.Pixel))
            using (SolidBrush annBrush = new SolidBrush(color))
            {
                SizeF longSize = g.MeasureString(longText, annFont, int.MaxValue, tight);
                float longX = rightEdge - longSize.Width;
                if (longX >= minStart)
                {
                    g.DrawString(longText, annFont, annBrush, longX, y, tight);
                    return;
                }

                SizeF compactSize = g.MeasureString(compactText, annFont, int.MaxValue, tight);
                float compactX = rightEdge - compactSize.Width;
                if (compactX >= minStart)
                {
                    g.DrawString(compactText, annFont, annBrush, compactX, y, tight);
                }
                // Otherwise: not enough room; skip (tooltip carries it).
            }
        }

        // Title-row reset annotation for clean mode: the local block reset time
        // (compact "↻ H:mm"), gray, right-aligned in the free space after the
        // title. Callers ensure usage.Active && usage.ResetAtUtc.HasValue.
        private static void DrawCleanTitleAnnotation(Graphics g, Config cfg, UsageResult usage, float s)
        {
            string text = string.Format(
                CultureInfo.InvariantCulture,
                Strings.ResetFmt,
                usage.ResetAtUtc.Value.ToLocalTime().ToString("H:mm"));
            Color color = ThemeManager.Current.DimText;

            StringFormat tight = StringFormat.GenericTypographic;

            float titleEnd;
            using (Font titleFont = new Font("Segoe UI", 10.5f * s, GraphicsUnit.Pixel))
            {
                SizeF titleSize = g.MeasureString(Strings.Title, titleFont, int.MaxValue, tight);
                titleEnd = ContentX * s + titleSize.Width;
            }

            float rightEdge = (cfg.widgetWidth - 20f) * s;
            float minStart = titleEnd + 6f * s;
            float y = 6f * s;

            using (Font annFont = new Font("Segoe UI", 9f * s, GraphicsUnit.Pixel))
            using (SolidBrush annBrush = new SolidBrush(color))
            {
                SizeF size = g.MeasureString(text, annFont, int.MaxValue, tight);
                float x = rightEdge - size.Width;
                if (x >= minStart)
                {
                    g.DrawString(text, annFont, annBrush, x, y, tight);
                }
                // Otherwise: not enough room; skip (tooltip carries it).
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
            else if (cfg.useApi)
            {
                // API mode with nothing fetched yet: show a no-data placeholder.
                row.NoData = true;
                row.Fraction = 0.0;
                row.PctText = "—";
                row.PctValue = 0;
            }
            else
            {
                // Clean mode: drive the meter from the local JSONL estimate with
                // the effective (possibly auto-calibrated) token limit.
                long effLimit = EffectiveLocalLimit(cfg, usage);
                long used = (usage != null && usage.Active) ? usage.UsedTokens : 0L;
                long remaining = effLimit - used;
                if (remaining < 0L)
                    remaining = 0L;

                double frac = effLimit > 0L ? (double)remaining / (double)effLimit : 0.0;
                row.Fraction = Clamp01(frac);
                int remainingInt = (int)Math.Round(frac * 100.0);
                row.PctText = remainingInt.ToString(CultureInfo.InvariantCulture) + "%";
                row.PctValue = remainingInt;

                // Reset annotation from the local block reset time when active.
                if (cfg.showResetTime && usage != null && usage.Active && usage.ResetAtUtc.HasValue)
                {
                    row.ShowReset = true;
                    row.ResetText = string.Format(
                        CultureInfo.InvariantCulture,
                        Strings.ResetFmt,
                        usage.ResetAtUtc.Value.ToLocalTime().ToString("H:mm"));
                }
            }

            return row;
        }

        // Resolves the local meter's token limit: prefer the reader's effective
        // (auto-calibrated) value, then cfg.tokenLimit, then a 200k fallback.
        private static long EffectiveLocalLimit(Config cfg, UsageResult usage)
        {
            if (usage != null && usage.EffectiveTokenLimit > 0L)
                return usage.EffectiveTokenLimit;
            if (cfg.tokenLimit > 0L)
                return cfg.tokenLimit;
            return 200000L;
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

            // How many single-column rows fit in the bars area at MinRowH.
            int capacityPerColumn = (int)Math.Max(1f, (float)Math.Floor(barsAreaH / MinRowH));

            if (rows.Count == 1)
            {
                DrawSingleBar(g, cfg, usage, s, rows[0], titleVisible, barsTop, barsAreaH);
            }
            else if (rows.Count <= capacityPerColumn)
            {
                // Single-column multi-bar layout. When the title is hidden, a
                // compact reset annotation occupies a right-side column.
                float annInset = DrawMultiAnnotation(g, cfg, usage, s, titleVisible, barsTop, barsAreaH);
                DrawMultiBar(g, cfg, s, rows, barsTop, barsAreaH, annInset);
            }
            else
            {
                // Two-column layout. Show up to twice the per-column capacity.
                int displayable = Math.Min(rows.Count, capacityPerColumn * 2);
                int hidden = rows.Count - displayable;
                float annInset = DrawMultiAnnotation(g, cfg, usage, s, titleVisible, barsTop, barsAreaH);
                DrawTwoColumnBars(g, cfg, s, rows, displayable, barsTop, barsAreaH, annInset);
                if (hidden > 0)
                {
                    DrawHiddenCount(g, cfg, s, hidden, barsTop, barsAreaH);
                }
            }
        }

        private static void DrawError(Graphics g, UsageResult usage, float s, int h, bool titleVisible)
        {
            float y = titleVisible ? 18.5f * s : (h - 9f * s) / 2f;
            using (Font errFont = new Font("Segoe UI", 9f * s, GraphicsUnit.Pixel))
            using (SolidBrush errBrush = new SolidBrush(ThemeManager.Current.ErrorText))
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
                DrawSingleValueText(g, cfg, usage, s, row, (barX + barW + 6f), titleVisible);
            }
            else
            {
                // No value text: stretch bar across the full content width.
                // When the title is shown, the reset annotation lives on the title
                // row; only draw the compact value-row annotation when it's hidden.
                bool drawReset = !titleVisible && cfg.showResetTime && row.ShowReset && row.ResetText != null;
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
        private static void DrawSingleValueText(Graphics g, Config cfg, UsageResult usage, float s, BarRow row, float valueXLogical, bool titleVisible)
        {
            float valueX = valueXLogical * s;
            float valueY = 18.5f * s;

            // No API data: render a plain "—" in gray instead of "残り —%".
            if (row.NoData)
            {
                using (Font naFont = new Font("Segoe UI", 12f * s, FontStyle.Bold, GraphicsUnit.Pixel))
                using (SolidBrush naBrush = new SolidBrush(ThemeManager.Current.DimText))
                {
                    g.DrawString("—", naFont, naBrush, valueX, valueY);
                }
                return;
            }

            string valueText = string.Format(
                CultureInfo.InvariantCulture, Strings.RemainingFmt, row.PctValue);
            using (Font valueFont = new Font("Segoe UI", 12f * s, FontStyle.Bold, GraphicsUnit.Pixel))
            using (SolidBrush valueBrush = new SolidBrush(ThemeManager.Current.ValueText))
            {
                g.DrawString(valueText, valueFont, valueBrush, valueX, valueY);

                // When the title is shown, the reset annotation lives on the
                // title row; only draw the value-row annotation when it's hidden.
                if (!titleVisible && cfg.showResetTime && row.ShowReset && row.ResetText != null)
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
                return ThemeManager.Current.StaleText;
            }
            return ThemeManager.Current.DimText;
        }

        private static void DrawResetSmall(Graphics g, float s, string text, float xLogical, float yLogical, Color color)
        {
            using (Font resetFont = new Font("Segoe UI", 9.5f * s, GraphicsUnit.Pixel))
            using (SolidBrush resetBrush = new SolidBrush(color))
            {
                g.DrawString(text, resetFont, resetBrush, xLogical * s, yLogical * s);
            }
        }

        // No-title multi-bar layouts have no title row to carry the reset time,
        // so a compact annotation gets its own right-side column: drawn
        // right-aligned ending at (widgetWidth - 8), vertically centered in the
        // bars area. Returns the logical width the bar cells must give up
        // (annotation width + 6 gap), or 0 when nothing is drawn.
        private static float DrawMultiAnnotation(Graphics g, Config cfg, UsageResult usage, float s,
            bool titleVisible, float barsTop, float barsAreaH)
        {
            if (titleVisible || !cfg.showResetTime)
            {
                return 0f;
            }
            if (usage == null || !usage.FromApi)
            {
                return 0f;
            }

            // Compact forms only (mirrors DrawTitleAnnotation's fallback):
            // stale → cached-at "(H:mm)" in amber-gray; else reset "↻ H:mm" in gray.
            string text;
            Color color;
            if (usage.Stale)
            {
                text = string.Format(
                    CultureInfo.InvariantCulture,
                    Strings.CachedAtFmt,
                    usage.FetchedAtUtc.ToLocalTime().ToString("H:mm"));
                color = ThemeManager.Current.StaleText;
            }
            else if (usage.SessionResetUtc.HasValue)
            {
                text = string.Format(
                    CultureInfo.InvariantCulture,
                    Strings.ResetFmt,
                    usage.SessionResetUtc.Value.ToLocalTime().ToString("H:mm"));
                color = ThemeManager.Current.DimText;
            }
            else
            {
                return 0f;
            }

            StringFormat tight = StringFormat.GenericTypographic;
            using (Font annFont = new Font("Segoe UI", 9f * s, GraphicsUnit.Pixel))
            using (SolidBrush annBrush = new SolidBrush(color))
            {
                SizeF size = g.MeasureString(text, annFont, int.MaxValue, tight);
                float rightEdge = (cfg.widgetWidth - 8f) * s;
                float x = rightEdge - size.Width;
                float y = (barsTop + barsAreaH / 2f) * s - size.Height / 2f;
                g.DrawString(text, annFont, annBrush, x, y, tight);
                return size.Width / s + 6f;
            }
        }

        // 2-N rows in a single column: [label][bar][pct]. Rows distributed evenly
        // in barsAreaH. Reset time is omitted per-row; with the title hidden it
        // lives in the right annotation column (rightInset > 0), otherwise on
        // the title row. rightInset is the logical width reserved on the right.
        private static void DrawMultiBar(Graphics g, Config cfg, float s, List<BarRow> rows, float barsTop, float barsAreaH, float rightInset)
        {
            int count = rows.Count;
            float rowH = barsAreaH / count;

            // Single-column metrics (preserve the classic look).
            float labelW = 26f;
            float pctW = cfg.showValueText ? 30f : 0f;

            float contentW = cfg.widgetWidth - ContentX - ContentRightPad - rightInset;

            for (int i = 0; i < count; i++)
            {
                BarRow row = rows[i];
                float rowTop = barsTop + rowH * i;
                float rowMid = rowTop + rowH / 2f;

                DrawBarCell(g, cfg, s, row, ContentX, contentW, rowMid, rowH,
                    labelW, pctW, cfg.showValueText, false, false);
            }
        }

        // Two-column layout. The first `displayable` rows are split column-major:
        // the LEFT column holds the first ceil(displayable/2) rows top-to-bottom,
        // the RIGHT column holds the remainder. Both columns use the LEFT column's
        // row count for vertical distribution so rows align horizontally.
        private static void DrawTwoColumnBars(Graphics g, Config cfg, float s, List<BarRow> rows,
            int displayable, float barsTop, float barsAreaH, float rightInset)
        {
            int leftCount = (displayable + 1) / 2; // ceil(displayable / 2)

            float contentW = cfg.widgetWidth - ContentX - ContentRightPad - rightInset;
            float colW = (contentW - ColumnGap) / 2f;

            // Vertical distribution mirrors the single-column path but always uses
            // the left column's row count so the two columns line up row-for-row.
            float rowH = barsAreaH / leftCount;

            // Compact per-cell metrics for two columns.
            float labelW = 20f;
            float pctW = cfg.showValueText ? 26f : 0f;

            float leftX = ContentX;
            float rightX = ContentX + colW + ColumnGap;

            for (int i = 0; i < displayable; i++)
            {
                BarRow row = rows[i];
                bool inLeft = i < leftCount;
                int rowIndex = inLeft ? i : (i - leftCount);
                float cellX = inLeft ? leftX : rightX;

                float rowTop = barsTop + rowH * rowIndex;
                float rowMid = rowTop + rowH / 2f;

                // With the title visible the source dot sits at ~(widgetWidth-12, 7)
                // logical; the right column's top cell can reach that zone when the
                // first row starts high. Inset that one cell if needed. With the
                // title hidden the dot is at the top-LEFT, so no inset is required.
                bool insetForDot = cfg.showTitle && (!inLeft) && rowIndex == 0 && (rowMid - rowH / 2f) < 12f;

                DrawBarCell(g, cfg, s, row, cellX, colW, rowMid, rowH,
                    labelW, pctW, cfg.showValueText, true, insetForDot);
            }
        }

        // Draws one bar cell: [label][bar][pct] within a cell starting at
        // xLogical of width cellWLogical, vertically centered on rowMidLogical.
        // Shared by the single-column and two-column paths; metrics are passed in.
        // `compact` selects the smaller label/pct fonts used in two-column mode.
        // `insetForDot` reserves a little right-edge room so the top-right source
        // dot is never overlapped by a bar or pct text.
        private static void DrawBarCell(Graphics g, Config cfg, float s, BarRow row,
            float xLogical, float cellWLogical, float rowMidLogical, float rowH,
            float labelW, float pctW, bool showPct, bool compact, bool insetForDot)
        {
            float gap = 4f;
            float cellW = cellWLogical;
            if (insetForDot)
            {
                cellW -= 8f;
                if (cellW < 20f)
                {
                    cellW = 20f;
                }
            }

            float barX = xLogical + labelW + gap;
            float barW = cellW - labelW - gap - (showPct ? (gap + pctW) : 0f);
            if (barW < 10f)
            {
                barW = 10f;
            }
            float barH = Math.Min(compact ? 6f : 7f, rowH - 3f);
            if (barH < 3f)
            {
                barH = 3f;
            }
            float barY = rowMidLogical - barH / 2f;

            DrawMultiLabel(g, s, row.Label, xLogical, rowMidLogical, compact);
            DrawBar(g, barX * s, barY * s, barW * s, barH * s, row.Fraction);

            if (showPct)
            {
                DrawMultiPct(g, s, row.PctText, barX + barW + gap, pctW, rowMidLogical, compact);
            }
        }

        // Tiny "+N" marker at the far bottom-right inside the card, indicating
        // some bars are hidden (the tooltip lists everything).
        private static void DrawHiddenCount(Graphics g, Config cfg, float s, int hidden, float barsTop, float barsAreaH)
        {
            string text = "+" + hidden.ToString(CultureInfo.InvariantCulture);
            using (Font font = new Font("Segoe UI", 8f * s, GraphicsUnit.Pixel))
            using (SolidBrush brush = new SolidBrush(ThemeManager.Current.DimText))
            {
                StringFormat tight = StringFormat.GenericTypographic;
                SizeF size = g.MeasureString(text, font, int.MaxValue, tight);
                float rightEdge = (cfg.widgetWidth - ContentRightPad) * s;
                float x = rightEdge - size.Width;
                float bottomEdge = (barsTop + barsAreaH) * s;
                float y = bottomEdge - size.Height;
                g.DrawString(text, font, brush, x, y, tight);
            }
        }

        private static void DrawMultiLabel(Graphics g, float s, string label, float xLogical, float rowMidLogical, bool compact)
        {
            float fontPx = compact ? 8.5f : 9f;
            using (Font labelFont = new Font("Segoe UI", fontPx * s, GraphicsUnit.Pixel))
            using (SolidBrush labelBrush = new SolidBrush(ThemeManager.Current.DimText))
            {
                SizeF size = g.MeasureString(label, labelFont);
                float y = rowMidLogical * s - size.Height / 2f;
                g.DrawString(label, labelFont, labelBrush, xLogical * s, y);
            }
        }

        private static void DrawMultiPct(Graphics g, float s, string pctText, float xLogical, float widthLogical, float rowMidLogical, bool compact)
        {
            float fontPx = compact ? 8.5f : 9.5f;
            using (Font pctFont = new Font("Segoe UI", fontPx * s, FontStyle.Bold, GraphicsUnit.Pixel))
            using (SolidBrush pctBrush = new SolidBrush(ThemeManager.Current.ValueText))
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
            using (SolidBrush trackBrush = new SolidBrush(ThemeManager.Current.BarTrack))
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

        // Draws a small filled circle indicating the data source. Sits in the
        // top-right interior of the card when the title is visible; moves to the
        // top-LEFT (over the icon column) when the title is hidden, so it cannot
        // collide with right-aligned pct text or the reset annotation column.
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
            float dotCenterX = cfg.showTitle ? (cfg.widgetWidth - 12f) : 7f;
            float dotX = dotCenterX * s - diameter / 2f;
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
