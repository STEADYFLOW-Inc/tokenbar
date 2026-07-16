using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
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

        // Win32 constants.
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x80;
        private const int WS_EX_LAYERED = 0x00080000;

        // Layered-window blend/update constants.
        private const byte AC_SRC_OVER = 0x00;
        private const byte AC_SRC_ALPHA = 0x01;
        private const uint ULW_ALPHA = 0x00000002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_HIDEWINDOW = 0x0080;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        // WinEvent hook constants for instant foreground-change reaction.
        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

        // Our own process id, cached once. Used to skip hiding for fullscreen
        // when our own settings/setup windows are the foreground window.
        private static readonly int CurrentProcessId =
            System.Diagnostics.Process.GetCurrentProcess().Id;

        private readonly Config cfg;
        private readonly MeterAppContext owner;
        private readonly ToolTip tip;

        // Foreground WinEvent hook. The delegate MUST be held in an instance
        // field so the GC cannot collect it while the hook is alive.
        private IntPtr winEventHook = IntPtr.Zero;
        private WinEventDelegate winEventProc;

        private UsageResult usage;
        private float scale = 1f;
        private bool hovered;

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
            BackColor = ThemeManager.Current.Background;

            Cursor = Cursors.Hand;

            BuildContextMenu();

            Click += WidgetForm_Click;
        }

        // Installs the foreground WinEvent hook once the window handle exists.
        // The callback fires on this thread via the message loop (out-of-context),
        // so it is safe to touch UI state directly from it.
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            if (winEventHook == IntPtr.Zero)
            {
                // Keep the delegate alive for the lifetime of the hook.
                winEventProc = new WinEventDelegate(OnForegroundChanged);
                winEventHook = SetWinEventHook(
                    EVENT_SYSTEM_FOREGROUND,
                    EVENT_SYSTEM_FOREGROUND,
                    IntPtr.Zero,
                    winEventProc,
                    0,
                    0,
                    WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
            }

            // Push an initial layered bitmap so the window has valid per-pixel
            // alpha content immediately (before the first placement, if any).
            UpdateVisual();
        }

        // Fires whenever the foreground window changes. React instantly so we
        // do not linger on top of a newly-fullscreen app until the next timer
        // tick. UpdatePlacement already handles the hide/show decision.
        private void OnForegroundChanged(IntPtr hWinEventHook, uint eventType,
            IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            try
            {
                if (IsDisposed || !IsHandleCreated)
                {
                    return;
                }
                UpdatePlacement();
            }
            catch
            {
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (winEventHook != IntPtr.Zero)
            {
                UnhookWinEvent(winEventHook);
                winEventHook = IntPtr.Zero;
            }
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
                cp.ExStyle |= WS_EX_LAYERED;
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

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            hovered = true;
            UpdateVisual();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hovered = false;
            UpdateVisual();
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
            UpdateVisual();
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
                    api.Append("\r\n");
                    api.Append(string.Format(
                        CultureInfo.InvariantCulture,
                        Strings.TipCachedAtFmt,
                        u.FetchedAtUtc.ToLocalTime().ToString("H:mm")));
                }
                if (u.AuthExpired)
                {
                    api.Append("\r\n");
                    api.Append(Strings.TipAuthExpired);
                }
                api.Append(updatedLine);
                return api.ToString();
            }

            StringBuilder sb = new StringBuilder();
            // Clean mode (useApi == false): the local estimate is the intended
            // data source, not a fallback — lead with the estimate lines directly.
            // API mode with nothing cached yet: lead with the not-connected notice.
            if (cfg.useApi)
            {
                sb.Append(Strings.TipNoApiData);
                sb.Append("\r\n");
            }
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
            if (u.AuthExpired)
            {
                sb.Append("\r\n");
                sb.Append(Strings.TipAuthExpired);
            }
            sb.Append(updatedLine);

            return sb.ToString();
        }

        // ---- Painting -----------------------------------------------------

        // With a layered (WS_EX_LAYERED) window driven by UpdateLayeredWindow,
        // WM_PAINT carries no visible content — all rendering flows through
        // UpdateVisual(). Keep a minimal no-op override so no stray background
        // painting fights the layered bitmap.
        protected override void OnPaint(PaintEventArgs e)
        {
        }

        // Renders the card into a 32bpp premultiplied-alpha bitmap and pushes it
        // to the layered window via UpdateLayeredWindow, so the area outside the
        // rounded card is truly transparent and the translucent taskbar shows
        // through. Replaces the old OnPaint/Invalidate repaint path.
        private void UpdateVisual()
        {
            try
            {
                if (!IsHandleCreated || IsDisposed)
                {
                    return;
                }

                int w = Width;
                int h = Height;
                if (w <= 0 || h <= 0)
                {
                    return;
                }

                using (Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        // ClearType on a transparent background produces color
                        // fringing; AntiAliasGridFit keeps text clean on the
                        // per-pixel-alpha bitmap.
                        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                        CardRenderer.Draw(g, cfg, usage, scale, w, h, hovered, false);
                    }

                    PushLayeredBitmap(bmp, w, h);
                }
            }
            catch
            {
            }
        }

        // Pushes the given ARGB bitmap to the layered window. Keeps the current
        // window position (pptDst = null) and uses the bitmap's own per-pixel
        // alpha (AC_SRC_ALPHA). Fully cleans up all GDI resources.
        private void PushLayeredBitmap(Bitmap bmp, int w, int h)
        {
            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = IntPtr.Zero;
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr oldBitmap = IntPtr.Zero;
            try
            {
                memDc = CreateCompatibleDC(screenDc);
                hBitmap = bmp.GetHbitmap(Color.FromArgb(0));
                oldBitmap = SelectObject(memDc, hBitmap);

                SIZE size = new SIZE();
                size.cx = w;
                size.cy = h;

                POINT srcPoint = new POINT();
                srcPoint.x = 0;
                srcPoint.y = 0;

                BLENDFUNCTION blend = new BLENDFUNCTION();
                blend.BlendOp = AC_SRC_OVER;
                blend.BlendFlags = 0;
                blend.SourceConstantAlpha = 255;
                blend.AlphaFormat = AC_SRC_ALPHA;

                UpdateLayeredWindow(Handle, screenDc, IntPtr.Zero, ref size,
                    memDc, ref srcPoint, 0, ref blend, ULW_ALPHA);
            }
            finally
            {
                if (screenDc != IntPtr.Zero)
                {
                    ReleaseDC(IntPtr.Zero, screenDc);
                }
                if (memDc != IntPtr.Zero)
                {
                    if (oldBitmap != IntPtr.Zero)
                    {
                        SelectObject(memDc, oldBitmap);
                    }
                    DeleteDC(memDc);
                }
                if (hBitmap != IntPtr.Zero)
                {
                    DeleteObject(hBitmap);
                }
            }
        }

        // ---- Taskbar placement --------------------------------------------

        // Parses the trailing integer from a screen device name
        // ("\\.\DISPLAY3" -> 3). Returns 0 on failure.
        public static int GetDisplayNumber(Screen s)
        {
            if (s == null || s.DeviceName == null)
            {
                return 0;
            }
            string name = s.DeviceName;
            int i = name.Length;
            while (i > 0 && name[i - 1] >= '0' && name[i - 1] <= '9')
            {
                i--;
            }
            if (i >= name.Length)
            {
                return 0;
            }
            int result;
            if (int.TryParse(name.Substring(i), out result))
            {
                return result;
            }
            return 0;
        }

        // Resolves the Screen the widget should be placed on based on cfg.monitor.
        // cfg.monitor <= 0 -> primary. Otherwise the AllScreens entry whose
        // display number matches; falls back to primary.
        private Screen ResolveTargetScreen()
        {
            if (cfg.monitor <= 0)
            {
                return Screen.PrimaryScreen;
            }
            Screen[] screens = Screen.AllScreens;
            for (int i = 0; i < screens.Length; i++)
            {
                if (GetDisplayNumber(screens[i]) == cfg.monitor)
                {
                    return screens[i];
                }
            }
            return Screen.PrimaryScreen;
        }

        public void UpdatePlacement()
        {
            // Re-read the Windows theme; if the palette changed (light/dark
            // toggle or accent-on-taskbar), re-tint the window background and
            // force a full repaint so the card matches the taskbar again.
            if (ThemeManager.Refresh())
            {
                BackColor = ThemeManager.Current.Background;
                UpdateVisual();
            }

            try
            {
                Screen target = ResolveTargetScreen();

                if (ShouldHideForFullscreen(target))
                {
                    SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0,
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_HIDEWINDOW);
                    return;
                }

                if (target.Primary)
                {
                    PlaceOnPrimary(target);
                }
                else
                {
                    PlaceOnSecondary(target);
                }
            }
            catch
            {
            }
            // Placement (SetWindowPos) has set the final position+size above;
            // now push the layered bitmap so it matches the new size.
            UpdateVisual();
        }

        private bool ShouldHideForFullscreen(Screen target)
        {
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero || fg == Handle)
            {
                return false;
            }

            // Never hide for our own windows (settings/setup dialogs). If the
            // foreground window belongs to this process, treat it as non-covering.
            uint fgPid = 0;
            GetWindowThreadProcessId(fg, out fgPid);
            if ((int)fgPid == CurrentProcessId)
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

            // Compare against the absolute bounds of the target screen. Secondary
            // screens have non-zero origins, so we cannot assume a (0,0) origin.
            // Allow a 3-pixel tolerance (physical px) so fullscreen apps whose
            // rect is off by a pixel or two from the screen bounds still count.
            Rectangle bounds = target.Bounds;
            return fr.left <= bounds.Left + 3 && fr.top <= bounds.Top + 3 &&
                fr.right >= bounds.Right - 3 && fr.bottom >= bounds.Bottom - 3;
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

        // Primary display: place over Shell_TrayWnd, reserving the notification
        // area (TrayNotifyWnd) on the right. Behavior is unchanged from the
        // original single-monitor path; if the tray is missing we fall back.
        private void PlaceOnPrimary(Screen target)
        {
            IntPtr tray = FindWindow("Shell_TrayWnd", null);
            if (tray == IntPtr.Zero)
            {
                PlaceFallback(target);
                return;
            }

            float sc = ResolveScale(tray);
            scale = sc;

            RECT trayRect;
            if (!GetWindowRect(tray, out trayRect))
            {
                PlaceFallback(target);
                return;
            }
            int trayH = trayRect.bottom - trayRect.top;

            int h = ComputeBarHeight(trayH, sc);
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

        // Secondary display: locate the Shell_SecondaryTrayWnd whose center lies
        // inside the target screen. Secondary taskbars have no TrayNotifyWnd; the
        // clock occupies ~150 logical px on the right, which we reserve.
        private void PlaceOnSecondary(Screen target)
        {
            IntPtr secondary = FindSecondaryTray(target);
            if (secondary == IntPtr.Zero)
            {
                PlaceFallback(target);
                return;
            }

            float sc = ResolveScale(secondary);
            scale = sc;

            RECT trayRect;
            if (!GetWindowRect(secondary, out trayRect))
            {
                PlaceFallback(target);
                return;
            }
            int trayH = trayRect.bottom - trayRect.top;

            int h = ComputeBarHeight(trayH, sc);
            int w = (int)(cfg.widgetWidth * sc);

            int x;
            if (cfg.position == "left")
            {
                x = trayRect.left + (int)(8 * sc);
            }
            else
            {
                // Reserve the clock strip on the far right of the secondary bar.
                int rightEdge = trayRect.right - (int)(150 * sc);
                x = rightEdge - w - (int)(8 * sc);
            }

            x += (int)(cfg.offsetX * sc);
            int y = trayRect.top + (trayH - h) / 2;

            SetWindowPos(Handle, HWND_TOPMOST, x, y, w, h,
                SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        // Enumerates all Shell_SecondaryTrayWnd top-level windows and returns the
        // one whose rect center falls inside the target screen's bounds.
        private static IntPtr FindSecondaryTray(Screen target)
        {
            Rectangle bounds = target.Bounds;
            IntPtr after = IntPtr.Zero;
            while (true)
            {
                IntPtr h = FindWindowEx(IntPtr.Zero, after, "Shell_SecondaryTrayWnd", null);
                if (h == IntPtr.Zero)
                {
                    break;
                }
                after = h;

                RECT r;
                if (GetWindowRect(h, out r))
                {
                    int cx = r.left + (r.right - r.left) / 2;
                    int cy = r.top + (r.bottom - r.top) / 2;
                    if (cx >= bounds.Left && cx < bounds.Right &&
                        cy >= bounds.Top && cy < bounds.Bottom)
                    {
                        return h;
                    }
                }
            }
            return IntPtr.Zero;
        }

        // Shared bar-height computation used by both taskbar placement paths.
        private static int ComputeBarHeight(int trayH, float sc)
        {
            int h = Math.Min((int)(40 * sc), trayH - (int)(4 * sc));
            if (h < 20)
            {
                h = trayH;
            }
            return h;
        }

        // Overlay fallback on the given screen when no owning taskbar window is
        // found (e.g. taskbar-on-all-displays disabled for a secondary screen).
        private void PlaceFallback(Screen target)
        {
            float sc;
            using (Graphics g = CreateGraphics())
            {
                sc = g.DpiX / 96f;
            }
            scale = sc;

            Rectangle wa = target.WorkingArea;
            Rectangle sb = target.Bounds;
            int w = (int)(cfg.widgetWidth * sc);
            int h = (int)(40 * sc);

            int y;
            if (sb.Bottom > wa.Bottom)
            {
                // Center the widget in the strip between the working area and the
                // screen bottom (the space a taskbar would occupy).
                y = wa.Bottom + (sb.Bottom - wa.Bottom - h) / 2;
            }
            else
            {
                y = sb.Bottom - h - (int)(6 * sc);
            }
            int x = wa.Right - w - (int)(8 * sc) + (int)(cfg.offsetX * sc);

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

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE
        {
            public int cx;
            public int cy;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll", ExactSpelling = true)]
        private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

        [DllImport("gdi32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst,
            IntPtr pptDst, ref SIZE psize, IntPtr hdcSrc, ref POINT pptSrc,
            int crKey, ref BLENDFUNCTION pblend, uint dwFlags);

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

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType,
            IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax,
            IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc,
            uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);
    }
}
