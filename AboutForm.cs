using System.Drawing;
using System.Drawing.Drawing2D;

namespace CfValidateFixer;

/// <summary>星露谷风格「关于」窗口（中英双语）。</summary>
public sealed class AboutForm : Form
{
    public AboutForm()
    {
        Text = I18n.S("关于 Cloudflare 修复工坊", "About Cloudflare Fix Workshop");
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.None;
        Width = 660;
        Height = 440;
        BackColor = Palette.Panel;
        DoubleBuffered = true;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

        var title = new PixelLabel
        {
            Text = I18n.S("Cloudflare 修复工坊", "Cloudflare Fix Workshop"),
            Font = PixelFont.Create(34, FontStyle.Bold),
            ForeColor = Palette.Accent,
            Size = new Size(540, 52),
            Location = new Point(36, 26)
        };
        Controls.Add(title);

        var version = new PixelLabel
        {
            Text = "v" + AppInfo.Version,
            Font = PixelFont.Create(20, FontStyle.Bold),
            ForeColor = Palette.TextDim,
            Size = new Size(140, 32),
            Location = new Point(38, 78)
        };
        Controls.Add(version);

        var body = new Label
        {
            Text = I18n.S(
                "开发者签名：火爆辣椒鸡\r\n" +
                "功能：Cloudflare 验证失败 / 加载过长的诊断与一键修复\r\n" +
                "技术：.NET 9 · WinForms · Zpix 像素字体 · SQLite\r\n" +
                "版权：© 2026 火爆辣椒鸡 · 保留所有权利\r\n\r\n" +
                "工具只做可逆的本地修复，不改 DNS / 代理 / 路由；\r\n" +
                "涉及 Cookie 与 hosts 的操作都会先备份，修复报告自动生成。",
                "Developer signature: 火爆辣椒鸡\r\n" +
                "What it does: diagnose & fix Cloudflare verify failures / slow loading\r\n" +
                "Built with: .NET 9 · WinForms · Zpix pixel font · SQLite\r\n" +
                "Copyright: © 2026 火爆辣椒鸡 · All rights reserved\r\n\r\n" +
                "Only reversible local fixes: no DNS / proxy / route changes.\r\n" +
                "Cookies and hosts are backed up before any change; a report is generated."),
            Font = PixelFont.Create(18, FontStyle.Bold),
            ForeColor = Palette.Text,
            AutoSize = false,
            Width = 590,
            Height = 240,
            Location = new Point(38, 118)
        };
        Controls.Add(body);

        var close = new PixelButton
        {
            Text = I18n.S("关闭", "Close"),
            Accent = true,
            Size = new Size(150, 50),
            Location = new Point(470, 366)
        };
        close.Click += (_, _) => Close();
        Controls.Add(close);

        UiScale.Apply(this);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.None;
        using (var p = new Pen(Palette.BorderDim, 4)) g.DrawRectangle(p, 1, 1, Width - 3, Height - 3);
        using (var p = new Pen(Palette.Border, 2)) g.DrawRectangle(p, 5, 5, Width - 11, Height - 11);
    }
}
