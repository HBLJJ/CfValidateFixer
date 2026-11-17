using System.Text;

namespace CfValidateFixer;

/// <summary>反馈与赞助的本地配置（可被用户编辑）。</summary>
public sealed class FeedbackConfig
{
    public string Email = "";
    public string SmtpHost = "smtp.qq.com";
    public int SmtpPort = 587;
    public string SmtpAuth = "";

    public bool CanSmtp => !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(SmtpAuth);

    public static string ConfigPath => Path.Combine(AppContext.BaseDirectory, "feedback.config");

    public static FeedbackConfig Load()
    {
        var cfg = new FeedbackConfig();
        try
        {
            if (File.Exists(ConfigPath))
            {
                foreach (var raw in File.ReadAllLines(ConfigPath, Encoding.UTF8))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith('#')) continue;
                    var idx = line.IndexOf('=');
                    if (idx <= 0) continue;
                    var key = line[..idx].Trim().ToLowerInvariant();
                    var val = line[(idx + 1)..].Trim();
                    switch (key)
                    {
                        case "email": cfg.Email = val; break;
                        case "smtp_host": cfg.SmtpHost = val; break;
                        case "smtp_port": if (int.TryParse(val, out var p)) cfg.SmtpPort = p; break;
                        case "smtp_auth": cfg.SmtpAuth = val; break;
                    }
                }
            }
            else
            {
                File.WriteAllText(ConfigPath,
                    "# Cloudflare 修复工坊 - 反馈邮箱配置\n" +
                    "# 填好后保存，程序会自动读取（修改后重启程序生效）\n" +
                    "# QQ 邮箱：设置 → 账户 → 开启 SMTP 服务 → 生成授权码（不是登录密码）\n" +
                    "email=\n" +
                    "smtp_host=smtp.qq.com\n" +
                    "smtp_port=587\n" +
                    "smtp_auth=\n", Encoding.UTF8);
            }
        }
        catch { }
        return cfg;
    }
}
