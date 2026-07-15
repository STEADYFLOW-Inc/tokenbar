using System;
using System.Drawing;
using System.Windows.Forms;

namespace ClaudeTokenMeter
{
    // Dark-themed, non-modal settings dialog for TokenBar. Uses plain manual
    // positioning (no designer). C# 5.0 syntax only.
    public class SettingsForm : Form
    {
        // Dark theme colors, matching the widget.
        private static readonly Color BackDark = Color.FromArgb(32, 32, 32);
        private static readonly Color TextLight = Color.FromArgb(230, 230, 230);
        private static readonly Color GroupText = Color.FromArgb(200, 200, 200);
        private static readonly Color InputBack = Color.FromArgb(45, 45, 45);
        private static readonly Color BorderGray = Color.FromArgb(90, 90, 90);

        // Layout constants.
        private const int Pad = 12;
        private const int RowHeight = 26;
        private const int LabelWidth = 130;
        private const int InputLeft = 150;
        private const int InputWidth = 150;

        private readonly Config cfg;
        private readonly MeterAppContext owner;

        private CheckBox chkShowTitle;
        private CheckBox chkShowValueText;
        private CheckBox chkShowResetTime;

        private CheckBox chkBarSession;
        private CheckBox chkBarWeekly;
        private CheckBox chkBarModels;

        private NumericUpDown numWidth;
        private NumericUpDown numOffsetX;
        private ComboBox cmbPosition;
        private NumericUpDown numRefreshSec;

        private Button btnOk;
        private Button btnCancel;

        public SettingsForm(Config cfg, MeterAppContext owner)
        {
            this.cfg = cfg;
            this.owner = owner;

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
            this.ClientSize = new Size(340, 430);
            this.Font = new Font("Segoe UI", 9f);

            BuildControls();
        }

        private void BuildControls()
        {
            int y = Pad;

            // 1. Display group.
            GroupBox displayGroup = MakeGroup(Strings.SettingsDisplayGroup, y, 108);
            this.Controls.Add(displayGroup);

            chkShowTitle = MakeCheck(Strings.SettingsShowTitle, cfg.showTitle, 24);
            chkShowValueText = MakeCheck(Strings.SettingsShowValueText, cfg.showValueText, 24 + RowHeight);
            chkShowResetTime = MakeCheck(Strings.SettingsShowResetTime, cfg.showResetTime, 24 + RowHeight * 2);
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
            barsGroup.Controls.Add(chkBarSession);
            barsGroup.Controls.Add(chkBarWeekly);
            barsGroup.Controls.Add(chkBarModels);

            y = barsGroup.Bottom + Pad;

            // 3. Layout group.
            GroupBox layoutGroup = MakeGroup(Strings.SettingsLayoutGroup, y, 24 + RowHeight * 4 + 8);
            this.Controls.Add(layoutGroup);

            int ly = 24;

            Label lblWidth = MakeLabel(Strings.SettingsWidth, ly);
            numWidth = MakeNumeric(160, 400, ClampInt(cfg.widgetWidth, 160, 400), ly);
            layoutGroup.Controls.Add(lblWidth);
            layoutGroup.Controls.Add(numWidth);
            ly += RowHeight;

            Label lblOffsetX = MakeLabel(Strings.SettingsOffsetX, ly);
            numOffsetX = MakeNumeric(-1000, 1000, ClampInt(cfg.offsetX, -1000, 1000), ly);
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
            layoutGroup.Controls.Add(lblPosition);
            layoutGroup.Controls.Add(cmbPosition);
            ly += RowHeight;

            Label lblRefresh = MakeLabel(Strings.SettingsRefreshSec, ly);
            numRefreshSec = MakeNumeric(10, 3600, ClampInt(cfg.refreshSec, 10, 3600), ly);
            layoutGroup.Controls.Add(lblRefresh);
            layoutGroup.Controls.Add(numRefreshSec);

            // 4. Buttons (bottom-right).
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

            // Write values back into cfg.
            cfg.showTitle = chkShowTitle.Checked;
            cfg.showValueText = chkShowValueText.Checked;
            cfg.showResetTime = chkShowResetTime.Checked;

            cfg.showSessionBar = session;
            cfg.showWeeklyBar = weekly;
            cfg.showModelBars = models;

            cfg.widgetWidth = (int)numWidth.Value;
            cfg.offsetX = (int)numOffsetX.Value;
            cfg.position = cmbPosition.SelectedIndex == 1 ? "left" : "right";
            cfg.refreshSec = (int)numRefreshSec.Value;

            cfg.Save();
            owner.ApplySettings();

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
