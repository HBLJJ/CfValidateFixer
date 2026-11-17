using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace CfValidateFixer;

public sealed class MainForm : Form
{
    private readonly RichTextBox _log = new();
    private readonly PixelButton _btnScan = new();
    private readonly PixelButton _btnFix = new();
    private readonly PixelButton _btnOpen = new();
    private readonly PixelButton _btnRestore = new();
    private readonly PixelButton _btnAbout = new();
    private readonly PixelButton _btnSponsor = new();
    private readonly PixelButton _btnLang = new();
    private readonly PixelButton _btnMin = new();
    private readonly PixelButton _btnClose = new();
    private readonly PixelLabel _status = new();
    private readonly PixelLabel _modeLabel = new();
    private readonly Label _modeHint = new();
    private readonly PixelPanel _titleBar = new();
    private readonly PixelPanel _modeBar = new();
    private readonly PixelPanel _logHost = new();
    private readonly PixelPanel _bottom = new();
    private readonly PixelButton _btnModeVerify = new();
    private readonly PixelButton _btnModeSlow = new();
    private ProblemMode _mode = ProblemMode.VerifyFailed;

    private Diagnosis? _diag;
    private FixEngine.FixResult? _fixResult;
    private string? _lastDiagnosticLog;
    private bool _busy;
    private readonly bool _autoFix;
    private readonly bool _autoCloseBrowser;
    private readonly bool _showIntro;
    private readonly bool _safeMode;
    private bool _ready;

    public MainForm(bool autoFix = false, bool autoCloseBrowser = false, ProblemMode initialMode = ProblemMode.VerifyFailed, bool showIntro = true, bool safeMode = false)
    {
        PixelFont.Load();
        I18n.Load();
        _autoFix = autoFix;
        _autoCloseBrowser = autoCloseBrowser;
        _showIntro = showIntro;
        _safeMode = safeMode;

        Text = I18n.S(AppInfo.Title, AppInfo.TitleEn);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        Width = 1040;
        Height = 790;
        MinimumSize = new Size(1040, 790);
        MaximumSize = new Size(1040, 790);
        StartPosition = FormStartPosition.Manual;
        int screenW = GetSystemMetrics(0), screenH = GetSystemMetrics(1);
        Location = new Point(Math.Max(0, (screenW - Width) / 2), Math.Max(0, (screenH - Height) / 2));
        BackColor = Palette.Bg;
        DoubleBuffered = true;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

        BuildTitleBar();
        BuildModeBar();
        BuildBottom();
        BuildLog();

        Controls.Add(_logHost);
        Controls.Add(_bottom);
        Controls.Add(_modeBar);
        Controls.Add(_titleBar);

        if (initialMode == ProblemMode.SlowLoading)
        {
            _mode = ProblemMode.SlowLoading;
            UpdateModeButtons();
        }

        Load += async (_, _) =>
        {
            _ready = true;
            await RunScanAsync();
            if (_autoFix) await RunFixAsync(_autoCloseBrowser);
        };

        // 主窗口与简介同时出现，简介作为模态覆盖在主窗口上方
        Shown += (_, _) =>
        {
            if (!_showIntro || !UserSettings.ShowIntro) return;
            using var intro = new IntroForm();
            intro.ShowDialog(this);
        };

        UiScale.Apply(this);
    }

    private void BuildTitleBar()
    {
        _titleBar.Dock = DockStyle.Top;
        _titleBar.Height = 56;
        _titleBar.BackColor = Palette.Panel;
        _titleBar.DrawBorder = false;

        _titleBar.Paint += (s, e) =>
        {
            var g = e.Graphics;
            float sc = PixelFont.Scale;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
            using (var shade = new Pen(Color.FromArgb(30, 20, 12), 2))
                g.DrawLine(shade, 0, _titleBar.Height - 2, _titleBar.Width, _titleBar.Height - 2);

            using (var bg = new SolidBrush(Color.FromArgb(56, 35, 20))) g.FillRectangle(bg, 24 * sc, 14 * sc, 28 * sc, 28 * sc);
            using (var wd = new SolidBrush(Palette.PanelAlt)) g.FillRectangle(wd, 26 * sc, 16 * sc, 24 * sc, 24 * sc);
            using (var gb = new Pen(Palette.Border, 2)) g.DrawRectangle(gb, 26 * sc, 16 * sc, 22 * sc, 22 * sc);
            using (var ck = new Pen(Palette.LogGreen, 3))
                g.DrawLines(ck, new[]
                {
                    new Point((int)(30 * sc), (int)(28 * sc)),
                    new Point((int)(34 * sc), (int)(33 * sc)),
                    new Point((int)(43 * sc), (int)(22 * sc))
                });

            var font = PixelFont.Create(40, FontStyle.Bold);
            var title = I18n.S("Cloudflare 修复工坊", "Cloudflare Fix Workshop");
            using var outline = new SolidBrush(Color.FromArgb(235, 32, 18, 8));
            var offsets = new[] { (-3, 0), (3, 0), (0, -3), (0, 3), (-3, -3), (3, -3), (-3, 3), (3, 3), (-2, -2), (2, -2), (-2, 2), (2, 2) };
            foreach (var (dx, dy) in offsets)
                g.DrawString(title, font, outline, 64 * sc + dx, 7 * sc + dy);
            using var brush = new SolidBrush(Palette.Accent);
            g.DrawString(title, font, brush, 64 * sc, 7 * sc);

            using var smallFont = PixelFont.Create(20, FontStyle.Bold);
            var titleWidth = g.MeasureString(title, font).Width;
            using var smallBrush = new SolidBrush(Palette.TextDim);
            g.DrawString("v" + AppInfo.Version, smallFont, smallBrush, 64 * sc + titleWidth + 18 * sc, 24 * sc);
        };

        _titleBar.MouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, 0xA1, 0x2, 0);
            }
        };

        _btnMin.Text = "—";
        _btnMin.Size = new Size(40, 32);
        _btnMin.Location = new Point(944, 12);
        _btnMin.Font = PixelFont.Create(18, FontStyle.Bold);
        _btnMin.Click += (_, _) => WindowState = FormWindowState.Minimized;
        _titleBar.Controls.Add(_btnMin);

        _btnClose.Text = "X";
        _btnClose.Size = new Size(40, 32);
        _btnClose.Location = new Point(990, 12);
        _btnClose.Font = PixelFont.Create(18, FontStyle.Bold);
        _btnClose.Click += (_, _) => Close();
        _titleBar.Controls.Add(_btnClose);
    }

    private void BuildModeBar()
    {
        _modeBar.Dock = DockStyle.Top;
        _modeBar.Height = 68;
        _modeBar.BackColor = Color.FromArgb(72, 48, 26);
        _modeBar.DrawBorder = false;
        _modeBar.Paint += (s, e) =>
        {
            using var pen = new Pen(Palette.BorderDim, 2);
            e.Graphics.DrawLine(pen, 0, _modeBar.Height - 2, _modeBar.Width, _modeBar.Height - 2);
        };

        _modeLabel.Text = I18n.S("问题类型：", "Issue type:");
        _modeLabel.ForeColor = Palette.Text;
        _modeLabel.Font = PixelFont.Create(20, FontStyle.Bold);
        _modeLabel.Size = new Size(120, 32);
        _modeLabel.Location = new Point(24, 17);
        _modeBar.Controls.Add(_modeLabel);

        _btnModeVerify.Text = I18n.S("验证失败（卡验证）", "Verify failed (stuck)");
        _btnModeVerify.Font = PixelFont.Create(18, FontStyle.Bold);
        _btnModeVerify.Size = new Size(220, 40);
        _btnModeVerify.Location = new Point(150, 14);
        _btnModeVerify.Accent = true;
        _btnModeVerify.Click += async (_, _) => await SetModeAsync(ProblemMode.VerifyFailed);
        _modeBar.Controls.Add(_btnModeVerify);

        _btnModeSlow.Text = I18n.S("加载过久（转圈）", "Slow loading (spinning)");
        _btnModeSlow.Font = PixelFont.Create(18, FontStyle.Bold);
        _btnModeSlow.Size = new Size(220, 40);
        _btnModeSlow.Location = new Point(382, 14);
        _btnModeSlow.Click += async (_, _) => await SetModeAsync(ProblemMode.SlowLoading);
        _modeBar.Controls.Add(_btnModeSlow);

        _modeHint.Text = I18n.S("作者：火爆辣椒鸡 · 修复前自动备份", "by 火爆辣椒鸡 · auto-backup before fix");
        _modeHint.AutoSize = true;
        _modeHint.ForeColor = Palette.TextDim;
        _modeHint.Font = PixelFont.Create(17, FontStyle.Bold);
        _modeHint.Location = new Point(620, 21);
        _modeBar.Controls.Add(_modeHint);
    }

    private void BuildBottom()
    {
        _bottom.Dock = DockStyle.Bottom;
        _bottom.Height = 104;
        _bottom.BackColor = Palette.Panel;
        _bottom.DrawBorder = false;
        _bottom.Paint += (s, e) =>
        {
            using var pen = new Pen(Palette.Border, 2);
            e.Graphics.DrawLine(pen, 0, 1, _bottom.Width, 1);
        };

        _btnScan.Text = I18n.S("重新检测", "Re-scan");
        _btnScan.Size = new Size(140, 56);
        _btnScan.Location = new Point(24, 24);
        _btnScan.Click += async (_, _) => await RunScanAsync();
        _bottom.Controls.Add(_btnScan);

        _btnFix.Text = I18n.S("一键修复", "Fix All");
        _btnFix.Size = new Size(140, 56);
        _btnFix.Location = new Point(178, 24);
        _btnFix.Accent = true;
        _btnFix.Click += async (_, _) => await RunFixAsync();
        _bottom.Controls.Add(_btnFix);

        _btnOpen.Text = I18n.S("查看报告", "Report");
        _btnOpen.Size = new Size(110, 56);
        _btnOpen.Location = new Point(332, 24);
        _btnOpen.Enabled = false;
        _btnOpen.Click += (_, _) => OpenReport();
        _bottom.Controls.Add(_btnOpen);

        _btnRestore.Text = I18n.S("恢复备份", "Restore");
        _btnRestore.Size = new Size(120, 56);
        _btnRestore.Location = new Point(456, 24);
        _btnRestore.Click += async (_, _) => await RestoreBackupAsync();
        _bottom.Controls.Add(_btnRestore);

        _btnAbout.Text = I18n.S("关于", "About");
        _btnAbout.Size = new Size(80, 56);
        _btnAbout.Location = new Point(590, 24);
        _btnAbout.Click += (_, _) =>
        {
            using var about = new AboutForm();
            about.ShowDialog(this);
        };
        _bottom.Controls.Add(_btnAbout);

        _btnSponsor.Text = I18n.S("赞助/反馈", "Support");
        _btnSponsor.Size = new Size(130, 56);
        _btnSponsor.Location = new Point(684, 24);
        _btnSponsor.Click += (_, _) =>
        {
            using var sponsor = new SponsorForm(_lastDiagnosticLog);
            sponsor.ShowDialog(this);
        };
        _bottom.Controls.Add(_btnSponsor);

        _btnLang.Text = I18n.LangLabel;
        _btnLang.Size = new Size(70, 56);
        _btnLang.Location = new Point(828, 24);
        _btnLang.Click += (_, _) => SwitchLanguage();
        _bottom.Controls.Add(_btnLang);

        _status.ForeColor = Palette.TextDim;
        _status.Font = PixelFont.Create(18, FontStyle.Bold);
        _status.Size = new Size(120, 30);
        _status.Location = new Point(912, 38);
        _bottom.Controls.Add(_status);

        SetBusy(false);
    }

    private void BuildLog()
    {
        _logHost.Dock = DockStyle.Fill;
        _logHost.BackColor = Palette.Panel;
        _logHost.DrawBorder = true;
        _logHost.Padding = new Padding(18, 16, 18, 22);

        _log.Dock = DockStyle.Fill;
        _log.BackColor = Palette.Paper;
        _log.ForeColor = Palette.Ink;
        _log.BorderStyle = BorderStyle.None;
        _log.Font = PixelFont.Create(20, FontStyle.Bold);
        _log.ReadOnly = true;
        _log.WordWrap = true;
        _log.DetectUrls = false;
        _logHost.Controls.Add(_log);
    }

    private void SwitchLanguage()
    {
        var confirm = PixelDialog.Show(this,
            I18n.S("切换语言", "Switch language"),
            I18n.S("切换语言后软件会重启，是否继续？", "The app will restart to apply the language. Continue?"),
            yesNo: true);
        if (confirm != DialogResult.Yes) return;
        I18n.Set(I18n.Current == Lang.En ? Lang.Zh : Lang.En);
        Application.Restart();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using var brush = new LinearGradientBrush(ClientRectangle,
            Color.FromArgb(40, 55, 38), Color.FromArgb(24, 34, 24), LinearGradientMode.Vertical);
        e.Graphics.FillRectangle(brush, ClientRectangle);
        using var grass = new SolidBrush(Color.FromArgb(52, 70, 46));
        for (int x = 0; x < Width; x += 18)
            for (int y = 0; y < Height; y += 18)
                e.Graphics.FillRectangle(grass, x + 6, y + 8, 3, 5);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try
        {
            int dark = 1;
            DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
        }
        catch { }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    private void Append(string text, Color? color = null, bool bold = false)
    {
        _log.SelectionStart = _log.TextLength;
        _log.SelectionLength = 0;
        _log.SelectionColor = color ?? Palette.Ink;
        _log.SelectionFont = new Font(_log.Font, FontStyle.Bold);
        _log.AppendText(text + Environment.NewLine);
        _log.SelectionStart = _log.TextLength;
        _log.ScrollToCaret();
        SendMessage(_log.Handle, 0x0115, 7, 0);
        _log.SelectionStart = _log.TextLength;
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _btnScan.Enabled = !busy;
        _btnFix.Enabled = !busy && !_safeMode;
        _btnRestore.Enabled = !busy && !_safeMode;
        _status.Text = busy ? I18n.S("◼ 处理中...", "◼ Working...")
            : _safeMode ? I18n.S("◼ 安全模式 · 就绪", "◼ Safe mode · Ready")
            : I18n.S("◼ 就绪", "◼ Ready");
    }

    private string BuildDiagnosticLog(Diagnosis d)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Cloudflare 修复工坊 - 诊断日志");
        sb.AppendLine("时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine("程序版本：v" + AppInfo.Version);
        sb.AppendLine("模式：" + (d.Mode == ProblemMode.SlowLoading ? "加载过久" : "验证失败"));
        sb.AppendLine("系统：" + Environment.OSVersion.VersionString);
        sb.AppendLine(new string('-', 60));
        sb.AppendLine(_log.Text);
        return sb.ToString();
    }

    private string SaveDiagnosticLog(string text)
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var candidates = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "diagnostic_" + stamp + ".txt"),
            Path.Combine(Path.GetTempPath(), "diagnostic_" + stamp + ".txt")
        };
        foreach (var p in candidates)
        {
            try { File.WriteAllText(p, text, System.Text.Encoding.UTF8); return p; }
            catch { }
        }
        return "";
    }

    private async Task SetModeAsync(ProblemMode mode)
    {
        if (_busy || _mode == mode) return;
        _mode = mode;
        UpdateModeButtons();
        if (_ready) await RunScanAsync();
    }

    private void UpdateModeButtons()
    {
        _btnModeVerify.Accent = _mode == ProblemMode.VerifyFailed;
        _btnModeSlow.Accent = _mode == ProblemMode.SlowLoading;
        _btnModeVerify.Invalidate();
        _btnModeSlow.Invalidate();
    }

    private async Task RunScanAsync()
    {
        if (_busy) return;
        var mode = _mode;
        SetBusy(true);
        _log.Clear();
        if (_safeMode)
        {
            Append(I18n.S("【安全模式】仅诊断，不会修改任何系统设置。", "[Safe mode] Diagnose only; nothing will be changed."),
                Palette.LogBlue, true);
        }
        Append(mode == ProblemMode.SlowLoading
            ? I18n.S("> 正在诊断「加载时间过长（一直转圈）」...", "> Diagnosing slow loading...")
            : I18n.S("> 正在检测「验证失败（卡验证）」...", "> Checking verify failure..."), Palette.LogBlue);
        Append("");
        try
        {
            var progress = new Progress<string>(m => Append("  " + m, Palette.InkDim));
            _diag = await Diagnostics.RunAsync(mode, progress, CancellationToken.None);
            PrintDiagnosis(_diag);
            _lastDiagnosticLog = BuildDiagnosticLog(_diag);
            SaveDiagnosticLog(_lastDiagnosticLog);
        }
        catch (Exception ex)
        {
            Append(I18n.S("检测失败：", "Check failed: ") + ex.Message, Palette.LogRed, true);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void PrintDiagnosis(Diagnosis d)
    {
        Append(d.Mode == ProblemMode.SlowLoading
            ? I18n.S("=== 诊断结果（加载时间过长）===", "=== Diagnosis (slow loading) ===")
            : I18n.S("=== 检测结果（验证失败）===", "=== Check result (verify failed) ==="), Palette.LogGold, true);

        foreach (var item in d.Items)
        {
            var (color, tag) = item.Level switch
            {
                Level.Bad => (Palette.LogRed, "!!"),
                Level.Warn => (Palette.LogOrange, "!"),
                Level.Ok => (Palette.LogGreen, "OK"),
                _ => (Palette.InkDim, "--")
            };
            Append($"  [{tag}] {item.Name}", color, item.Level is Level.Bad or Level.Warn);
            Append($"       {item.Detail}", Palette.Ink);
            if (!string.IsNullOrWhiteSpace(item.Advice) && item.Advice != I18n.S("正常", "OK"))
                Append(I18n.S("       建议：", "       Advice: ") + item.Advice, Palette.InkDim);
        }
        Append("");

        Append(I18n.S("=== 大概率问题（按可能性排序）===", "=== Most likely causes (by score) ==="), Palette.LogGold, true);
        foreach (var f in d.Findings)
        {
            var color = f.Rank >= 80 ? Palette.LogRed : f.Rank >= 50 ? Palette.LogOrange : Palette.LogBlue;
            Append($"  {Stars(f.Rank)} [{f.WeightText}] {f.Title}", color, true);
            Append($"       {f.Reason}", Palette.Ink);
            Append(I18n.S("       怎么修：", "       Fix: ") + f.Fix, Palette.LogGreen);
        }
        Append("");
        Append(">> " + d.Summary, Palette.Ink);
        Append(I18n.S(">> 点击「一键修复」执行自动修复，或按上方建议手动处理。", ">> Click Fix All to auto-fix, or follow the advice above."), Palette.LogGold);
    }

    private static string Stars(int rank) => rank switch
    {
        >= 90 => "*****",
        >= 80 => "****-",
        >= 60 => "***--",
        >= 50 => "**---",
        _ => "*----"
    };

    private async Task RunFixAsync(bool? autoClose = null)
    {
        if (_safeMode)
        {
            PixelDialog.Show(this, I18n.S("安全模式", "Safe mode"),
                I18n.S("当前是安全模式：只诊断，不修改任何系统设置。\n关闭安全模式后重开程序即可修复。",
                       "Safe mode: diagnose only, nothing is changed.\nRestart without --safe to enable fixes."));
            return;
        }
        if (_busy) return;
        var mode = _mode;
        if (_diag == null || _diag.Mode != mode)
        {
            await RunScanAsync();
            if (_diag == null) return;
        }

        bool allowClose = true;
        if (autoClose.HasValue)
        {
            allowClose = autoClose.Value;
        }
        else
        {
            var running = new[] { "msedge", "chrome" }.Any(n => Process.GetProcessesByName(n).Length > 0);
            if (running)
            {
                var ans = PixelDialog.Show(this, I18n.S("一键修复", "Fix All"),
                    I18n.S(
                        "清理 Cloudflare 验证 Cookie 需要关闭正在运行的浏览器（Edge / Chrome）。\n\n" +
                        "选择「是」：自动关闭浏览器并清理（请先保存网页中的未保存内容）\n" +
                        "选择「否」：跳过 Cookie 清理，其他修复照常执行",
                        "Cleaning Cloudflare cookies requires closing running browsers (Edge / Chrome).\n\n" +
                        "Yes: close browsers automatically and clean (save your work first)\n" +
                        "No: skip cookie cleanup, run other fixes"),
                    yesNo: true);
                allowClose = ans == DialogResult.Yes;
            }
        }

        SetBusy(true);
        _log.SelectionStart = _log.TextLength;
        _log.SelectionLength = 0;
        _log.SelectionColor = Palette.LogGold;
        _log.SelectionFont = new Font(_log.Font, FontStyle.Bold);
        _log.AppendText(Environment.NewLine + I18n.S("=== 一键修复 ===", "=== Fix All ===") + Environment.NewLine);
        _log.ScrollToCaret();
        try
        {
            var progress = new Progress<string>(m => Append("  " + m, Palette.InkDim));
            _fixResult = await FixEngine.RunAsync(_diag, allowClose, mode, progress, CancellationToken.None);
            PrintFixResult(_fixResult);
            _btnOpen.Enabled = !string.IsNullOrEmpty(_fixResult.ReportPath);

            PixelDialog.Show(this, I18n.S("修复完成", "Done"), BuildFixSummary(_fixResult));
        }
        catch (Exception ex)
        {
            Append(I18n.S("修复失败：", "Fix failed: ") + ex.Message, Palette.LogRed, true);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void PrintFixResult(FixEngine.FixResult r)
    {
        Append("");
        foreach (var a in r.Actions)
        {
            var color = a.Level switch
            {
                Level.Ok => Palette.LogGreen,
                Level.Warn => Palette.LogOrange,
                Level.Bad => Palette.LogRed,
                _ => Palette.InkDim
            };
            Append($"  [{a.Result}] {a.Title}", color, true);
            Append($"       {a.Detail}", Palette.Ink);
        }
        Append("");
        if (!string.IsNullOrEmpty(r.ReportPath))
            Append(I18n.S(">> 修复报告：", ">> Report: ") + r.ReportPath, Palette.LogBlue);
    }

    private string BuildFixSummary(FixEngine.FixResult r)
    {
        var fixedCount = r.Actions.Count(a => a.Result == I18n.S("已修复", "Fixed") || a.Result == "Fixed");
        var manual = r.Actions.Count(a => a.Result == I18n.S("需手动", "Manual") || a.Result == "Manual");
        var sb = new System.Text.StringBuilder();
        var top = _diag?.Findings.FirstOrDefault();
        if (top != null)
        {
            sb.AppendLine(I18n.S("检测结论（大概率问题）：", "Most likely cause:"));
            sb.AppendLine("  " + top.Title);
            sb.AppendLine();
        }
        sb.AppendLine(I18n.S(
            $"本次共执行 {r.Actions.Count} 项，自动修复 {fixedCount} 项。",
            $"{r.Actions.Count} actions, {fixedCount} auto-fixed."));
        if (r.CookiesRemoved > 0) sb.AppendLine(I18n.S($"· 清理 Cloudflare 验证 Cookie {r.CookiesRemoved} 条", $"· Removed {r.CookiesRemoved} Cloudflare cookies"));
        if (r.BrowsersClosed > 0) sb.AppendLine(I18n.S($"· 关闭浏览器 {r.BrowsersClosed} 个", $"· Closed {r.BrowsersClosed} browser(s)"));
        sb.AppendLine();
        foreach (var a in r.Actions.Where(a => a.Result is "已修复" or "需手动" or "已跳过" or "Fixed" or "Manual" or "Skipped"))
            sb.AppendLine($"· [{a.Result}] {a.Title}");
        if (manual > 0)
        {
            sb.AppendLine();
            sb.AppendLine(I18n.S("有需要手动处理的项，请查看日志与修复报告中的说明。", "Some items need manual action. See the log and report."));
        }
        sb.AppendLine();
        sb.AppendLine(I18n.S("下一步：重新打开浏览器，刷新卡验证的页面再试。", "Next: reopen your browser and refresh the page."));
        return sb.ToString();
    }

    private async Task RestoreBackupAsync()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "backup");
        if (!Directory.Exists(root))
        {
            PixelDialog.Show(this, I18n.S("恢复备份", "Restore backup"),
                I18n.S("没有找到备份目录。先执行一次「一键修复」才会生成备份。",
                       "No backup folder found. Run Fix All once to create a backup."));
            return;
        }
        var latest = Directory.GetDirectories(root).OrderByDescending(d => d).FirstOrDefault();
        if (latest == null)
        {
            PixelDialog.Show(this, I18n.S("恢复备份", "Restore backup"),
                I18n.S("备份目录是空的。", "The backup folder is empty."));
            return;
        }

        var confirm = PixelDialog.Show(this, I18n.S("恢复备份", "Restore backup"),
            I18n.S($"将恢复最近一次备份：{Path.GetFileName(latest)}\n\n恢复内容：浏览器 Cookies、hosts 文件。\n恢复前会关闭浏览器（如有运行）。",
                   $"Restore the latest backup: {Path.GetFileName(latest)}\n\nCookies and hosts will be restored.\nBrowsers will be closed if running."),
            yesNo: true);
        if (confirm != DialogResult.Yes) return;

        SetBusy(true);
        try
        {
            foreach (var p in new[] { "msedge", "chrome" }.SelectMany(Process.GetProcessesByName))
            {
                try { p.CloseMainWindow(); } catch { }
            }
            await Task.Delay(4000);
            foreach (var n in new[] { "msedge", "chrome" })
                foreach (var p in Process.GetProcessesByName(n))
                {
                    try { p.Kill(true); } catch { }
                }
            await Task.Delay(1000);

            int ok = 0, fail = 0;
            foreach (var dir in Directory.GetDirectories(latest))
            {
                var name = Path.GetFileName(dir);
                var sep = name.IndexOf('_');
                if (sep <= 0) continue;
                var browser = name[..sep];
                var profileName = name[(sep + 1)..];
                var cookies = Path.Combine(dir, "Cookies");
                if (!File.Exists(cookies)) continue;
                var target = FindCookiesPath(browser, profileName);
                if (target == null) { fail++; continue; }
                try { File.Copy(cookies, target, true); ok++; } catch { fail++; }
            }

            var hostsBackup = Path.Combine(latest, "hosts");
            if (File.Exists(hostsBackup))
            {
                try
                {
                    File.Copy(hostsBackup,
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts"), true);
                    ok++;
                }
                catch { fail++; }
            }

            Append(I18n.S("已恢复备份：", "Backup restored: ") + Path.GetFileName(latest) +
                   I18n.S($"（成功 {ok}，失败 {fail}）", $" (ok {ok}, failed {fail})"), Palette.LogGreen, true);
            PixelDialog.Show(this, I18n.S("恢复备份", "Restore backup"),
                I18n.S($"恢复完成：成功 {ok} 项，失败 {fail} 项。\n\n失败项可能需要管理员权限，或浏览器没有完全关闭。",
                       $"Restore finished: {ok} ok, {fail} failed.\n\nFailures may need administrator rights or fully closed browsers."));
        }
        finally
        {
            SetBusy(false);
        }
    }

    private static string? FindCookiesPath(string browser, string profileName)
    {
        try
        {
            var baseDir = browser.Equals("Edge", StringComparison.OrdinalIgnoreCase)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Edge\User Data")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\User Data");
            if (!Directory.Exists(baseDir)) return null;
            foreach (var dir in Directory.GetDirectories(baseDir))
            {
                var name = Path.GetFileName(dir);
                if (!name.Equals("Default", StringComparison.OrdinalIgnoreCase) &&
                    !name.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase)) continue;
                var display = name;
                try
                {
                    var pref = Path.Combine(dir, "Preferences");
                    if (File.Exists(pref))
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(pref));
                        if (doc.RootElement.TryGetProperty("profile", out var prof) &&
                            prof.TryGetProperty("name", out var pn) &&
                            pn.ValueKind == System.Text.Json.JsonValueKind.String)
                            display = pn.GetString() ?? name;
                    }
                }
                catch { }
                if (display.Equals(profileName, StringComparison.OrdinalIgnoreCase) ||
                    name.Equals(profileName, StringComparison.OrdinalIgnoreCase))
                {
                    var c = Path.Combine(dir, "Network", "Cookies");
                    if (!File.Exists(c)) c = Path.Combine(dir, "Cookies");
                    return c;
                }
            }
        }
        catch { }
        return null;
    }

    private void OpenReport()
    {
        if (_fixResult != null && File.Exists(_fixResult.ReportPath))
        {
            try { Process.Start(new ProcessStartInfo("notepad.exe", $"\"{_fixResult.ReportPath}\"") { UseShellExecute = true }); }
            catch { }
        }
    }
}
