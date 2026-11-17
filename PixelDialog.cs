using System.Drawing;
using System.Drawing.Drawing2D;

namespace CfValidateFixer;

/// <summary>星露谷风格的像素消息框，替代系统 MessageBox。</summary>
public sealed class PixelDialog : Form
{
    public static DialogResult Show(Form? owner, string title, string message, bool yesNo = false)
    {
        using var dlg = new PixelDialog(title, message, yesNo);
        return owner != null ? dlg.ShowDialog(owner) : dlg.ShowDialog();
    }

    private PixelDialog(string title, string message, bool yesNo)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.None;
        Width = 780;
        Height = 380;
        BackColor = Palette.Panel;
        DoubleBuffered = true;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

        var titleLabel = new PixelLabel
        {
            Text = title,
            Font = PixelFont.Create(32, FontStyle.Bold),
            ForeColor = Palette.Accent,
            Size = new Size(700, 50),
            Location = new Point(36, 22)
        };
        Controls.Add(titleLabel);

        var body = new PixelShadowLabel
        {
            Text = message,
            Font = PixelFont.Create(22, FontStyle.Bold),
            ForeColor = Palette.Text,
            Size = new Size(700, 180),
            Location = new Point(38, 84),
            Align = StringAlignment.Center
        };
        Controls.Add(body);

        if (yesNo)
        {
            var yes = new PixelButton
            {
                Text = I18n.S("是", "Yes"),
                Accent = true,
                Size = new Size(150, 50),
                Location = new Point(420, 300)
            };
            yes.Click += (_, _) => DialogResult = DialogResult.Yes;
            Controls.Add(yes);

            var no = new PixelButton
            {
                Text = I18n.S("否", "No"),
                Size = new Size(150, 50),
                Location = new Point(590, 300)
            };
            no.Click += (_, _) => DialogResult = DialogResult.No;
            Controls.Add(no);
        }
        else
        {
            var ok = new PixelButton
            {
                Text = I18n.S("知道了", "OK"),
                Accent = true,
                Size = new Size(170, 50),
                Location = new Point(580, 300)
            };
            ok.Click += (_, _) => DialogResult = DialogResult.OK;
            Controls.Add(ok);
        }

        AcceptButton = null;
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
