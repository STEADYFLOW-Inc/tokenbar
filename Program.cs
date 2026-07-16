using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ClaudeTokenMeter
{
    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2
        private static readonly IntPtr DpiPerMonitorV2 = new IntPtr(-4);

        [STAThread]
        private static void Main(string[] args)
        {
            EnableDpiAwareness();

            if (args != null && args.Length > 0 && args[0] == "--dump")
            {
                DumpUsage();
                return;
            }

            if (args != null && args.Length > 0 && args[0] == "--rendertest")
            {
                RenderTest();
                return;
            }

            bool createdNew;
            using (var mutex = new Mutex(true, "TokenBar_SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    return;
                }
                // Strict TLS floor: 1.2/1.3 only. If the OS rejects the TLS 1.3
                // flag (older Windows 10), fall back to adding 1.2 to the set.
                try
                {
                    System.Net.ServicePointManager.SecurityProtocol =
                        System.Net.SecurityProtocolType.Tls12 | (System.Net.SecurityProtocolType)12288;
                }
                catch
                {
                    try
                    {
                        System.Net.ServicePointManager.SecurityProtocol =
                            System.Net.ServicePointManager.SecurityProtocol | System.Net.SecurityProtocolType.Tls12;
                    }
                    catch
                    {
                    }
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MeterAppContext());
                GC.KeepAlive(mutex);
            }
        }

        private static void EnableDpiAwareness()
        {
            try
            {
                SetProcessDpiAwarenessContext(DpiPerMonitorV2);
            }
            catch (EntryPointNotFoundException)
            {
                try { SetProcessDPIAware(); } catch { }
            }
        }

        private static void DumpUsage()
        {
            Config cfg = Config.Load();
            UsageResult r = UsageReader.Read(cfg);
            string text = string.Format(
                "active={0}\r\nusedTokens={1}\r\nblockStartUtc={2}\r\nresetAtUtc={3}\r\nlastActivityUtc={4}\r\nerror={5}\r\n" +
                "fromApi={6}\r\nsessionPct={7}\r\nsessionResetUtc={8}\r\nweeklyPct={9}\r\nweeklyScopedPct={10}\r\nweeklyScopedModel={11}\r\n",
                r.Active, r.UsedTokens, r.BlockStartUtc, r.ResetAtUtc, r.LastActivityUtc, r.Error,
                r.FromApi, r.SessionPct, r.SessionResetUtc, r.WeeklyPct, r.WeeklyScopedPct, r.WeeklyScopedModel);
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dump.txt"), text);
        }

        // Renders a sample card in each of the three palettes (dark / light /
        // accent) to PNG files, so the theming can be eyeballed without needing
        // to flip the actual Windows theme. The accent sample uses this machine's
        // real accent color (falling back to Windows blue).
        private static void RenderTest()
        {
            Config cfg = Config.Load();
            // Force a predictable layout for the sample regardless of saved config.
            cfg.showTitle = true;
            cfg.showSessionBar = true;
            cfg.showValueText = true;
            cfg.showResetTime = true;

            UsageResult usage = new UsageResult();
            usage.FromApi = true;
            usage.SessionPct = 42.0;
            usage.SessionResetUtc = DateTime.UtcNow.AddHours(2);
            usage.WeeklyPct = 30.0;
            usage.FetchedAtUtc = DateTime.UtcNow;
            usage.ScopedLimits = new List<ScopedLimit>();
            ScopedLimit sl = new ScopedLimit();
            sl.Model = "Fable";
            sl.Pct = 55.0;
            usage.ScopedLimits.Add(sl);

            Color accent = ThemeManager.ReadAccentColor();

            RenderOnePalette(cfg, usage, 0, 0, accent, "rendertest_dark.png");
            RenderOnePalette(cfg, usage, 1, 0, accent, "rendertest_light.png");
            RenderOnePalette(cfg, usage, 0, 1, accent, "rendertest_accent.png");
        }

        private static void RenderOnePalette(Config cfg, UsageResult usage,
            int light, int prevalence, Color accent, string fileName)
        {
            ThemeManager.ForceForTest(light, prevalence, accent);

            const int width = 260;
            const int height = 40;
            using (Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                CardRenderer.Draw(g, cfg, usage, 1f, width, height, false);
                string outPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, fileName);
                bmp.Save(outPath, ImageFormat.Png);
            }
        }
    }
}
