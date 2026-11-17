using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace CfValidateFixer;

/// <summary>问题反馈窗口：SMTP 直发 / 邮件客户端 / 本地保存三通道（中英双语）。</summary>
public sealed class FeedbackForm : Form
{
    private readonly PixelRadio _r1 = new();
    private readonly PixelRadio _r2 = new();
    private readonly PixelRadio _r3 = new();
    private readonly TextBox _desc = new();
    private readonly TextBox _contact = new();
    private readonly Label _hint = new();
    private readonly PixelButton _send = new();
    private readonly PixelButton _copy = new();
    private readonly PixelButton _close = new();
    private readonly FeedbackConfig _cfg;
    private readonly string? _diagLog;
    private bool _sending;

    public FeedbackForm(string? diagnosticLog = null)
    {
        _cfg = FeedbackConfig.Load();
        _diagLog = diagnosticLog;
        Text = I18n.S("问题反馈", "Feedback");
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.None;
        Width = 780;
        Height = 640;
        BackColor = Palette.Panel;
        DoubleBuffered = true;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

        var title = new PixelLabel
        {
            Text = I18n.S("问题反馈", "Feedback"),
            Font = PixelFont.Create(34, FontStyle.Bold),
            ForeColor = Palette.Accent,
            Size = new Size(340, 52),
            Location = new Point(36, 24)
        };
        Controls.Add(title);

        var sub = new Label
        {
            Text = _cfg.CanSmtp
                ? I18n.S("填写后点「发送反馈」，会直接发到开发者邮箱。", "Fill it in and click Send; it goes straight to the developer's inbox.")
                : I18n.S("填写后点「发送反馈」；未配置邮箱时会保存到本地并复制。",
                         "Click Send; without a mailbox the message is saved locally and copied."),
            Font = PixelFont.Create(17, FontStyle.Bold),
            ForeColor = Palette.TextDim,
            AutoSize = false,
            Width = 700,
            Height = 28,
            Location = new Point(38, 72)
        };
        Controls.Add(sub);

        var typeLabel = new Label { Text = I18n.S("问题类型：", "Type:"), Font = PixelFont.Create(18, FontStyle.Bold), ForeColor = Palette.Text, AutoSize = true, Location = new Point(40, 108) };
        Controls.Add(typeLabel);

        _r1.Text = I18n.S("验证失败", "Verify failed"); _r1.Location = new Point(150, 100); _r1.Width = 150; _r1.Checked = true;
        Controls.Add(_r1);
        _r2.Text = I18n.S("加载过久", "Slow loading"); _r2.Location = new Point(310, 100); _r2.Width = 160;
        Controls.Add(_r2);
        _r3.Text = I18n.S("其他", "Other"); _r3.Location = new Point(480, 100); _r3.Width = 120;
        Controls.Add(_r3);

        var descLabel = new Label { Text = I18n.S("问题描述：", "Description:"), Font = PixelFont.Create(18, FontStyle.Bold), ForeColor = Palette.Text, AutoSize = true, Location = new Point(40, 148) };
        Controls.Add(descLabel);

        _desc.Multiline = true;
        _desc.ScrollBars = ScrollBars.Vertical;
        _desc.AcceptsReturn = true;
        _desc.Font = PixelFont.Create(17);
        _desc.BackColor = Palette.Paper;
        _desc.ForeColor = Palette.Ink;
        _desc.BorderStyle = BorderStyle.FixedSingle;
        _desc.Location = new Point(40, 178);
        _desc.Width = 700;
        _desc.Height = 250;
        Controls.Add(_desc);

        var contactLabel = new Label { Text = I18n.S("联系方式（可选，QQ / 微信）：", "Contact (optional, QQ / WeChat):"), Font = PixelFont.Create(18, FontStyle.Bold), ForeColor = Palette.Text, AutoSize = true, Location = new Point(40, 446) };
        Controls.Add(contactLabel);

        _contact.Font = PixelFont.Create(17);
        _contact.BackColor = Palette.Paper;
        _contact.ForeColor = Palette.Ink;
        _contact.BorderStyle = BorderStyle.FixedSingle;
        _contact.Location = new Point(320, 440);
        _contact.Width = 420;
        Controls.Add(_contact);

        _hint.Font = PixelFont.Create(15, FontStyle.Bold);
        _hint.ForeColor = Color.FromArgb(236, 214, 170);
        _hint.AutoSize = false;
        _hint.Width = 700;
        _hint.Height = 24;
        _hint.Location = new Point(40, 488);
        Controls.Add(_hint);

        if (!string.IsNullOrWhiteSpace(_diagLog))
        {
            var attachHint = new Label
            {
                Text = I18n.S("本次反馈会自动附带诊断日志（不含账号、Cookie 内容）",
                              "This feedback will include the diagnostic log (no accounts or cookie values)"),
                Font = PixelFont.Create(15, FontStyle.Bold),
                ForeColor = Color.FromArgb(180, 220, 180),
                AutoSize = false,
                Width = 700,
                Height = 24,
                Location = new Point(40, 514)
            };
            Controls.Add(attachHint);
        }

        _send.Text = I18n.S("发送反馈", "Send");
        _send.Accent = true;
        _send.Size = new Size(170, 52);
        _send.Location = new Point(40, 556);
        _send.Click += async (_, _) => await SendAsync();
        Controls.Add(_send);

        _copy.Text = I18n.S("复制内容", "Copy");
        _copy.Size = new Size(150, 52);
        _copy.Location = new Point(226, 556);
        _copy.Click += (_, _) => CopyAll();
        Controls.Add(_copy);

        _close.Text = I18n.S("关闭", "Close");
        _close.Size = new Size(130, 52);
        _close.Location = new Point(610, 556);
        _close.Click += (_, _) => Close();
        Controls.Add(_close);

        UiScale.Apply(this);
    }

    private string SelectedType => _r2.Checked ? I18n.S("加载过久", "Slow loading")
        : _r3.Checked ? I18n.S("其他", "Other") : I18n.S("验证失败", "Verify failed");

    private string BuildBody()
    {
        var sb = new StringBuilder();
        sb.AppendLine(I18n.S("【问题类型】", "[Type] ") + SelectedType);
        sb.AppendLine(I18n.S("【问题描述】", "[Description]"));
        sb.AppendLine(_desc.Text.Trim());
        if (!string.IsNullOrWhiteSpace(_contact.Text))
            sb.AppendLine(I18n.S("【联系方式】", "[Contact] ") + _contact.Text.Trim());
        sb.AppendLine();
        sb.AppendLine(I18n.S("—— 以下由程序自动附加 ——", "—— Added automatically ——"));
        sb.AppendLine(I18n.S("程序：", "App: ") + AppInfo.ProductName + " v" + AppInfo.Version);
        sb.AppendLine(I18n.S("系统：", "OS: ") + Environment.OSVersion.VersionString);
        sb.AppendLine(I18n.S("时间：", "Time: ") + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        if (!string.IsNullOrWhiteSpace(_diagLog))
        {
            sb.AppendLine();
            sb.AppendLine(I18n.S("—— 诊断日志 ——", "—— Diagnostic log ——"));
            sb.AppendLine(_diagLog);
        }
        return sb.ToString();
    }

    private void CopyAll()
    {
        try
        {
            Clipboard.SetText(BuildBody());
            _hint.Text = I18n.S("已复制到剪贴板，可直接粘贴发给我。", "Copied to clipboard. Paste it to me anytime.");
        }
        catch { _hint.Text = I18n.S("复制失败，请手动选择文本复制。", "Copy failed, please select and copy manually."); }
    }

    private async Task SendAsync()
    {
        if (_sending) return;
        if (string.IsNullOrWhiteSpace(_desc.Text))
        {
            _hint.Text = I18n.S("请先填写问题描述。", "Please describe the problem first.");
            return;
        }
        _sending = true;
        _send.Enabled = false;
        _hint.Text = I18n.S("正在发送...", "Sending...");

        var body = BuildBody();
        var subject = I18n.S("【Cloudflare 修复工坊】用户反馈 - ", "[Cloudflare Fix Workshop] Feedback - ") + SelectedType;

        if (_cfg.CanSmtp)
        {
            try
            {
                await Task.Run(() =>
                {
                    using var client = new SmtpClient(_cfg.SmtpHost, _cfg.SmtpPort)
                    {
                        EnableSsl = true,
                        Credentials = new NetworkCredential(_cfg.Email, _cfg.SmtpAuth),
                        Timeout = 15000
                    };
                    using var mail = new MailMessage(_cfg.Email, _cfg.Email, subject, body);
                    client.Send(mail);
                });
                _hint.Text = I18n.S("发送成功，谢谢反馈！", "Sent. Thank you!");
                _desc.Clear();
            }
            catch (Exception ex)
            {
                SaveLocal(body, subject);
                _hint.Text = I18n.S("邮件发送失败（", "Sending failed (") + ex.Message + I18n.S("），已保存到本地并复制。", "); saved locally and copied.");
                CopyAll();
            }
        }
        else
        {
            SaveLocal(body, subject);
            CopyAll();
            if (!string.IsNullOrWhiteSpace(_cfg.Email))
            {
                try
                {
                    var mailto = "mailto:" + _cfg.Email +
                        "?subject=" + Uri.EscapeDataString(subject) +
                        "&body=" + Uri.EscapeDataString(body);
                    Process.Start(new ProcessStartInfo(mailto) { UseShellExecute = true });
                    _hint.Text = I18n.S("已尝试打开邮件客户端；如未配置，请用剪贴板内容发给我。", "Tried to open your mail client; otherwise paste the clipboard content.");
                }
                catch
                {
                    _hint.Text = I18n.S("已保存到本地并复制到剪贴板，请手动发给我。", "Saved locally and copied; please send it to me manually.");
                }
            }
            else
            {
                _hint.Text = I18n.S("开发者暂未配置邮箱：内容已保存到本地并复制，可通过 QQ / 微信发给我。", "No mailbox configured: content saved locally and copied; send it via QQ / WeChat.");
            }
        }

        _sending = false;
        _send.Enabled = true;
    }

    private void SaveLocal(string body, string subject)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory,
                "feedback_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
            File.WriteAllText(path, subject + Environment.NewLine + new string('-', 50) + Environment.NewLine + body, Encoding.UTF8);
        }
        catch { }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.None;
        using (var p = new Pen(Palette.BorderDim, 4)) g.DrawRectangle(p, 1, 1, Width - 3, Height - 3);
        using (var p = new Pen(Palette.Border, 2)) g.DrawRectangle(p, 5, 5, Width - 11, Height - 11);
    }
}
