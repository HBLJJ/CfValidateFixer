namespace CfValidateFixer;

/// <summary>按系统 DPI 缩放窗口与控件，避免 4K 高缩放下界面过小。</summary>
public static class UiScale
{
    public static float Current => PixelFont.Scale;

    public static void Apply(Form form)
    {
        float s = PixelFont.Scale;
        if (Math.Abs(s - 1f) <= 0.01f) return;
        try
        {
            int oldW = form.Width;
            int oldH = form.Height;
            form.Scale(new SizeF(s, s));
            form.Width = (int)(oldW * s);
            form.Height = (int)(oldH * s);
            var wa = Screen.PrimaryScreen!.WorkingArea;
            form.Location = new Point(
                Math.Max(0, (wa.Width - form.Width) / 2),
                Math.Max(0, (wa.Height - form.Height) / 2));
        }
        catch { }
    }
}
