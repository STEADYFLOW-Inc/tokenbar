using System;
using System.Drawing;
using Microsoft.Win32;

namespace ClaudeTokenMeter
{
    // Immutable palette for the widget card. All colors that used to be
    // hardcoded in CardRenderer live here so the card can adapt to the
    // Windows light/dark theme and the accent color.
    //
    // The bar gradient greens/oranges and the semantic source-dot colors are
    // NOT part of the theme; they read well on every background and stay
    // hardcoded in CardRenderer.
    //
    // C# 5.0 syntax only.
    internal sealed class Theme
    {
        public readonly Color Background;       // matches the taskbar surface
        public readonly Color CardFill;
        public readonly Color CardFillHover;
        public readonly Color CardBorder;
        public readonly Color CardBorderHover;
        public readonly Color TitleText;        // dim heading
        public readonly Color ValueText;        // main bold value
        public readonly Color DimText;          // labels / reset time
        public readonly Color StaleText;        // amber-ish cached-at annotation
        public readonly Color BarTrack;         // bar background
        public readonly Color ErrorText;

        public Theme(
            Color background,
            Color cardFill, Color cardFillHover,
            Color cardBorder, Color cardBorderHover,
            Color titleText,
            Color valueText,
            Color dimText,
            Color staleText,
            Color barTrack,
            Color errorText)
        {
            Background = background;
            CardFill = cardFill;
            CardFillHover = cardFillHover;
            CardBorder = cardBorder;
            CardBorderHover = cardBorderHover;
            TitleText = titleText;
            ValueText = valueText;
            DimText = dimText;
            StaleText = staleText;
            BarTrack = barTrack;
            ErrorText = errorText;
        }
    }

    // Detects the Windows theme (light/dark + accent-on-taskbar) from the
    // registry and builds a matching Theme. All registry reads are wrapped in
    // try/catch and fall back to the dark palette on any failure.
    internal static class ThemeManager
    {
        private const string PersonalizeKey =
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        private const string DwmKey =
            @"SOFTWARE\Microsoft\Windows\DWM";

        // Luminance threshold below which an accent color is treated as "dark".
        private const double DarkAccentLuminance = 140.0;

        private static readonly object sync = new object();
        private static Theme current;
        private static string currentKey;

        // The live palette. Built lazily on first access.
        public static Theme Current
        {
            get
            {
                lock (sync)
                {
                    if (current == null)
                    {
                        Rebuild();
                    }
                    return current;
                }
            }
        }

        // Re-reads the registry and rebuilds the palette. Returns true when the
        // resulting palette differs from the previous one (so the caller can
        // repaint only when something actually changed).
        public static bool Refresh()
        {
            lock (sync)
            {
                return Rebuild();
            }
        }

        // Test-only hook: forces the palette from explicit values, bypassing the
        // registry. Used exclusively by Program's --rendertest path.
        internal static void ForceForTest(int light, int prevalence, Color accent)
        {
            lock (sync)
            {
                current = BuildPalette(light, prevalence, accent);
                currentKey = ComputeKey(light, prevalence, accent);
            }
        }

        // Reads the current machine's accent color from the registry, falling
        // back to the classic Windows blue when unavailable. Public so the
        // rendertest accent sample can use the real accent.
        internal static Color ReadAccentColor()
        {
            Color fallback = Color.FromArgb(255, 0, 120, 215);
            try
            {
                object v = Registry.GetValue(
                    @"HKEY_CURRENT_USER\" + DwmKey, "AccentColor", null);
                if (v is int)
                {
                    return AccentFromDword((int)v);
                }
            }
            catch (Exception)
            {
            }
            return fallback;
        }

        // Rebuilds `current` from the registry. Returns whether it changed.
        private static bool Rebuild()
        {
            int light = ReadDword(PersonalizeKey, "SystemUsesLightTheme", 0);
            int prevalence = ReadDword(PersonalizeKey, "ColorPrevalence", 0);
            Color accent = ReadAccentColor();

            string key = ComputeKey(light, prevalence, accent);
            if (key == currentKey && current != null)
            {
                return false;
            }
            current = BuildPalette(light, prevalence, accent);
            currentKey = key;
            return true;
        }

        private static string ComputeKey(int light, int prevalence, Color accent)
        {
            return light.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "|" + prevalence.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "|" + accent.ToArgb().ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static int ReadDword(string subKey, string valueName, int fallback)
        {
            try
            {
                object v = Registry.GetValue(
                    @"HKEY_CURRENT_USER\" + subKey, valueName, null);
                if (v is int)
                {
                    return (int)v;
                }
            }
            catch (Exception)
            {
            }
            return fallback;
        }

        // AccentColor is stored as an AABBGGRR dword:
        //   red   = v        & 0xFF
        //   green = (v >> 8)  & 0xFF
        //   blue  = (v >> 16) & 0xFF
        // (alpha byte ignored; the accent is always opaque here).
        private static Color AccentFromDword(int v)
        {
            int r = v & 0xFF;
            int g = (v >> 8) & 0xFF;
            int b = (v >> 16) & 0xFF;
            return Color.FromArgb(255, r, g, b);
        }

        // ---- Palette construction -----------------------------------------

        private static Theme BuildPalette(int light, int prevalence, Color accent)
        {
            if (prevalence != 0)
            {
                return BuildAccentPalette(accent);
            }
            if (light != 0)
            {
                return BuildLightPalette();
            }
            return BuildDarkPalette();
        }

        // DARK — exactly the values CardRenderer previously hardcoded.
        private static Theme BuildDarkPalette()
        {
            return new Theme(
                Color.FromArgb(32, 32, 32),      // Background
                Color.FromArgb(45, 45, 45),      // CardFill
                Color.FromArgb(52, 52, 52),      // CardFillHover
                Color.FromArgb(70, 70, 70),      // CardBorder
                Color.FromArgb(96, 96, 96),      // CardBorderHover
                Color.FromArgb(200, 200, 200),   // TitleText
                Color.FromArgb(255, 255, 255),   // ValueText
                Color.FromArgb(160, 160, 160),   // DimText
                Color.FromArgb(198, 168, 120),   // StaleText
                Color.FromArgb(74, 74, 74),      // BarTrack
                Color.FromArgb(160, 160, 160));  // ErrorText (matches old gray)
        }

        // LIGHT — a bright surface that sits under the Windows light taskbar.
        private static Theme BuildLightPalette()
        {
            return new Theme(
                Color.FromArgb(243, 243, 243),   // Background
                Color.FromArgb(255, 255, 255),   // CardFill
                Color.FromArgb(246, 246, 246),   // CardFillHover
                Color.FromArgb(190, 190, 190),   // CardBorder
                Color.FromArgb(150, 150, 150),   // CardBorderHover
                Color.FromArgb(96, 96, 96),      // TitleText
                Color.FromArgb(25, 25, 25),      // ValueText
                Color.FromArgb(110, 110, 110),   // DimText
                Color.FromArgb(170, 120, 30),    // StaleText
                Color.FromArgb(205, 205, 205),   // BarTrack
                Color.FromArgb(120, 120, 120));  // ErrorText (readable gray)
        }

        // ACCENT — background is the accent color; everything else is derived
        // from the accent's luminance so text stays legible on either a dark or
        // a light accent.
        private static Theme BuildAccentPalette(Color accent)
        {
            double lum = Luminance(accent);
            Color white = Color.FromArgb(255, 255, 255, 255);
            Color black = Color.FromArgb(255, 0, 0, 0);

            if (lum < DarkAccentLuminance)
            {
                // Dark accent: lighten fills/borders, use light text.
                return new Theme(
                    accent,                              // Background
                    Blend(accent, white, 0.10),          // CardFill
                    Blend(accent, white, 0.16),          // CardFillHover
                    Blend(accent, white, 0.30),          // CardBorder
                    Blend(accent, white, 0.45),          // CardBorderHover
                    Blend(white, accent, 0.15),          // TitleText
                    white,                               // ValueText
                    Blend(white, accent, 0.30),          // DimText
                    Color.FromArgb(255, 214, 150),       // StaleText
                    Blend(accent, black, 0.30),          // BarTrack
                    Blend(white, accent, 0.25));         // ErrorText (light, readable)
            }

            // Light accent: darken fills/borders, use dark text.
            return new Theme(
                accent,                                  // Background
                Blend(accent, black, 0.06),              // CardFill
                Blend(accent, black, 0.12),              // CardFillHover
                Blend(accent, black, 0.24),              // CardBorder
                Blend(accent, black, 0.38),              // CardBorderHover
                Blend(black, accent, 0.15),              // TitleText
                Blend(black, accent, 0.05),              // ValueText (near-black)
                Blend(black, accent, 0.30),              // DimText
                Color.FromArgb(120, 80, 10),             // StaleText
                Blend(accent, white, 0.35),              // BarTrack
                Blend(black, accent, 0.20));             // ErrorText (dark, readable)
        }

        // Perceived luminance of a color (0-255) via the standard weights.
        private static double Luminance(Color c)
        {
            return 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
        }

        // Linear blend from a toward b by t (0 = a, 1 = b). Result is opaque.
        internal static Color Blend(Color a, Color b, double t)
        {
            if (t < 0.0) t = 0.0;
            if (t > 1.0) t = 1.0;
            int r = (int)Math.Round(a.R + (b.R - a.R) * t);
            int g = (int)Math.Round(a.G + (b.G - a.G) * t);
            int bl = (int)Math.Round(a.B + (b.B - a.B) * t);
            return Color.FromArgb(255, Clamp(r), Clamp(g), Clamp(bl));
        }

        private static int Clamp(int v)
        {
            if (v < 0) return 0;
            if (v > 255) return 255;
            return v;
        }
    }
}
