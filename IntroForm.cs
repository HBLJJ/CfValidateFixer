using System.Drawing;
using System.Drawing.Drawing2D;

namespace CfValidateFixer;

/// <summary>启动时的「这是什么？」简介弹窗：展示亮点与差异，同时给首次启动的初始化留出时间。</summary>
public sealed class IntroForm : Form
{
    private readonly CheckBox _hide = new();

    public IntroForm()
    {
        Text = I18n.S("这是什么？", "What is this?");
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.None;
        Width = 1360;
        Height = 800;
        BackColor = Palette.Panel;
        DoubleBuffered = true;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

        var title = new PixelLabel
        {
            Text = I18n.S("这是什么？", "What is this?"),
            Font = PixelFont.Create(42, FontStyle.Bold),
            ForeColor = Palette.Accent,
            Size = new Size(460, 64),
            Location = new Point(34, 22)
        };
        Controls.Add(title);

        var body = new PixelShadowLabel
        {
            Text = I18n.S(
                "😋🔧这是一个专治 Cloudflare 卡验证 / 加载慢的小工具🔧😋\n" +
                "🔎「正在验证您是真人」，或者验证框一直转圈？🌧️\n" +
                "这个小东西会看你的系统时间、DNS、hosts、代理、\n" +
                "浏览器扩展、残留 Cookie，一项项查清楚🔎\n" +
                "再按可能性告诉你问题出在哪，最后只修该修的那几处。\n\n" +
                "✅️不绕验证、不换 IP、不改 DNS 和代理，改过的地方都能回滚✅️\n\n" +
                "开源👍免费👍中英双语👍\n" +
                "左下角「问题反馈」随时找我；\n" +
                "想请作者喝杯咖啡的话☕️赞助窗口在里面（我真的很想喝咖啡）",
                "😋🔧 A tiny tool for \"Cloudflare verification stuck / slow loading\" 🔧😋\n" +
                "🔎 \"Verify you are human\" spinning? 🌧️\n" +
                "It checks your system clock, DNS, hosts, proxy,\n" +
                "browser extensions and leftover cookies, one by one 🔎\n" +
                "Then ranks the likely cause and fixes only what's needed.\n\n" +
                "✅ No bypass, no IP change, no DNS/proxy edits - everything reversible ✅\n\n" +
                "Open source 👍 Free 👍 Bilingual 👍\n" +
                "Feedback at the bottom left anytime;\n" +
                "if you'd like to buy the author a coffee ☕️ the sponsor window is right there (I really want coffee)"),
            Font = PixelFont.Create(34, FontStyle.Bold),
            ForeColor = Palette.Text,
            Size = new Size(1300, 580),
            Location = new Point(28, 94),
            Align = StringAlignment.Center
        };
        Controls.Add(body);

        var ok = new PixelButton
        {
            Text = I18n.S("懂你意思", "Got it"),
            Accent = true,
            Size = new Size(220, 56),
            Location = new Point(1090, 726)
        };
        ok.Click += (_, _) =>
        {
            if (_hide.Checked)
            {
                UserSettings.ShowIntro = false;
                UserSettings.Save();
            }
            Close();
        };
        Controls.Add(ok);

        _hide.Text = I18n.S("不再显示", "Don't show again");
        _hide.Font = PixelFont.Create(20, FontStyle.Bold);
        _hide.ForeColor = Palette.Text;
        _hide.BackColor = Palette.Panel;
        _hide.FlatStyle = FlatStyle.Flat;
        _hide.AutoSize = true;
        _hide.Location = new Point(900, 742);
        Controls.Add(_hide);

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
