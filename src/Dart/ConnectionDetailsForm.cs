namespace Dart;

internal sealed class ConnectionDetailsForm : Form
{
    readonly Func<CameraSettings?> getSettings;
    readonly Func<string> getStatus;
    readonly Dictionary<string, TextBox> values = new();
    readonly System.Windows.Forms.Timer refresh = new() { Interval = 500 };
    readonly Label state = new() { AutoSize = true, ForeColor = Color.FromArgb(70, 80, 95), MaximumSize = new Size(660, 0) };
    readonly Label feedback = new() { AutoSize = true, ForeColor = Color.FromArgb(30, 110, 80) };
    readonly CheckBox reveal = new() { Text = "Show password", AutoSize = true };

    public ConnectionDetailsForm(Func<CameraSettings?> getSettings, Func<string> getStatus)
    {
        this.getSettings = getSettings; this.getStatus = getStatus;
        Text = "DART — Camera connection";
        Icon = SystemIcons.Information;
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(247, 249, 252);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(760, 720);
        AutoScroll = true;
        MinimumSize = new Size(650, 620);

        var layout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(26), ColumnCount = 1, RowCount = 8 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label { Text = "Connect DART to your recorder", AutoSize = true, Font = new Font("Segoe UI", 20, FontStyle.Bold), ForeColor = Color.FromArgb(25, 45, 70), Margin = new Padding(0, 0, 0, 8) });
        layout.Controls.Add(new Label { Text = "Add an ONVIF camera in Agent DVR. Use the service URL and camera credentials below.", AutoSize = true, MaximumSize = new Size(680, 0), Margin = new Padding(0, 0, 0, 18) });

        var fields = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 3, BackColor = Color.White, Padding = new Padding(14) };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
        foreach (var name in new[] { "ONVIF service URL", "RTSP stream URL", "Username", "Password", "ONVIF port", "RTSP port" })
        {
            var row = fields.RowCount++;
            fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            fields.Controls.Add(new Label { Text = name, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            var box = new TextBox { ReadOnly = true, Dock = DockStyle.Fill, Margin = new Padding(3, 10, 8, 8), BackColor = Color.White, AccessibleName = name, UseSystemPasswordChar = name == "Password" };
            values[name] = box;
            fields.Controls.Add(box, 1, row);
            var copy = new Button { Text = "Copy", AutoSize = true, Anchor = AnchorStyles.None, AccessibleName = "Copy " + name };
            copy.Click += (_, _) => Copy(name);
            fields.Controls.Add(copy, 2, row);
        }
        layout.Controls.Add(fields);
        reveal.Margin = new Padding(0, 12, 0, 8);
        reveal.CheckedChanged += (_, _) => values["Password"].UseSystemPasswordChar = !reveal.Checked;
        layout.Controls.Add(reveal);
        layout.Controls.Add(new Label { Text = "Use RTSP over TCP. These are DART camera credentials, not your Agent DVR account login.\nChanges to camera.json take effect after restarting DART.", AutoSize = true, MaximumSize = new Size(680, 0), Margin = new Padding(0, 4, 0, 14) });
        layout.Controls.Add(state);
        layout.Controls.Add(feedback);
        var close = new Button { Text = "Close", AutoSize = true, Anchor = AnchorStyles.Right, Margin = new Padding(0, 12, 0, 0) };
        close.Click += (_, _) => Close();
        layout.Controls.Add(close);
        CancelButton = close;
        Controls.Add(layout);
        refresh.Tick += (_, _) => UpdateDetails();
        Shown += (_, _) => { UpdateDetails(); refresh.Start(); };
        FormClosed += (_, _) => { refresh.Stop(); refresh.Dispose(); };
    }

    void UpdateDetails()
    {
        state.Text = "Status: " + getStatus();
        var s = getSettings();
        if (s is null) { state.Text += " — Connection details will appear when configuration loads."; return; }
        Set("ONVIF service URL", s.BaseUrl + "/onvif/device_service");
        Set("RTSP stream URL", s.RtspUrl);
        Set("Username", s.Username);
        Set("Password", s.Password);
        Set("ONVIF port", s.OnvifPort.ToString());
        Set("RTSP port", s.RtspPort.ToString());
    }
    void Set(string name, string value) { if (values[name].Text != value) values[name].Text = value; }
    void Copy(string name)
    {
        var value = values[name].Text;
        if (value.Length == 0) { feedback.Text = "Connection details are still loading."; return; }
        try { Clipboard.SetText(value); feedback.Text = name + " copied to clipboard."; }
        catch (System.Runtime.InteropServices.ExternalException) { feedback.Text = "Clipboard is busy. Please try again."; }
    }
}
