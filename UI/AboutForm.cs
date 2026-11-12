using System.Diagnostics;
using System.Reflection;
using SpkDisplayController.Localization;

namespace SpkDisplayController.UI;

internal sealed class AboutForm : Form
{
    public AboutForm(Icon? appIcon, int monitorCount)
    {
        var asm = Assembly.GetExecutingAssembly();
        var version = asm.GetName().Version?.ToString(3) ?? "1.0.0";
        var runtime = Environment.Version.ToString();
        var framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;

        Text = Loc.Get("AboutTitle");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(440, 300);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 9F);

        if (appIcon is not null)
            Icon = appIcon;

        var logo = new PictureBox
        {
            Location = new Point(24, 24),
            Size = new Size(64, 64),
            SizeMode = PictureBoxSizeMode.Zoom
        };

        if (appIcon is not null)
        {
            // 从多尺寸 ICO 中取 64×64，避免用托盘那档 32×32 放大后发虚
            using var large = new Icon(appIcon, 64, 64);
            logo.Image = large.ToBitmap();
        }

        Controls.Add(logo);

        var title = new Label
        {
            Text = Loc.Get("AppName"),
            Location = new Point(104, 26),
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 15F)
        };
        Controls.Add(title);

        var subtitle = new Label
        {
            Text = $"{Loc.Get("Version")} {version}",
            Location = new Point(107, 58),
            AutoSize = true,
            ForeColor = Color.Gray
        };
        Controls.Add(subtitle);

        var body = new Label
        {
            Text = Loc.Get("AboutBody"),
            Location = new Point(24, 106),
            Size = new Size(392, 110),
            AutoSize = false
        };
        Controls.Add(body);

        var info = new Label
        {
            Text = $"{Loc.Get("Runtime")}: {framework}  ·  {Loc.Get("MonitorCount")}: {monitorCount}",
            Location = new Point(24, 222),
            Size = new Size(392, 20),
            AutoSize = false,
            ForeColor = Color.DimGray
        };
        Controls.Add(info);

        var ok = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(340, 254),
            Size = new Size(76, 28)
        };
        Controls.Add(ok);
        AcceptButton = ok;
        CancelButton = ok;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Debug.WriteLine($"[SpkDisplayController] About shown, monitors={Environment.ProcessorCount}");
    }
}
