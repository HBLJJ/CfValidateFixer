using System.Drawing;
using System.Drawing.Drawing2D;

namespace CfValidateFixer;

/// <summary>赞助窗口：收款二维码 + 问题反馈入口。文案与二维码均可由作者直接替换。</summary>
public sealed class SponsorForm : Form
{
    private readonly PixelButton _feedback = new();
    private readonly PixelButton _close = new();
    private readonly string? _diagLog;

    public SponsorForm(string? diagnosticLog = null)
    {
        _diagLog = diagnosticLog;
        Text = I18n.S("支持开发者", "Support the Developer");
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.None;
        Width = 1060;
        Height = 860;
        BackColor = Palette.Panel;
        DoubleBuffered = true;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

        var title = new PixelLabel
        {
            Text = I18n.S("支持开发者", "Support the Developer"),
            Font = PixelFont.Create(40, FontStyle.Bold),
            ForeColor = Palette.Accent,
            Size = new Size(520, 58),
            Location = new Point(36, 22)
        };
        Controls.Add(title);

        var text = new Label
        {
            Text = LoadText(),
            Font = PixelFont.Create(22, FontStyle.Bold),
            ForeColor = Color.FromArgb(255, 240, 205),
            AutoSize = false,
            Width = 980,
            Height = 100,
            Location = new Point(38, 84)
        };
        Controls.Add(text);

        AddQr("sponsor_wechat.png", I18n.S("微 信", "WeChat"), 70, 195);
        AddQr("sponsor_alipay.png", I18n.S("支付宝", "Alipay"), 640, 195);

        _feedback.Text = I18n.S("问题反馈", "Feedback");
        _feedback.Accent = true;
        _feedback.Size = new Size(180, 54);
        _feedback.Location = new Point(38, 790);
        _feedback.Click += (_, _) =>
        {
            using var fb = new FeedbackForm(_diagLog);
            fb.ShowDialog(this);
        };
        Controls.Add(_feedback);

        _close.Text = I18n.S("关闭", "Close");
        _close.Size = new Size(150, 54);
        _close.Location = new Point(870, 790);
        _close.Click += (_, _) => Close();
        Controls.Add(_close);

        UiScale.Apply(this);
    }

    private static string LoadText()
    {
        try
        {
            var dir = AppContext.BaseDirectory;
            var name = I18n.Current == Lang.En ? "sponsor_en.txt" : "sponsor.txt";
            var path = Path.Combine(dir, name);
            if (!File.Exists(path)) path = Path.Combine(dir, "sponsor.txt");
            if (File.Exists(path))
            {
                var t = File.ReadAllText(path).Trim();
                if (t.Length > 0) return t;
            }
        }
        catch { }
        return I18n.S(
            "如果这个工具帮你解决了卡验证的麻烦，欢迎请作者喝一杯咖啡o(╥﹏╥)o作者是咖啡重度依赖，搞这些小破玩意耗了不少咖啡。赞助完全自愿不影响任何功能。也欢迎把使用中遇到的问题反馈给我，左下角有问题反馈",
            "If this tool saved you from the Cloudflare loop, buy the author a coffee o(╥﹏╥)o The author runs on coffee, and these little tools burned a lot of it. Sponsorship is optional and never affects features. Feedback is welcome — use the Feedback button at the bottom left.");
    }

    private void AddQr(string file, string caption, int x, int y)
    {
        var box = new PixelPanel
        {
            Location = new Point(x, y),
            Size = new Size(350, 470),
            BackColor = Palette.Paper,
            DrawBorder = true
        };
        Controls.Add(box);

        Image? img = null;
        try
        {
            var dir = AppContext.BaseDirectory;
            var key = file.Replace(".png", "");
            byte[]? bytes = null;
            foreach (var name in new[] { file, key + ".jpg" })
            {
                var path = Path.Combine(dir, name);
                if (File.Exists(path)) { bytes = File.ReadAllBytes(path); break; }
            }
            if (bytes == null)
            {
                var asm = typeof(SponsorForm).Assembly;
                var res = asm.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith(key + ".jpg", StringComparison.OrdinalIgnoreCase));
                if (res != null)
                {
                    using var s = asm.GetManifestResourceStream(res);
                    if (s != null)
                    {
                        using var ms = new MemoryStream();
                        s.CopyTo(ms);
                        bytes = ms.ToArray();
                    }
                }
            }
            if (bytes != null)
            {
                using var ms = new MemoryStream(bytes);
                img = Image.FromStream(ms);
            }
        }
        catch { }

        if (img != null)
        {
            // 统一画布尺寸：两张图按比例居中；画布按 2 倍分辨率生成，高 DPI 放大后依然清晰
            const int cw = 660, ch = 900;
            var canvas = new Bitmap(cw, ch);
            using (var cg = Graphics.FromImage(canvas))
            {
                cg.Clear(Color.White);
                cg.InterpolationMode = InterpolationMode.HighQualityBicubic;
                float scale = Math.Min((float)cw / img.Width, (float)ch / img.Height);
                int w = (int)(img.Width * scale);
                int h = (int)(img.Height * scale);
                cg.DrawImage(img, (cw - w) / 2, (ch - h) / 2, w, h);
            }
            var pic = new PictureBox
            {
                Image = canvas,
                SizeMode = PictureBoxSizeMode.Zoom,
                Location = new Point(10, 10),
                Size = new Size(330, 450),
                BackColor = Color.White,
                Cursor = Cursors.Hand
            };
            pic.Click += (_, _) => ShowLarge(canvas);
            box.Controls.Add(pic);
        }
        else
        {
            var placeholder = new Label
            {
                Text = I18n.S("把收款码图片放到\n", "Put the QR image here:\n") + file,
                Font = PixelFont.Create(15),
                ForeColor = Palette.InkDim,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill
            };
            box.Controls.Add(placeholder);
        }

        var label = new PixelLabel
        {
            Text = caption,
            Font = PixelFont.Create(18, FontStyle.Bold),
            ForeColor = Palette.Text,
            Size = new Size(140, 30),
            Location = new Point(x + 130, y + 482)
        };
        Controls.Add(label);
    }

    private void ShowLarge(Image img)
    {
        var f = new Form
        {
            Text = I18n.S("查看收款码", "QR code"),
            FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.CenterParent,
            BackColor = Color.Black,
            Width = 760,
            Height = 940
        };
        var pb = new PictureBox
        {
            Image = img,
            SizeMode = PictureBoxSizeMode.Zoom,
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            Cursor = Cursors.Hand
        };
        pb.Click += (_, _) => f.Close();
        f.Controls.Add(pb);
        f.ShowDialog(this);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.None;
        using (var p = new Pen(Palette.BorderDim, 4)) g.DrawRectangle(p, 1, 1, Width - 3, Height - 3);
        using (var p = new Pen(Palette.Border, 2)) g.DrawRectangle(p, 5, 5, Width - 11, Height - 11);
    }
}
