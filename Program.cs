using System;
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

            bool createdNew;
            using (var mutex = new Mutex(true, "TokenBar_SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    return;
                }
                try
                {
                    System.Net.ServicePointManager.SecurityProtocol = System.Net.ServicePointManager.SecurityProtocol | System.Net.SecurityProtocolType.Tls12;
                }
                catch
                {
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
    }
}
