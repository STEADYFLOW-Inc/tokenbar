using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
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

        // Per-model selection layout.
        private const int ModelIndentLeft = 36;   // indent under chkBarModels
        private const int ModelRowHeight = 22;
        private const int ModelHintHeight = 18;
        // Bars group base height (3 top-level checkboxes + padding), before the
        // hint label and any per-model rows are added below chkBarModels.
        private const int BarsBaseHeight = 108;
        // Max overall form height; realistic model count is 1-4 so this is a guard.
        // Must leave room for preview (48) + monitor picker (92) + all groups.
        private const int MaxFormHeight = 946;

        private readonly Config cfg;
        private readonly MeterAppContext owner;
        private readonly Config snapshot;

        // True while controls are being wired up, to suppress live-preview and
        // startup side-effects during initialization.
        private bool initializing = true;

        private CheckBox chkShowTitle;
        private CheckBox chkShowValueText;
        private CheckBox chkShowResetTime;

        private CheckBox chkUseApi;

        private CheckBox chkBarSession;
        private CheckBox chkBarWeekly;
        private CheckBox chkBarModels;

        // Data source group (between bars and layout groups).
        private GroupBox dataGroup;

        // Bars group + dynamic per-model selection.
        private GroupBox barsGroup;
        private Label lblModelsHint;
        private Label lblModelsNone;
        private readonly List<CheckBox> modelChecks = new List<CheckBox>();
        // Joined snapshot of the model-name set the list was last built from,
        // used to detect changes on the periodic status refresh.
        private string lastModelNamesJoined = null;

        // Controls below the bars group, re-flowed when the bars group grows.
        private GroupBox layoutGroup;

        private NumericUpDown numWidth;
        private NumericUpDown numOffsetX;
        private ComboBox cmbPosition;
        private MonitorPicker monitorPicker;
        private NumericUpDown numRefreshSec;

        // Live widget preview (mirrors the on-taskbar widget card).
        private Label previewCaption;
        private BufferedPanel previewPanel;

        private CheckBox chkStartup;

        private Panel statusDot;
        private Label statusLabel;
        private System.Windows.Forms.Timer statusTimer;

        private Button btnOk;
        private Button btnCancel;

        // A double-buffered Panel used for flicker-free custom painting (the live
        // widget preview and the monitor picker draw continuously as settings
        // change). ResizeRedraw so a size change repaints the whole surface.
        private sealed class BufferedPanel : Panel
        {
            public BufferedPanel()
            {
                DoubleBuffered = true;
                ResizeRedraw = true;
            }
        }

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

            // 0.5 Live widget preview: a dim caption + a custom-painted panel
            // that mirrors the on-taskbar widget card in real time.
            previewCaption = new Label();
            previewCaption.Text = Strings.SettingsPreview;
            previewCaption.Font = new Font("Segoe UI", 8.5f);
            previewCaption.ForeColor = TextDim;
            previewCaption.BackColor = BackDark;
            previewCaption.AutoSize = false;
            previewCaption.Left = Pad;
            previewCaption.Top = y;
            previewCaption.Width = this.ClientSize.Width - Pad * 2;
            previewCaption.Height = 16;
            this.Controls.Add(previewCaption);
            y = previewCaption.Bottom + 2;

            previewPanel = new BufferedPanel();
            previewPanel.Left = Pad;
            previewPanel.Top = y;
            previewPanel.Width = this.ClientSize.Width - Pad * 2;
            previewPanel.Height = 48;
            previewPanel.BackColor = Color.FromArgb(24, 24, 24);
            previewPanel.Paint += PreviewPanel_Paint;
            this.Controls.Add(previewPanel);
            y = previewPanel.Bottom + Pad;

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

            // 2. Bars group. Height is grown later by RebuildModelList to fit the
            // per-model selection rows.
            barsGroup = MakeGroup(Strings.SettingsBarsGroup, y, BarsBaseHeight);
            this.Controls.Add(barsGroup);

            chkBarSession = MakeCheck(Strings.SettingsBarSession, cfg.showSessionBar, 24);
            chkBarWeekly = MakeCheck(Strings.SettingsBarWeekly, cfg.showWeeklyBar, 24 + RowHeight);
            chkBarModels = MakeCheck(Strings.SettingsBarModels, cfg.showModelBars, 24 + RowHeight * 2);
            chkBarSession.CheckedChanged += Bars_Changed;
            chkBarWeekly.CheckedChanged += Bars_Changed;
            chkBarModels.CheckedChanged += Bars_Changed;
            // Toggling the per-model master checkbox enables/disables the model rows.
            chkBarModels.CheckedChanged += BarModels_CheckedChanged;
            barsGroup.Controls.Add(chkBarSession);
            barsGroup.Controls.Add(chkBarWeekly);
            barsGroup.Controls.Add(chkBarModels);

            // Hint label under chkBarModels (dim). The per-model checkboxes are
            // (re)built by RebuildModelList, which also sets the group height.
            lblModelsHint = new Label();
            lblModelsHint.Text = Strings.SettingsModelsHint;
            lblModelsHint.Font = new Font("Segoe UI", 8.5f);
            lblModelsHint.ForeColor = TextDim;
            lblModelsHint.BackColor = BackDark;
            lblModelsHint.AutoSize = false;
            lblModelsHint.Left = ModelIndentLeft;
            lblModelsHint.Top = 24 + RowHeight * 3 + 2;
            lblModelsHint.Width = barsGroup.Width - ModelIndentLeft - Pad;
            lblModelsHint.Height = ModelHintHeight;
            barsGroup.Controls.Add(lblModelsHint);

            RebuildModelList();

            y = barsGroup.Bottom + Pad;

            // 2.5 Data source group: "use API" toggle + clean-mode hint.
            // Height 86: group header (~20) + checkbox row (22+4) + hint label (30+4) + pad (6).
            dataGroup = MakeGroup(Strings.SettingsDataGroup, y, 86);
            this.Controls.Add(dataGroup);

            chkUseApi = MakeCheck(Strings.SettingsUseApi, cfg.useApi, 18);
            chkUseApi.CheckedChanged += UseApi_Changed;
            dataGroup.Controls.Add(chkUseApi);

            Label lblCleanHint = new Label();
            lblCleanHint.Text = Strings.SettingsCleanModeHint;
            lblCleanHint.Font = new Font("Segoe UI", 8.5f);
            lblCleanHint.ForeColor = TextDim;
            lblCleanHint.BackColor = BackDark;
            lblCleanHint.AutoSize = false;
            lblCleanHint.Left = Pad;
            lblCleanHint.Top = chkUseApi.Bottom + 4;
            lblCleanHint.Width = dataGroup.Width - Pad * 2;
            lblCleanHint.Height = 30;
            dataGroup.Controls.Add(lblCleanHint);

            y = dataGroup.Bottom + Pad;

            // 3. Layout group (width, offset, position, monitor picker, refresh).
            // Base height covers the 4 combo/numeric rows + the monitor label +
            // the ~92px picker + bottom padding.
            const int MonitorPickerHeight = 92;
            layoutGroup = MakeGroup(Strings.SettingsLayoutGroup, y,
                24 + RowHeight * 4 + 20 + MonitorPickerHeight + 8);
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

            // Monitor: a full-width visual picker (Windows display-settings style)
            // instead of a combo. Label on its own line, picker spanning below.
            Label lblMonitor = MakeLabel(Strings.SettingsMonitor, ly);
            lblMonitor.Width = layoutGroup.Width - Pad * 2;
            layoutGroup.Controls.Add(lblMonitor);
            ly += 20;

            monitorPicker = new MonitorPicker();
            monitorPicker.Left = Pad;
            monitorPicker.Top = ly;
            monitorPicker.Width = layoutGroup.Width - Pad * 2;
            monitorPicker.Height = MonitorPickerHeight;
            monitorPicker.SetSelectedNumber(cfg.monitor);
            monitorPicker.SelectionChanged += Monitor_Changed;
            layoutGroup.Controls.Add(monitorPicker);
            ly += MonitorPickerHeight + 4;

            Label lblRefresh = MakeLabel(Strings.SettingsRefreshSec, ly);
            numRefreshSec = MakeNumeric(10, 3600, ClampInt(cfg.refreshSec, 10, 3600), ly);
            layoutGroup.Controls.Add(lblRefresh);
            layoutGroup.Controls.Add(numRefreshSec);

            // 4. Startup checkbox (standalone, below layout group).
            chkStartup = new CheckBox();
            chkStartup.Text = Strings.SettingsStartup;
            chkStartup.Checked = owner.IsStartupEnabled();
            chkStartup.ForeColor = TextLight;
            chkStartup.BackColor = BackDark;
            chkStartup.FlatStyle = FlatStyle.Flat;
            chkStartup.Left = Pad + 4;
            chkStartup.Width = this.ClientSize.Width - Pad * 3;
            chkStartup.AutoSize = false;
            chkStartup.Height = 22;
            chkStartup.CheckedChanged += Startup_Changed;
            this.Controls.Add(chkStartup);

            // 5. Status footer: colored dot + source label.
            statusDot = new Panel();
            statusDot.Left = Pad + 2;
            statusDot.Width = 10;
            statusDot.Height = 10;
            statusDot.BackColor = DotGray;
            MakeDotRound(statusDot);
            this.Controls.Add(statusDot);

            statusLabel = new Label();
            statusLabel.Left = statusDot.Right + 8;
            statusLabel.Height = 18;
            statusLabel.Width = this.ClientSize.Width - statusDot.Right - 8 - Pad;
            statusLabel.ForeColor = TextDim;
            statusLabel.BackColor = BackDark;
            statusLabel.AutoSize = false;
            statusLabel.Text = Strings.SettingsSourceNone;
            this.Controls.Add(statusLabel);

            // 6. Buttons (bottom-right). Positioned relative to ClientSize by
            // RelayoutBelow so they track the (possibly grown) form height.
            btnCancel = MakeButton(Strings.SettingsCancel);
            btnCancel.DialogResult = DialogResult.Cancel;
            this.Controls.Add(btnCancel);

            btnOk = MakeButton(Strings.SettingsOK);
            btnOk.Click += Ok_Click;
            this.Controls.Add(btnOk);

            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;

            // Position the layout group and everything below it, and size the form.
            RelayoutBelow();
        }

        // Positions the layout group, startup checkbox, status footer, and
        // buttons relative to the (possibly grown) bars group, then sizes the
        // form to fit. Safe to call repeatedly after the bars group height
        // changes (e.g. when the per-model list is rebuilt).
        private void RelayoutBelow()
        {
            if (layoutGroup == null)
            {
                return;
            }

            // If the data-source group exists, it sits between bars and layout.
            if (dataGroup != null)
            {
                dataGroup.Top = barsGroup.Bottom + Pad;
                layoutGroup.Top = dataGroup.Bottom + Pad;
            }
            else
            {
                layoutGroup.Top = barsGroup.Bottom + Pad;
            }

            int y = layoutGroup.Top;

            y = layoutGroup.Bottom + Pad;

            if (chkStartup != null)
            {
                chkStartup.Top = y;
                y = chkStartup.Bottom + Pad;
            }

            if (statusDot != null)
            {
                statusDot.Top = y + 2;
            }
            if (statusLabel != null)
            {
                statusLabel.Top = y;
            }

            y += 18 + Pad;

            // Form height grows with the content; cap as a guard for many models.
            int desiredHeight = y + 26 + Pad; // room for the button row + bottom pad.
            if (desiredHeight > MaxFormHeight)
            {
                desiredHeight = MaxFormHeight;
            }
            this.ClientSize = new Size(this.ClientSize.Width, desiredHeight);

            if (btnCancel != null)
            {
                btnCancel.Top = this.ClientSize.Height - btnCancel.Height - Pad;
                btnCancel.Left = this.ClientSize.Width - btnCancel.Width - Pad;
            }
            if (btnOk != null && btnCancel != null)
            {
                btnOk.Top = btnCancel.Top;
                btnOk.Left = btnCancel.Left - btnOk.Width - 8;
            }
        }

        // --- Per-model selection ---

        // Distinct, non-null model names from the latest usage snapshot, in
        // first-seen order.
        private List<string> GetAvailableModels()
        {
            List<string> models = new List<string>();
            UsageResult u = owner.LastUsage;
            if (u == null || u.ScopedLimits == null)
            {
                return models;
            }
            foreach (ScopedLimit sl in u.ScopedLimits)
            {
                if (sl == null || sl.Model == null)
                {
                    continue;
                }
                bool seen = false;
                foreach (string existing in models)
                {
                    if (string.Equals(existing, sl.Model, StringComparison.OrdinalIgnoreCase))
                    {
                        seen = true;
                        break;
                    }
                }
                if (!seen)
                {
                    models.Add(sl.Model);
                }
            }
            return models;
        }

        private static bool SelectedContains(string[] selected, string model)
        {
            if (selected == null)
            {
                return false;
            }
            foreach (string s in selected)
            {
                if (string.Equals(s, model, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        // (Re)builds the indented per-model checkbox list (or the "none" label)
        // under chkBarModels, grows the bars group to fit, and re-flows the
        // controls below. Preserves the config-driven check state logic.
        private void RebuildModelList()
        {
            bool wasInitializing = initializing;
            initializing = true;
            try
            {
                // Remove any previously built model checkboxes / none label.
                foreach (CheckBox old in modelChecks)
                {
                    barsGroup.Controls.Remove(old);
                    old.Dispose();
                }
                modelChecks.Clear();

                if (lblModelsNone != null)
                {
                    barsGroup.Controls.Remove(lblModelsNone);
                    lblModelsNone.Dispose();
                    lblModelsNone = null;
                }

                List<string> models = GetAvailableModels();
                lastModelNamesJoined = string.Join("", models.ToArray());

                int rowTop = lblModelsHint.Bottom + 2;

                if (models.Count == 0)
                {
                    lblModelsNone = new Label();
                    lblModelsNone.Text = Strings.SettingsModelsNone;
                    lblModelsNone.Font = new Font("Segoe UI", 8.5f);
                    lblModelsNone.ForeColor = TextDim;
                    lblModelsNone.BackColor = BackDark;
                    lblModelsNone.AutoSize = false;
                    lblModelsNone.Left = ModelIndentLeft;
                    lblModelsNone.Top = rowTop;
                    lblModelsNone.Width = barsGroup.Width - ModelIndentLeft - Pad;
                    lblModelsNone.Height = ModelRowHeight;
                    barsGroup.Controls.Add(lblModelsNone);
                    rowTop = lblModelsNone.Bottom;
                }
                else
                {
                    bool selectAll = cfg.selectedModels == null || cfg.selectedModels.Length == 0;
                    for (int i = 0; i < models.Count; i++)
                    {
                        string model = models[i];
                        CheckBox chk = new CheckBox();
                        chk.Text = model;
                        chk.Checked = selectAll || SelectedContains(cfg.selectedModels, model);
                        chk.ForeColor = TextLight;
                        chk.BackColor = BackDark;
                        chk.FlatStyle = FlatStyle.Flat;
                        chk.AutoSize = false;
                        chk.Left = ModelIndentLeft;
                        chk.Top = rowTop;
                        chk.Width = barsGroup.Width - ModelIndentLeft - Pad;
                        chk.Height = ModelRowHeight;
                        chk.Enabled = chkBarModels.Checked;
                        chk.CheckedChanged += ModelCheck_Changed;
                        barsGroup.Controls.Add(chk);
                        modelChecks.Add(chk);
                        rowTop += ModelRowHeight;
                    }
                }

                // Grow the bars group to fit hint + rows.
                barsGroup.Height = rowTop + Pad;

                RelayoutBelow();
            }
            finally
            {
                initializing = wasInitializing;
            }
        }

        // Master toggle: enable/disable the per-model checkboxes with chkBarModels.
        private void BarModels_CheckedChanged(object sender, EventArgs e)
        {
            foreach (CheckBox chk in modelChecks)
            {
                chk.Enabled = chkBarModels.Checked;
            }
        }

        // A per-model checkbox changed: rebuild cfg.selectedModels and preview.
        private void ModelCheck_Changed(object sender, EventArgs e)
        {
            if (initializing)
            {
                return;
            }
            cfg.selectedModels = ComputeSelectedModels();
            owner.PreviewSettings();
            InvalidatePreview();
        }

        // Builds the selectedModels array from the current checkbox state.
        // If every available model is checked, returns an empty array so future
        // models are auto-included; otherwise the checked model names.
        private string[] ComputeSelectedModels()
        {
            List<string> checkedNames = new List<string>();
            bool allChecked = true;
            foreach (CheckBox chk in modelChecks)
            {
                if (chk.Checked)
                {
                    checkedNames.Add(chk.Text);
                }
                else
                {
                    allChecked = false;
                }
            }
            if (modelChecks.Count == 0 || allChecked)
            {
                return new string[0];
            }
            return checkedNames.ToArray();
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

        // --- Monitor selector ---

        // The visual picker raised SelectionChanged: write the selected display
        // number into cfg, push the live preview, and refresh the preview panel.
        private void Monitor_Changed(object sender, EventArgs e)
        {
            if (initializing)
            {
                return;
            }
            if (monitorPicker != null)
            {
                cfg.monitor = monitorPicker.GetSelectedNumber();
                owner.PreviewSettings();
                InvalidatePreview();
            }
        }

        // --- Live widget preview ---

        // Repaints the preview backdrop and draws the widget card centered via
        // the shared CardRenderer, so the panel always mirrors the real widget.
        private void PreviewPanel_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int panelW = previewPanel.Width;
            int panelH = previewPanel.Height;

            // Subtle backdrop.
            using (SolidBrush back = new SolidBrush(Color.FromArgb(24, 24, 24)))
            {
                g.FillRectangle(back, 0, 0, panelW, panelH);
            }

            // Card sized to the configured widget width (clamped to the panel),
            // fixed 40px tall, drawn at scale 1 and centered.
            int cardW = cfg.widgetWidth;
            int maxW = panelW - 8;
            if (cardW > maxW)
            {
                cardW = maxW;
            }
            if (cardW < 1)
            {
                cardW = 1;
            }
            int cardH = 40;

            int cardX = (panelW - cardW) / 2;
            int cardY = (panelH - cardH) / 2;

            UsageResult u = owner.LastUsage != null ? owner.LastUsage : new UsageResult();

            GraphicsState state = g.Save();
            try
            {
                g.TranslateTransform(cardX, cardY);
                CardRenderer.Draw(g, cfg, u, 1f, cardW, cardH, false);
            }
            finally
            {
                g.Restore(state);
            }

            // Thin border around the panel.
            using (Pen borderPen = new Pen(Color.FromArgb(60, 60, 60), 1f))
            {
                g.DrawRectangle(borderPen, 0, 0, panelW - 1, panelH - 1);
            }
        }

        // Forces the preview panel to repaint (mirrors any cfg / usage change).
        private void InvalidatePreview()
        {
            if (previewPanel != null)
            {
                previewPanel.Invalidate();
            }
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
            InvalidatePreview();
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
            InvalidatePreview();
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
            InvalidatePreview();
        }

        private void UseApi_Changed(object sender, EventArgs e)
        {
            if (initializing)
            {
                return;
            }
            cfg.useApi = chkUseApi.Checked;
            owner.PreviewSettings();
            InvalidatePreview();
            owner.RefreshData();
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
            // Mirror any usage-data change (source dot, bars, percentages) into
            // the live preview panel on every refresh tick.
            InvalidatePreview();
        }

        private void UpdateStatus()
        {
            UsageResult u = owner.LastUsage;

            // If the set of available model names changed since the list was
            // built, rebuild the per-model checkbox list.
            List<string> currentModels = GetAvailableModels();
            string currentJoined = string.Join("", currentModels.ToArray());
            if (currentJoined != lastModelNamesJoined)
            {
                RebuildModelList();
            }

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
            else if (u != null && u.AuthExpired)
            {
                dot = DotAmber;
                text = Strings.SettingsSourceAuthExpired;
            }
            else if (u.FromApi && u.Stale)
            {
                dot = DotAmber;
                text = Strings.SettingsSourceApiStale;
            }
            else if (!cfg.useApi)
            {
                dot = DotGray;
                text = Strings.SettingsSourceLocalClean;
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

            // Ensure the per-model selection reflects the current checkbox state.
            cfg.selectedModels = ComputeSelectedModels();

            cfg.widgetWidth = (int)numWidth.Value;
            cfg.offsetX = (int)numOffsetX.Value;
            cfg.position = cmbPosition.SelectedIndex == 1 ? "left" : "right";
            if (monitorPicker != null)
            {
                cfg.monitor = monitorPicker.GetSelectedNumber();
            }
            cfg.refreshSec = (int)numRefreshSec.Value;

            if (chkUseApi != null)
            {
                cfg.useApi = chkUseApi.Checked;
            }

            cfg.Save();
            owner.ApplySettings();

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
