using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace CfValidateFixer;

/// <summary>星露谷风格调色板：木框 + 羊皮纸 + 奶油黄。</summary>
public static class Palette
{
    public static readonly Color Bg = Color.FromArgb(32, 44, 30);          // 森林夜
    public static readonly Color BgAlt = Color.FromArgb(43, 57, 38);
    public static readonly Color Panel = Color.FromArgb(96, 64, 34);       // 木色
    public static readonly Color PanelAlt = Color.FromArgb(124, 85, 47);
    public static readonly Color Border = Color.FromArgb(222, 172, 96);    // 金边
    public static readonly Color BorderDim = Color.FromArgb(56, 35, 20);   // 深棕描边
    public static readonly Color Accent = Color.FromArgb(255, 216, 122);   // 奶油黄
    public static readonly Color Text = Color.FromArgb(246, 232, 200);     // 米白
    public static readonly Color TextDim = Color.FromArgb(206, 178, 136);
    public static readonly Color Paper = Color.FromArgb(240, 218, 172);    // 羊皮纸
    public static readonly Color Ink = Color.FromArgb(58, 41, 24);         // 墨色
    public static readonly Color InkDim = Color.FromArgb(122, 92, 58);
    public static readonly Color LogGreen = Color.FromArgb(46, 125, 50);
    public static readonly Color LogRed = Color.FromArgb(183, 28, 28);
    public static readonly Color LogOrange = Color.FromArgb(216, 96, 16);
    public static readonly Color LogBlue = Color.FromArgb(21, 101, 192);
    public static readonly Color LogGold = Color.FromArgb(150, 100, 30);
}

public static class PixelFont
{
    private static FontFamily? _family;
    private static GCHandle _pin;

    /// <summary>按系统 DPI 放大的全局字体缩放系数（96 DPI = 1）。</summary>
    public static float Scale = 1f;

    public static void Load()
    {
        try
        {
            var asm = typeof(PixelFont).Assembly;
            var name = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("zpix.ttf", StringComparison.OrdinalIgnoreCase));
            if (name != null)
            {
                using var stream = asm.GetManifestResourceStream(name);
                if (stream != null)
                {
                    var bytes = new byte[stream.Length];
                    int read = 0;
                    while (read < bytes.Length) read += stream.Read(bytes, read, bytes.Length - read);
                    _pin = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                    var pfc = new PrivateFontCollection();
                    pfc.AddMemoryFont(_pin.AddrOfPinnedObject(), bytes.Length);
                    if (pfc.Families.Length > 0) _family = pfc.Families[0];
                    if (_family != null) return;
                }
            }
            var path = Path.Combine(AppContext.BaseDirectory, "fonts", "zpix.ttf");
            if (!File.Exists(path)) return;
            var fileFonts = new PrivateFontCollection();
            fileFonts.AddFontFile(path);
            if (fileFonts.Families.Length > 0) _family = fileFonts.Families[0];
        }
        catch { }
    }

    public static Font Create(float pixelSize, FontStyle style = FontStyle.Regular)
    {
        float size = pixelSize * Scale;
        if (_family != null)
        {
            try { return new Font(_family, size, style, GraphicsUnit.Pixel); } catch { }
        }
        return new Font("Microsoft YaHei UI", size * 0.75f, style, GraphicsUnit.Pixel);
    }
}

/// <summary>木质像素按钮：深棕外框 + 金色内线 + 奶油色文字。</summary>
public sealed class PixelButton : Button
{
    public bool Accent { get; set; }
    private bool _hover;
    private bool _down;

    public PixelButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Palette.PanelAlt;
        ForeColor = Palette.Text;
        Font = PixelFont.Create(22, FontStyle.Bold);
        Cursor = Cursors.Hand;
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.None;
        g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
        var parentColor = Parent?.BackColor ?? Palette.Bg;
        g.Clear(parentColor);

        int pad = 2;
        var rect = new Rectangle(pad, pad, Width - pad * 2, Height - pad * 2);
        if (_down) rect.Offset(1, 2);

        Color top = !Enabled ? Color.FromArgb(66, 58, 48)
            : Accent ? Color.FromArgb(255, 205, 96) : (_hover ? Color.FromArgb(150, 106, 62) : Palette.PanelAlt);
        Color bottom = !Enabled ? Color.FromArgb(52, 45, 37)
            : Accent ? Color.FromArgb(214, 150, 46) : (_hover ? Color.FromArgb(122, 84, 46) : Palette.Panel);
        using (var b = new LinearGradientBrush(rect, top, bottom, LinearGradientMode.Vertical))
            g.FillRectangle(b, rect);

        using (var pen = new Pen(!Enabled ? Color.FromArgb(90, 80, 68) : Palette.BorderDim, 3)) g.DrawRectangle(pen, rect);
        using (var pen = new Pen(!Enabled ? Color.FromArgb(90, 80, 68) : Color.FromArgb(206, 160, 96), 1))
            g.DrawRectangle(pen, rect.X + 3, rect.Y + 3, rect.Width - 7, rect.Height - 7);
        // 顶部高光 / 底部阴影
        using (var hi = new Pen(Color.FromArgb(90, 255, 230, 170), 1))
            g.DrawLine(hi, rect.X + 5, rect.Y + 5, rect.Right - 5, rect.Y + 5);
        using (var lo = new Pen(Color.FromArgb(120, 30, 18, 8), 1))
            g.DrawLine(lo, rect.X + 5, rect.Bottom - 6, rect.Right - 5, rect.Bottom - 6);

        // 像素切角
        using (var corner = new SolidBrush(parentColor))
        {
            g.FillRectangle(corner, rect.X, rect.Y, 3, 3);
            g.FillRectangle(corner, rect.Right - 3, rect.Y, 3, 3);
            g.FillRectangle(corner, rect.X, rect.Bottom - 3, 3, 3);
            g.FillRectangle(corner, rect.Right - 3, rect.Bottom - 3, 3, 3);
        }

        var textColor = !Enabled ? Color.FromArgb(130, 120, 108)
            : Accent ? Color.FromArgb(64, 38, 12) : Palette.Text;
        using var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using (var shadow = new SolidBrush(Accent ? Color.FromArgb(90, 255, 240, 200) : Color.FromArgb(120, 20, 12, 6)))
            g.DrawString(Text, Font, shadow, new RectangleF(rect.X + 1, rect.Y + 1, rect.Width, rect.Height), fmt);
        using (var brush = new SolidBrush(textColor))
            g.DrawString(Text, Font, brush, rect, fmt);
    }
}

/// <summary>带像素阴影的单行标签。</summary>
public sealed class PixelLabel : Control
{
    public Color ShadowColor { get; set; } = Color.FromArgb(220, 28, 16, 8);
    public int ShadowX { get; set; } = 2;
    public int ShadowY { get; set; } = 2;

    public PixelLabel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        ForeColor = Palette.Text;
        Font = PixelFont.Create(20, FontStyle.Bold);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
        g.Clear(Parent?.BackColor ?? Palette.Panel);
        using (var shadow = new SolidBrush(ShadowColor))
            g.DrawString(Text, Font, shadow, ShadowX, ShadowY);
        using (var brush = new SolidBrush(ForeColor))
            g.DrawString(Text, Font, brush, 0, 0);
    }
}

/// <summary>带像素阴影、支持多行居中/左对齐的文本标签。</summary>
public sealed class PixelShadowLabel : Control
{
    public int ShadowX { get; set; } = 3;
    public int ShadowY { get; set; } = 3;
    public Color ShadowColor { get; set; } = Color.FromArgb(210, 20, 12, 6);
    public StringAlignment Align { get; set; } = StringAlignment.Center;

    public PixelShadowLabel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Font = PixelFont.Create(30, FontStyle.Bold);
        ForeColor = Palette.Text;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Palette.Panel);
        // 用 GDI 的 TextRenderer：支持字体链接，emoji 不会丢字形
        var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix;
        TextRenderer.DrawText(g, Text, Font,
            new Rectangle(ShadowX, ShadowY, Math.Max(0, Width - ShadowX), Math.Max(0, Height - ShadowY)),
            ShadowColor, flags);
        TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 0, Width, Height), ForeColor, flags);
    }
}

/// <summary>木质像素单选：方框 + 金色方块。</summary>
public sealed class PixelRadio : RadioButton
{
    public PixelRadio()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        AutoSize = false;
        Height = 30;
        Font = PixelFont.Create(20, FontStyle.Bold);
        ForeColor = Palette.Text;
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.None;
        g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
        var parentColor = Parent?.BackColor ?? Palette.Panel;
        g.Clear(parentColor);

        int box = 18;
        int y = (Height - box) / 2;
        var r = new Rectangle(2, y, box, box);
        using (var back = new SolidBrush(Color.FromArgb(70, 46, 26))) g.FillRectangle(back, r);
        using (var pen = new Pen(Checked ? Palette.Border : Color.FromArgb(60, 40, 24), 2))
            g.DrawRectangle(pen, r);
        if (Checked)
        {
            using var b = new SolidBrush(Palette.Accent);
            g.FillRectangle(b, r.X + 5, r.Y + 5, box - 10, box - 10);
        }
        var textColor = Checked ? Palette.Accent : Palette.TextDim;
        using var brush2 = new SolidBrush(textColor);
        g.DrawString(Text, Font, brush2, new PointF(box + 12, (Height - Font.Height) / 2f - 1));
    }
}

/// <summary>木质面板：深棕外框 + 金色内线 + 像素切角。</summary>
public sealed class PixelPanel : Panel
{
    public bool DrawBorder { get; set; } = true;
    public bool LightFrame { get; set; }

    public PixelPanel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        if (DrawBorder)
        {
            var outer = LightFrame ? Color.FromArgb(196, 150, 88) : Palette.BorderDim;
            using (var pen = new Pen(outer, 4)) g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            using (var pen = new Pen(Palette.Border, 2)) g.DrawRectangle(pen, 4, 4, Width - 9, Height - 9);
            using (var corner = new SolidBrush(BackColor))
            {
                g.FillRectangle(corner, 0, 0, 4, 4);
                g.FillRectangle(corner, Width - 4, 0, 4, 4);
                g.FillRectangle(corner, 0, Height - 4, 4, 4);
                g.FillRectangle(corner, Width - 4, Height - 4, 4, 4);
            }
        }
        base.OnPaint(e);
    }
}
