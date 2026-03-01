using System;
using System.Drawing;
using System.Windows.Forms;

namespace KocurConsole
{
    public partial class SettingsForm : Form
    {
        public SettingsForm()
        {
            InitializeComponent();

            // Set NumericUpDown ranges
            numFontSize.Minimum = 6;
            numFontSize.Maximum = 72;
            numFontSize.DecimalPlaces = 0;

            numTimeout.Minimum = 5;
            numTimeout.Maximum = 300;
            numTimeout.DecimalPlaces = 0;

            // Populate theme dropdown
            cboTheme.Items.Clear();
            foreach (string name in ThemeManager.GetThemeNames())
                cboTheme.Items.Add(name);

            // Style controls for dark theme
            StyleControls();

            // Load current settings into controls
            LoadSettings();
        }

        private void StyleControls()
        {
            Color bg = Color.FromArgb(45, 45, 45);
            Color fg = Color.White;

            foreach (Control c in this.Controls)
            {
                if (c is TextBox || c is ComboBox || c is NumericUpDown)
                {
                    c.BackColor = bg;
                    c.ForeColor = fg;
                }
                if (c is CheckBox)
                {
                    c.ForeColor = fg;
                }
            }

            btnSave.BackColor = Color.FromArgb(0, 122, 204);
            btnSave.ForeColor = Color.White;
            btnSave.FlatStyle = FlatStyle.Flat;
            btnSave.FlatAppearance.BorderSize = 0;

            btnCancel.BackColor = Color.FromArgb(60, 60, 60);
            btnCancel.ForeColor = Color.White;
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.FlatAppearance.BorderSize = 0;
        }

        private void LoadSettings()
        {
            AppSettings s = SettingsManager.Current;

            // Theme
            int idx = cboTheme.FindStringExact(s.Theme);
            if (idx >= 0) cboTheme.SelectedIndex = idx;

            // Font
            txtFont.Text = s.FontFamily;
            numFontSize.Value = (decimal)s.FontSize;

            // Shell
            int shellIdx = cboShell.FindStringExact(s.Shell);
            if (shellIdx >= 0) cboShell.SelectedIndex = shellIdx;
            else cboShell.SelectedIndex = 0;

            // Timeout
            numTimeout.Value = s.ShellTimeout;

            // Checkboxes
            chkWordWrap.Checked = s.WordWrap;
            chkTimestamps.Checked = s.ShowTimestamps;
            chkAutoScroll.Checked = s.AutoScroll;
        }

        /// <summary>
        /// Called by Form1 after DialogResult.OK to apply settings.
        /// </summary>
        public void ApplySettings()
        {
            // Theme
            if (cboTheme.SelectedItem != null)
                SettingsManager.Set("theme", cboTheme.SelectedItem.ToString());

            // Font
            if (!string.IsNullOrWhiteSpace(txtFont.Text))
                SettingsManager.Set("font", txtFont.Text.Trim());

            SettingsManager.Set("fontSize", numFontSize.Value.ToString());

            // Shell
            if (cboShell.SelectedItem != null)
                SettingsManager.Set("shell", cboShell.SelectedItem.ToString());

            SettingsManager.Set("timeout", numTimeout.Value.ToString());

            // Checkboxes
            SettingsManager.Set("wordWrap", chkWordWrap.Checked.ToString());
            SettingsManager.Set("timestamps", chkTimestamps.Checked.ToString());
            SettingsManager.Set("autoScroll", chkAutoScroll.Checked.ToString());
        }
    }
}
