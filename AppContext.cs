using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ClaudeTokenMeter
{
    public class MeterAppContext : ApplicationContext
    {
        private const string RunKeyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
        private const string RunValueName = "TokenBar";

        private readonly Config cfg;
        private readonly System.Windows.Forms.Timer placeTimer;
        private readonly System.Windows.Forms.Timer dataTimer;
        private TaskScheduler uiScheduler;
        private WidgetForm widget;
        private SettingsForm settingsForm;
        private SetupForm setupForm;
        private UsageResult lastUsage;
        private volatile bool refreshing;
        private UsageResult lastApiUsage;
        private DateTime lastApiSuccessUtc = DateTime.MinValue;

        public MeterAppContext()
        {
            // Detect a genuine first run (no config.json yet) BEFORE Config.Load,
            // which creates a default config.json on the first call. Existing
            // installs (config.json present but without setupDone) are marked done
            // silently so they never see the first-run wizard.
            bool isFirstRun = !System.IO.File.Exists(Config.ConfigPath);
            cfg = Config.Load();
            if (!isFirstRun && !cfg.setupDone)
            {
                cfg.setupDone = true;
                cfg.Save();
            }

            EnsureWidget();

            // Capture the UI scheduler AFTER the widget has created its handle so
            // ContinueWith callbacks marshal back onto the UI thread.
            uiScheduler = TaskScheduler.FromCurrentSynchronizationContext();

            placeTimer = new System.Windows.Forms.Timer();
            placeTimer.Interval = 5000;
            placeTimer.Tick += PlaceTimer_Tick;
            placeTimer.Start();

            dataTimer = new System.Windows.Forms.Timer();
            dataTimer.Interval = Math.Max(5, cfg.refreshSec) * 1000;
            dataTimer.Tick += DataTimer_Tick;
            dataTimer.Start();

            RefreshData();

            // First launch: show the quick-setup wizard non-modally. Existing
            // installs already had setupDone forced true above, so this only
            // fires for a genuine first run.
            if (!cfg.setupDone)
            {
                try
                {
                    setupForm = new SetupForm(cfg, this);
                    setupForm.Show();
                    setupForm.Activate();
                }
                catch
                {
                }
            }
        }

        private void PlaceTimer_Tick(object sender, EventArgs e)
        {
            EnsureWidget();
        }

        private void DataTimer_Tick(object sender, EventArgs e)
        {
            RefreshData();
        }

        private bool WidgetAlive()
        {
            return widget != null && !widget.IsDisposed;
        }

        private void EnsureWidget()
        {
            try
            {
                if (widget == null || widget.IsDisposed)
                {
                    widget = new WidgetForm(cfg, this);
                    widget.Show();
                    widget.SetUsage(lastUsage);
                }
                widget.UpdatePlacement();
            }
            catch
            {
            }
        }

        public void RefreshData()
        {
            if (refreshing)
            {
                return;
            }
            refreshing = true;

            try
            {
                Task.Factory.StartNew(delegate
                {
                    return UsageReader.Read(cfg);
                }).ContinueWith(delegate(Task<UsageResult> t)
                {
                    refreshing = false;
                    try
                    {
                        if (t.Status == TaskStatus.RanToCompletion && t.Result != null)
                        {
                            UsageResult fresh = t.Result;
                            if (fresh.FromApi && !fresh.Stale)
                            {
                                // Live API data: cache in-memory and display.
                                lastApiUsage = fresh;
                                lastApiSuccessUtc = DateTime.UtcNow;
                                lastUsage = fresh;
                            }
                            else if (fresh.FromApi && fresh.Stale)
                            {
                                // Disk-cached API snapshot. It carries its own FetchedAtUtc, so
                                // do NOT update lastApiSuccessUtc. Prefer whichever of the disk
                                // snapshot and the in-memory snapshot is newer by FetchedAtUtc.
                                if (lastApiUsage != null &&
                                    lastApiUsage.FetchedAtUtc >= fresh.FetchedAtUtc)
                                {
                                    lastApiUsage.Stale = true;
                                    lastUsage = lastApiUsage;
                                }
                                else
                                {
                                    lastUsage = fresh;
                                }
                            }
                            else if (lastApiUsage != null)
                            {
                                // Local estimate only, but we still have prior API data in memory:
                                // always prefer it (no 30-minute cutoff) with a stale flag.
                                lastApiUsage.Stale = true;
                                lastUsage = lastApiUsage;
                            }
                            else
                            {
                                // No usable API cache at all; fall back to local estimate.
                                lastUsage = fresh;
                            }

                            if (WidgetAlive())
                            {
                                widget.SetUsage(lastUsage);
                            }
                        }
                    }
                    catch
                    {
                    }
                }, uiScheduler);
            }
            catch
            {
                refreshing = false;
            }
        }

        public bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (key == null)
                    {
                        return false;
                    }
                    object val = key.GetValue(RunValueName);
                    return val != null;
                }
            }
            catch
            {
                return false;
            }
        }

        public void SetStartup(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath))
                {
                    if (key == null)
                    {
                        return;
                    }
                    if (enable)
                    {
                        key.SetValue(RunValueName, "\"" + Application.ExecutablePath + "\"");
                    }
                    else
                    {
                        object existing = key.GetValue(RunValueName);
                        if (existing != null)
                        {
                            key.DeleteValue(RunValueName, false);
                        }
                    }
                }
            }
            catch
            {
            }
        }

        public void OpenConfig()
        {
            try
            {
                // Absolute notepad path (never PATH-resolved) and a quote-free
                // argument so the path can't break out of its quoting.
                string notepad = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    "notepad.exe");
                string arg = Config.ConfigPath.Replace("\"", "");
                Process.Start(notepad, "\"" + arg + "\"");
            }
            catch
            {
            }
        }

        public void ReloadConfig()
        {
            try
            {
                Config fresh = Config.Load();
                cfg.CopyFrom(fresh);
                dataTimer.Interval = Math.Max(5, cfg.refreshSec) * 1000;
                RefreshData();
                if (WidgetAlive())
                {
                    widget.UpdatePlacement();
                }
            }
            catch
            {
            }
        }

        public void OpenSettings()
        {
            try
            {
                if (settingsForm != null && !settingsForm.IsDisposed)
                {
                    settingsForm.Activate();
                    settingsForm.BringToFront();
                    return;
                }

                // Show non-modal: a modal dialog from a NOACTIVATE widget can
                // behave oddly, so a non-modal window is safer here.
                settingsForm = new SettingsForm(cfg, this);
                settingsForm.Show();
                settingsForm.Activate();
            }
            catch
            {
            }
        }

        public UsageResult LastUsage
        {
            get { return lastUsage; }
        }

        public void ApplySettings()
        {
            try
            {
                dataTimer.Interval = Math.Max(5, cfg.refreshSec) * 1000;
                if (WidgetAlive())
                {
                    widget.UpdatePlacement();
                    widget.Invalidate();
                }
                RefreshData();
            }
            catch
            {
            }
        }

        // Like ApplySettings but without cfg.Save side-effects and without
        // RefreshData. Used for live preview from the settings dialog while it
        // is open, so the widget updates in real time as controls change.
        public void PreviewSettings()
        {
            try
            {
                dataTimer.Interval = Math.Max(5, cfg.refreshSec) * 1000;
                if (WidgetAlive())
                {
                    widget.UpdatePlacement();
                    widget.Invalidate();
                }
            }
            catch
            {
            }
        }

        public void ExitApp()
        {
            try
            {
                if (placeTimer != null)
                {
                    placeTimer.Stop();
                }
                if (dataTimer != null)
                {
                    dataTimer.Stop();
                }
                if (settingsForm != null && !settingsForm.IsDisposed)
                {
                    settingsForm.Close();
                }
                if (setupForm != null && !setupForm.IsDisposed)
                {
                    setupForm.Close();
                }
                if (WidgetAlive())
                {
                    widget.Close();
                }
            }
            catch
            {
            }
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (placeTimer != null)
                {
                    placeTimer.Dispose();
                }
                if (dataTimer != null)
                {
                    dataTimer.Dispose();
                }
                if (settingsForm != null && !settingsForm.IsDisposed)
                {
                    settingsForm.Dispose();
                }
                if (setupForm != null && !setupForm.IsDisposed)
                {
                    setupForm.Dispose();
                }
                if (widget != null && !widget.IsDisposed)
                {
                    widget.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
