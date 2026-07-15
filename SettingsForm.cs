using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace ClaudeTokenMeter
{
    // Dark-themed, non-modal settings dialog for TokenBar. Uses plain manual
    // positioning (no designer). C# 5.0 syntax only.
    //
    // The dialog supports live preview: display/bars/layout changes are written
    // into cfg immediately and pushed to the widget via owner.PreviewSettings().
    // A snapshot is taken at open so Cancel/close-without-OK can restore.
    public class SettingsForm : Form
    {
        // Dark theme colors, matching the widget.
        private static readonly Color BackDark = Color.FromArgb(32, 32, 32);
        private static readonly Color TextLight = Color.FromArgb(230, 230, 230);
        private static readonly Color TextDim = Color.FromArgb(140, 140, 140);
        private static readonly Color GroupText = Color.FromArgb(200, 200, 200);
        private static readonly Color InputBack = Color.FromArgb(45, 45, 45);
        private static readonly Color BorderGray = Color.FromArgb(90, 90, 90);

        // Status dot colors.
        private static readonly Color DotGreen = Color.FromArgb(63, 185, 80);
        private static readonly Color DotAmber = Color.FromArgb(210, 153, 34);
        private static readonly Color DotGray = Color.FromArgb(139, 148, 158);

        // Layout constants.
        private const int Pad = 12;
        private const int RowHeight = 26;
        private const int LabelWidth = 130;
        private const int InputLeft = 150;
        private const int InputWidth = 150;

        private readonly Config cfg;
        private readonly MeterAppContext owner;
        private readonly Config snapshot;

        // True while controls are being wired up, to suppress live-preview and
        // startup side-effects during initialization.
        private bool initializing = true;

        private CheckBox chkShowTitle;
        private CheckBox chkShowValueText;
        private CheckBox chkShowResetTime;

        private CheckBox chkBarSession;
        private CheckBox chkBarWeekly;
        private CheckBox chkBarModels;

        private NumericUpDown numWidth;
        private NumericUpDown numOffsetX;
        private ComboBox cmbPosition;
        private NumericUpDown numTokenLimit;
        private NumericUpDown numRefreshSec;

        private CheckBox chkStartup;

        private Panel statusDot;
        private Label statusLabel;
        private System.Windows.Forms.Timer statusTimer;

        private Button btnOk;
        private Button btnCancel;

        public SettingsForm(Config cfg, MeterAppContext owner)
        {
            this.cfg = cfg;
            this.owner = owner;

            // Take a snapshot so Cancel / close-without-OK can restore.
            this.snapshot = new Config();
            this.snapshot.CopyFrom(cfg);

            this.Text = Strings.SettingsTitle;
            this.BackColor = BackDark;
            this.ForeColor = TextLight;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = true;
            // The widget is WS_EX_NOACTIVATE, so a plain Show() from its thread
            // opens behind the foreground app. TopMost guarantees visibility.
            this.TopMost = true;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ClientSize = new Size(348, 560);
            this.Font = new Font("Segoe UI", 9f);

            BuildControls();

            this.FormClosing += SettingsForm_FormClosing;

            initializing = false;

            // Kick off the periodic status refresh once fully built.
            statusTimer = new System.Windows.Forms.Timer();
            statusTimer.Interval = 5000;
            statusTimer.Tick += StatusTimer_Tick;
            statusTimer.Start();
            UpdateStatus();
        }

        private void BuildControls()
        {
            int y = Pad;

            // 0. Header: logo + product name + version.
            BuildHeader(ref y);

            // 1. Display group.
            GroupBox displayGroup = MakeGroup(Strings.SettingsDisplayGroup, y, 108);
            this.Controls.Add(displayGroup);

            chkShowTitle = MakeCheck(Strings.SettingsShowTitle, cfg.showTitle, 24);
            chkShowValueText = MakeCheck(Strings.SettingsShowValueText, cfg.showValueText, 24 + RowHeight);
            chkShowResetTime = MakeCheck(Strings.SettingsShowResetTime, cfg.showResetTime, 24 + RowHeight * 2);
            chkShowTitle.CheckedChanged += Display_Changed;
            chkShowValueText.CheckedChanged += Display_Changed;
            chkShowResetTime.CheckedChanged += Display_Changed;
            displayGroup.Controls.Add(chkShowTitle);
            displayGroup.Controls.Add(chkShowValueText);
            displayGroup.Controls.Add(chkShowResetTime);

            y = displayGroup.Bottom + Pad;

            // 2. Bars group.
            GroupBox barsGroup = MakeGroup(Strings.SettingsBarsGroup, y, 108);
            this.Controls.Add(barsGroup);

            chkBarSession = MakeCheck(Strings.SettingsBarSession, cfg.showSessionBar, 24);
            chkBarWeekly = MakeCheck(Strings.SettingsBarWeekly, cfg.showWeeklyBar, 24 + RowHeight);
            chkBarModels = MakeCheck(Strings.SettingsBarModels, cfg.showModelBars, 24 + RowHeight * 2);
            chkBarSession.CheckedChanged += Bars_Changed;
            chkBarWeekly.CheckedChanged += Bars_Changed;
            chkBarModels.CheckedChanged += Bars_Changed;
            barsGroup.Controls.Add(chkBarSession);
            barsGroup.Controls.Add(chkBarWeekly);
            barsGroup.Controls.Add(chkBarModels);

            y = barsGroup.Bottom + Pad;

            // 3. Layout group (width, offset, position, token limit, refresh).
            GroupBox layoutGroup = MakeGroup(Strings.SettingsLayoutGroup, y, 24 + RowHeight * 5 + 8);
            this.Controls.Add(layoutGroup);

            int ly = 24;

            Label lblWidth = MakeLabel(Strings.SettingsWidth, ly);
            numWidth = MakeNumeric(160, 400, ClampInt(cfg.widgetWidth, 160, 400), ly);
            numWidth.ValueChanged += Layout_Changed;
            layoutGroup.Controls.Add(lblWidth);
            layoutGroup.Controls.Add(numWidth);
            ly += RowHeight;

            Label lblOffsetX = MakeLabel(Strings.SettingsOffsetX, ly);
            numOffsetX = MakeNumeric(-1000, 1000, ClampInt(cfg.offsetX, -1000, 1000), ly);
            numOffsetX.ValueChanged += Layout_Changed;
            layoutGroup.Controls.Add(lblOffsetX);
            layoutGroup.Controls.Add(numOffsetX);
            ly += RowHeight;

            Label lblPosition = MakeLabel(Strings.SettingsPosition, ly);
            cmbPosition = new ComboBox();
            cmbPosition.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbPosition.FlatStyle = FlatStyle.Flat;
            cmbPosition.BackColor = InputBack;
            cmbPosition.ForeColor = TextLight;
            cmbPosition.Left = InputLeft;
            cmbPosition.Top = ly;
            cmbPosition.Width = InputWidth;
            cmbPosition.Items.Add(Strings.SettingsPositionRight);
            cmbPosition.Items.Add(Strings.SettingsPositionLeft);
            cmbPosition.SelectedIndex = cfg.position == "left" ? 1 : 0;
            cmbPosition.SelectedIndexChanged += Layout_Changed;
            layoutGroup.Controls.Add(lblPosition);
            layoutGroup.Controls.Add(cmbPosition);
            ly += RowHeight;

            Label lblTokenLimit = MakeLabel(Strings.SettingsTokenLimit, ly);
            numTokenLimit = new NumericUpDown();
            numTokenLimit.Minimum = 10000;
            numTokenLimit.Maximum = 10000000;
            numTokenLimit.Increment = 10000;
            numTokenLimit.ThousandsSeparator = true;
            numTokenLimit.Value = ClampLong(cfg.tokenLimit, 10000, 10000000);
            numTokenLimit.BackColor = InputBack;
            numTokenLimit.ForeColor = TextLight;
            numTokenLimit.BorderStyle = BorderStyle.FixedSingle;
            numTokenLimit.Left = InputLeft;
            numTokenLimit.Top = ly;
            numTokenLimit.Width = InputWidth;
            layoutGroup.Controls.Add(lblTokenLimit);
            layoutGroup.Controls.Add(numTokenLimit);
            ly += RowHeight;

            Label lblRefresh = MakeLabel(Strings.SettingsRefreshSec, ly);
            numRefreshSec = MakeNumeric(10, 3600, ClampInt(cfg.refreshSec, 10, 3600), ly);
            layoutGroup.Controls.Add(lblRefresh);
            layoutGroup.Controls.Add(numRefreshSec);

            y = layoutGroup.Bottom + Pad;

            // 4. Startup checkbox (standalone, below layout group).
            chkStartup = new CheckBox();
            chkStartup.Text = Strings.SettingsStartup;
            chkStartup.Checked = owner.IsStartupEnabled();
            chkStartup.ForeColor = TextLight;
            chkStartup.BackColor = BackDark;
            chkStartup.FlatStyle = FlatStyle.Flat;
            chkStartup.Left = Pad + 4;
            chkStartup.Top = y;
            chkStartup.Width = this.ClientSize.Width - Pad * 3;
            chkStartup.AutoSize = false;
            chkStartup.Height = 22;
            chkStartup.CheckedChanged += Startup_Changed;
            this.Controls.Add(chkStartup);

            y = chkStartup.Bottom + Pad;

            // 5. Status footer: colored dot + source label.
            statusDot = new Panel();
            statusDot.Left = Pad + 2;
            statusDot.Top = y + 2;
            statusDot.Width = 10;
            statusDot.Height = 10;
            statusDot.BackColor = DotGray;
            MakeDotRound(statusDot);
            this.Controls.Add(statusDot);

            statusLabel = new Label();
            statusLabel.Left = statusDot.Right + 8;
            statusLabel.Top = y;
            statusLabel.Width = this.ClientSize.Width - statusDot.Right - 8 - Pad;
            statusLabel.Height = 18;
            statusLabel.ForeColor = TextDim;
            statusLabel.BackColor = BackDark;
            statusLabel.AutoSize = false;
            statusLabel.Text = Strings.SettingsSourceNone;
            this.Controls.Add(statusLabel);

            // 6. Buttons (bottom-right).
            btnCancel = MakeButton(Strings.SettingsCancel);
            btnCancel.DialogResult = DialogResult.Cancel;
            btnCancel.Top = this.ClientSize.Height - btnCancel.Height - Pad;
            btnCancel.Left = this.ClientSize.Width - btnCancel.Width - Pad;
            this.Controls.Add(btnCancel);

            btnOk = MakeButton(Strings.SettingsOK);
            btnOk.Top = btnCancel.Top;
            btnOk.Left = btnCancel.Left - btnOk.Width - 8;
            btnOk.Click += Ok_Click;
            this.Controls.Add(btnOk);

            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }

        private void BuildHeader(ref int y)
        {
            int headerTop = y;

            PictureBox logo = new PictureBox();
            logo.Left = Pad;
            logo.Top = headerTop;
            logo.Width = 28;
            logo.Height = 28;
            logo.SizeMode = PictureBoxSizeMode.StretchImage;
            logo.BackColor = BackDark;
            Bitmap logoBmp = LoadLogo();
            if (logoBmp != null)
            {
                logo.Image = logoBmp;
            }
            this.Controls.Add(logo);

            Label lblName = new Label();
            lblName.Text = "TokenBar";
            lblName.Font = new Font("Segoe UI", 13f, FontStyle.Bold);
            lblName.ForeColor = TextLight;
            lblName.BackColor = BackDark;
            lblName.AutoSize = true;
            lblName.Left = logo.Right + 8;
            lblName.Top = headerTop + 2;
            this.Controls.Add(lblName);

            Label lblVersion = new Label();
            lblVersion.Text = string.Format(Strings.SettingsVersionFmt, VersionString());
            lblVersion.ForeColor = TextDim;
            lblVersion.BackColor = BackDark;
            lblVersion.AutoSize = true;
            lblVersion.Font = new Font("Segoe UI", 9f);
            lblVersion.Left = lblName.Right + 8;
            lblVersion.Top = headerTop + 10;
            this.Controls.Add(lblVersion);

            y = headerTop + 28 + Pad;
        }

        private static string VersionString()
        {
            try
            {
                Version v = Assembly.GetExecutingAssembly().GetName().Version;
                if (v != null)
                {
                    return v.ToString(3);
                }
            }
            catch (Exception)
            {
            }
            return "1.0.0";
        }

        // Load the embedded logo the same way WidgetForm does: read the manifest
        // resource stream and copy into a fresh Bitmap so the stream can close.
        private static Bitmap LoadLogo()
        {
            try
            {
                Stream resource = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("claude_logo.png");
                if (resource != null)
                {
                    using (resource)
                    using (Bitmap streamBound = new Bitmap(resource))
                    {
                        return new Bitmap(streamBound);
                    }
                }
            }
            catch (Exception)
            {
            }
            return null;
        }

        private static void MakeDotRound(Panel dot)
        {
            try
            {
                GraphicsPath path = new GraphicsPath();
                path.AddEllipse(0, 0, dot.Width, dot.Height);
                dot.Region = new Region(path);
            }
            catch (Exception)
            {
            }
        }

        private GroupBox MakeGroup(string text, int top, int height)
        {
            GroupBox box = new GroupBox();
            box.Text = text;
            box.ForeColor = GroupText;
            box.BackColor = BackDark;
            box.Left = Pad;
            box.Top = top;
            box.Width = this.ClientSize.Width - Pad * 2;
            box.Height = height;
            return box;
        }

        private CheckBox MakeCheck(string text, bool value, int top)
        {
            CheckBox chk = new CheckBox();
            chk.Text = text;
            chk.Checked = value;
            chk.ForeColor = TextLight;
            chk.BackColor = BackDark;
            chk.FlatStyle = FlatStyle.Flat;
            chk.Left = Pad;
            chk.Top = top;
            chk.Width = this.ClientSize.Width - Pad * 4;
            chk.AutoSize = false;
            chk.Height = 22;
            return chk;
        }

        private Label MakeLabel(string text, int top)
        {
            Label lbl = new Label();
            lbl.Text = text;
            lbl.ForeColor = TextLight;
            lbl.BackColor = BackDark;
            lbl.Left = Pad;
            lbl.Top = top + 3;
            lbl.Width = LabelWidth;
            lbl.AutoSize = false;
            return lbl;
        }

        private NumericUpDown MakeNumeric(int min, int max, int value, int top)
        {
            NumericUpDown num = new NumericUpDown();
            num.Minimum = min;
            num.Maximum = max;
            num.Value = value;
            num.BackColor = InputBack;
            num.ForeColor = TextLight;
            num.BorderStyle = BorderStyle.FixedSingle;
            num.Left = InputLeft;
            num.Top = top;
            num.Width = InputWidth;
            return num;
        }

        private Button MakeButton(string text)
        {
            Button btn = new Button();
            btn.Text = text;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderColor = BorderGray;
            btn.BackColor = InputBack;
            btn.ForeColor = Color.White;
            btn.Width = 80;
            btn.Height = 26;
            return btn;
        }

        private static int ClampInt(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }
            if (value > max)
            {
                return max;
            }
            return value;
        }

        private static decimal ClampLong(long value, long min, long max)
        {
            if (value < min)
            {
                return min;
            }
            if (value > max)
            {
                return max;
            }
            return value;
        }

        // --- Live preview handlers ---

        private void Display_Changed(object sender, EventArgs e)
        {
            if (initializing)
            {
                return;
            }
            cfg.showTitle = chkShowTitle.Checked;
            cfg.showValueText = chkShowValueText.Checked;
            cfg.showResetTime = chkShowResetTime.Checked;
            owner.PreviewSettings();
        }

        private void Bars_Changed(object sender, EventArgs e)
        {
            if (initializing)
            {
                return;
            }
            cfg.showSessionBar = chkBarSession.Checked;
            cfg.showWeeklyBar = chkBarWeekly.Checked;
            cfg.showModelBars = chkBarModels.Checked;
            owner.PreviewSettings();
        }

        private void Layout_Changed(object sender, EventArgs e)
        {
            if (initializing)
            {
                return;
            }
            cfg.widgetWidth = (int)numWidth.Value;
            cfg.offsetX = (int)numOffsetX.Value;
            cfg.position = cmbPosition.SelectedIndex == 1 ? "left" : "right";
            owner.PreviewSettings();
        }

        private void Startup_Changed(object sender, EventArgs e)
        {
            if (initializing)
            {
                return;
            }
            owner.SetStartup(chkStartup.Checked);
        }

        // --- Status footer ---

        private void StatusTimer_Tick(object sender, EventArgs e)
        {
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            UsageResult u = owner.LastUsage;

            Color dot;
            string text;

            if (IsNoData(u))
            {
                dot = DotGray;
                text = Strings.SettingsSourceNone;
            }
            else if (u.FromApi && !u.Stale)
            {
                dot = DotGreen;
                text = Strings.SettingsSourceApi;
            }
            else if (u.FromApi && u.Stale)
            {
                dot = DotAmber;
                text = Strings.SettingsSourceApiStale;
            }
            else
            {
                dot = DotGray;
                text = Strings.SettingsSourceLocal;
            }

            if (statusDot != null)
            {
                statusDot.BackColor = dot;
            }
            if (statusLabel != null)
            {
                statusLabel.Text = text;
            }
        }

        private static bool IsNoData(UsageResult u)
        {
            if (u == null)
            {
                return true;
            }
            return !u.FromApi
                && !u.Active
                && u.Error == null
                && u.UsedTokens == 0
                && u.LastActivityUtc == null;
        }

        // --- Cancel / close-without-OK restores the snapshot ---

        private void SettingsForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (statusTimer != null)
            {
                statusTimer.Stop();
                statusTimer.Dispose();
                statusTimer = null;
            }

            if (this.DialogResult != DialogResult.OK)
            {
                // Restore the pre-open state and push it to the widget (no save).
                cfg.CopyFrom(snapshot);
                owner.PreviewSettings();
            }
        }

        private void Ok_Click(object sender, EventArgs e)
        {
            // Validate: at least one bar must be visible.
            bool session = chkBarSession.Checked;
            bool weekly = chkBarWeekly.Checked;
            bool models = chkBarModels.Checked;
            if (!session && !weekly && !models)
            {
                session = true;
                chkBarSession.Checked = true;
            }

            // Final write-back of all controls (live preview already wrote most).
            cfg.showTitle = chkShowTitle.Checked;
            cfg.showValueText = chkShowValueText.Checked;
            cfg.showResetTime = chkShowResetTime.Checked;

            cfg.showSessionBar = session;
            cfg.showWeeklyBar = weekly;
            cfg.showModelBars = models;

            cfg.widgetWidth = (int)numWidth.Value;
            cfg.offsetX = (int)numOffsetX.Value;
            cfg.position = cmbPosition.SelectedIndex == 1 ? "left" : "right";
            cfg.tokenLimit = (long)numTokenLimit.Value;
            cfg.refreshSec = (int)numRefreshSec.Value;

            cfg.Save();
            owner.ApplySettings();

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
