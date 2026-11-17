namespace CfValidateFixer;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            ApplicationConfiguration.Initialize();
        }
        catch
        {
            // 受限环境下 EnableVisualStyles 可能写临时文件失败，回退到无视觉样式启动
            try { Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); } catch { }
            try { Application.SetCompatibleTextRenderingDefault(false); } catch { }
        }
        bool autoFix = args.Any(a => a.Equals("--autofix", StringComparison.OrdinalIgnoreCase));
        bool closeBrowser = args.Any(a => a.Equals("--close-browser", StringComparison.OrdinalIgnoreCase));
        bool noSplash = args.Any(a => a.Equals("--no-splash", StringComparison.OrdinalIgnoreCase));
        bool safeMode = args.Any(a => a.Equals("--safe", StringComparison.OrdinalIgnoreCase));
        int modeIdx = Array.FindIndex(args, a => a.Equals("--mode", StringComparison.OrdinalIgnoreCase));
        bool slowMode = modeIdx >= 0 && modeIdx + 1 < args.Length &&
                        args[modeIdx + 1].Equals("slow", StringComparison.OrdinalIgnoreCase);
        var mode = slowMode ? ProblemMode.SlowLoading : ProblemMode.VerifyFailed;

        PixelFont.Load();
        I18n.Load();
        try
        {
            using var g = Graphics.FromHwnd(IntPtr.Zero);
            // 始终跟随系统 DPI：大于 100% 放大，小于 100% 缩小（下限 0.5 防止极端过小）
            PixelFont.Scale = Math.Clamp(g.DpiX / 96f, 0.5f, 3f);
        }
        catch { }

        Application.Run(new MainForm(autoFix, closeBrowser, mode, !autoFix && !noSplash, safeMode));
    }
}
