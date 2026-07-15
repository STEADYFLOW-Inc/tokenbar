using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace ClaudeTokenMeter
{
    // Dark-themed, non-modal first-run quick-setup dialog ("簡単セットアップ").
    // Lets the user confirm the Claude directory, monitor, taskbar position, and
    // startup registration on first launch. Everything can still be changed later
    // from the full settings window. Manual positioning, no designer.
    // C# 5.0 syntax only.
    public class SetupForm : Form
    {
        // Dark theme colors, matching SettingsForm.
        private static readonly Color BackDark = Color.FromArgb(32, 32, 32);
        private static readonly Color TextLight = Color.FromArgb(230, 230, 230);
        private static readonly Color TextDim = Color.FromArgb(170, 170, 170);
        private static readonly Color InputBack = Color.FromArgb(45, 45, 45);
        private static readonly Color BorderGray = Color.FromArgb(90, 90, 90);
        private static readonly Color CredOkGreen = Color.FromArgb(63, 185, 80);
        private static readonly Color CredAmber = Color.FromArgb(210, 153, 34);
        private static readonly Color AccentBack = Color.FromArgb(0, 120, 100);

        private const int Pad = 16;

        private readonly Config cfg;
        private readonly MeterAppContext owner;

        // The %USERPROFILE%\.claude default path, used to decide whether to
        // persist an explicit claudeDir or keep the default-resolution behavior.
        private readonly string defaultClaudeDir;

        private TextBox txtClaudeDir;
        private Label lblCred;
        private MonitorPicker monitorPicker;
        private ComboBox cmbPosition;
        private CheckBox chkStartup;

        // True once the Start button has committed the settings, so FormClosing
        // does not run the "dismissed without Start" path.
        private bool committed = false;

        public SetupForm(Config cfg, MeterAppContext owner)
        {
            this.cfg = cfg;
            this.owner = owner;

            this.defaultClaudeDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".claude");

            this.Text = Strings.SetupTitle;
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
            this.ClientSize = new Size(380, 470);
            this.Font = new Font("Segoe UI", 9f);

            BuildControls();

            this.FormClosing += SetupForm_FormClosing;
        }

        private void BuildControls()
        {
            int contentWidth = this.ClientSize.Width - Pad * 2;
            int y = Pad;

            // 1. Header: logo + product name.
            PictureBox logo = new PictureBox();
            logo.Left = Pad;
            logo.Top = y;
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
            lblName.Top = y + 4;
            this.Controls.Add(lblName);

            y = logo.Bottom + Pad;

            // 2. Welcome text (multi-line, dim).
            Label lblWelcome = new Label();
            lblWelcome.Text = Strings.SetupWelcome;
            lblWelcome.ForeColor = TextDim;
            lblWelcome.BackColor = BackDark;
            lblWelcome.AutoSize = false;
            lblWelcome.Left = Pad;
            lblWelcome.Top = y;
            lblWelcome.Width = contentWidth;
            lblWelcome.Height = 40;
            this.Controls.Add(lblWelcome);
            y = lblWelcome.Bottom + Pad;

            // 3. Claude directory row.
            Label lblDir = new Label();
            lblDir.Text = Strings.SetupClaudeDir;
            lblDir.ForeColor = TextLight;
            lblDir.BackColor = BackDark;
            lblDir.AutoSize = false;
            lblDir.Left = Pad;
            lblDir.Top = y;
            lblDir.Width = contentWidth;
            lblDir.Height = 18;
            this.Controls.Add(lblDir);
            y = lblDir.Bottom + 2;

            Button btnBrowse = new Button();
            btnBrowse.Text = Strings.SetupBrowse;
            btnBrowse.FlatStyle = FlatStyle.Flat;
            btnBrowse.FlatAppearance.BorderColor = BorderGray;
            btnBrowse.BackColor = InputBack;
            btnBrowse.ForeColor = TextLight;
            btnBrowse.Width = 72;
            btnBrowse.Height = 24;
            btnBrowse.Top = y;
            btnBrowse.Left = Pad + contentWidth - btnBrowse.Width;
            btnBrowse.Click += Browse_Click;
            this.Controls.Add(btnBrowse);

            txtClaudeDir = new TextBox();
            txtClaudeDir.BackColor = InputBack;
            txtClaudeDir.ForeColor = TextLight;
            txtClaudeDir.BorderStyle = BorderStyle.FixedSingle;
            txtClaudeDir.Left = Pad;
            txtClaudeDir.Top = y + 1;
            txtClaudeDir.Width = contentWidth - btnBrowse.Width - 8;
            txtClaudeDir.Text = cfg.ResolveClaudeDir();
            txtClaudeDir.TextChanged += ClaudeDir_Changed;
            this.Controls.Add(txtClaudeDir);
            y = btnBrowse.Bottom + 4;

            // Live credential validation label.
            lblCred = new Label();
            lblCred.AutoSize = false;
            lblCred.Left = Pad;
            lblCred.Top = y;
            lblCred.Width = contentWidth;
            lblCred.Height = 18;
            lblCred.BackColor = BackDark;
            this.Controls.Add(lblCred);
            y = lblCred.Bottom + Pad;

            UpdateCredStatus();

            // 4. Monitor picker.
            Label lblMonitor = new Label();
            lblMonitor.Text = Strings.SetupMonitorLabel;
            lblMonitor.ForeColor = TextLight;
            lblMonitor.BackColor = BackDark;
            lblMonitor.AutoSize = false;
            lblMonitor.Left = Pad;
            lblMonitor.Top = y;
            lblMonitor.Width = contentWidth;
            lblMonitor.Height = 18;
            this.Controls.Add(lblMonitor);
            y = lblMonitor.Bottom + 2;

            monitorPicker = new MonitorPicker();
            monitorPicker.Left = Pad;
            monitorPicker.Top = y;
            monitorPicker.Width = contentWidth;
            monitorPicker.Height = 100;
            monitorPicker.SetSelectedNumber(cfg.monitor);
            this.Controls.Add(monitorPicker);
            y = monitorPicker.Bottom + Pad;

            // 5. Position combo.
            Label lblPosition = new Label();
            lblPosition.Text = Strings.SetupPositionLabel;
            lblPosition.ForeColor = TextLight;
            lblPosition.BackColor = BackDark;
            lblPosition.AutoSize = false;
            lblPosition.Left = Pad;
            lblPosition.Top = y + 3;
            lblPosition.Width = 150;
            lblPosition.Height = 20;
            this.Controls.Add(lblPosition);

            cmbPosition = new ComboBox();
            cmbPosition.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbPosition.FlatStyle = FlatStyle.Flat;
            cmbPosition.BackColor = InputBack;
            cmbPosition.ForeColor = TextLight;
            cmbPosition.Left = Pad + 160;
            cmbPosition.Top = y;
            cmbPosition.Width = contentWidth - 160;
            cmbPosition.Items.Add(Strings.SettingsPositionRight);
            cmbPosition.Items.Add(Strings.SettingsPositionLeft);
            cmbPosition.SelectedIndex = cfg.position == "left" ? 1 : 0;
            this.Controls.Add(cmbPosition);
            y = cmbPosition.Bottom + Pad;

            // 6. Startup checkbox (default CHECKED).
            chkStartup = new CheckBox();
            chkStartup.Text = Strings.SetupStartup;
            chkStartup.Checked = true;
            chkStartup.ForeColor = TextLight;
            chkStartup.BackColor = BackDark;
            chkStartup.FlatStyle = FlatStyle.Flat;
            chkStartup.AutoSize = false;
            chkStartup.Left = Pad;
            chkStartup.Top = y;
            chkStartup.Width = contentWidth;
            chkStartup.Height = 22;
            this.Controls.Add(chkStartup);
            y = chkStartup.Bottom + Pad;

            // 7. Accent Start button (bottom-right).
            Button btnStart = new Button();
            btnStart.Text = Strings.SetupStart;
            btnStart.FlatStyle = FlatStyle.Flat;
            btnStart.FlatAppearance.BorderColor = AccentBack;
            btnStart.BackColor = AccentBack;
            btnStart.ForeColor = Color.White;
            btnStart.Width = 96;
            btnStart.Height = 30;
            btnStart.Click += Start_Click;
            this.Controls.Add(btnStart);

            // Size the form to fit the content, then anchor the Start button.
            int desiredHeight = y + btnStart.Height + Pad;
            this.ClientSize = new Size(this.ClientSize.Width, desiredHeight);

            btnStart.Top = this.ClientSize.Height - btnStart.Height - Pad;
            btnStart.Left = this.ClientSize.Width - btnStart.Width - Pad;

            this.AcceptButton = btnStart;
        }

        // --- Claude directory validation ---

        private void ClaudeDir_Changed(object sender, EventArgs e)
        {
            UpdateCredStatus();
        }

        // Live-updates the credential label based on the current textbox path.
        // Treats any IO exception as "credentials missing".
        private void UpdateCredStatus()
        {
            bool found = false;
            try
            {
                string path = txtClaudeDir.Text != null ? txtClaudeDir.Text.Trim() : "";
                if (path.Length > 0)
                {
                    found = File.Exists(Path.Combine(path, ".credentials.json"));
                }
            }
            catch (Exception)
            {
                found = false;
            }

            if (found)
            {
                lblCred.Text = Strings.SetupCredOk;
                lblCred.ForeColor = CredOkGreen;
            }
            else
            {
                lblCred.Text = Strings.SetupCredMissing;
                lblCred.ForeColor = CredAmber;
            }
        }

        private void Browse_Click(object sender, EventArgs e)
        {
            try
            {
                using (FolderBrowserDialog dlg = new FolderBrowserDialog())
                {
                    string seed = txtClaudeDir.Text != null ? txtClaudeDir.Text.Trim() : "";
                    if (seed.Length > 0 && Directory.Exists(seed))
                    {
                        dlg.SelectedPath = seed;
                    }
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        txtClaudeDir.Text = dlg.SelectedPath;
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        // --- Commit ---

        private void Start_Click(object sender, EventArgs e)
        {
            string typed = txtClaudeDir.Text != null ? txtClaudeDir.Text.Trim() : "";

            // If the user kept the default %USERPROFILE%\.claude path, store "" so
            // the default-resolution behavior is preserved; otherwise persist it.
            if (typed.Length == 0 ||
                string.Equals(typed, defaultClaudeDir, StringComparison.OrdinalIgnoreCase))
            {
                cfg.claudeDir = "";
            }
            else
            {
                cfg.claudeDir = typed;
            }

            if (monitorPicker != null)
            {
                cfg.monitor = monitorPicker.GetSelectedNumber();
            }
            cfg.position = cmbPosition.SelectedIndex == 1 ? "left" : "right";
            cfg.setupDone = true;
            cfg.Save();

            if (chkStartup.Checked)
            {
                owner.SetStartup(true);
            }

            committed = true;

            owner.ApplySettings();
            this.Close();
        }

        // X-closed without pressing Start: still mark setup done (don't nag again)
        // but make no other changes.
        private void SetupForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!committed)
            {
                cfg.setupDone = true;
                cfg.Save();
            }
        }

        // Load the embedded logo the same way SettingsForm/WidgetForm do: read the
        // manifest resource stream and copy into a fresh Bitmap so the stream can close.
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
    }
}
