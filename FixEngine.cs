using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;

namespace CfValidateFixer;

public static class FixEngine
{
    public sealed class FixResult
    {
        public List<FixAction> Actions = new();
        public string ReportPath = "";
        public string ReportText = "";
        public int CookiesRemoved;
        public int BrowsersClosed;
    }

    public static async Task<FixResult> RunAsync(Diagnosis d, bool allowCloseBrowser, ProblemMode mode, IProgress<string> progress, CancellationToken ct)
    {
        var result = new FixResult();
        var backupRoot = GetBackupRoot(DateTime.Now.ToString("yyyyMMdd_HHmmss"));

        progress.Report(I18n.S("清理 Cloudflare 验证残留 Cookie ...", "Cleaning leftover Cloudflare cookies ..."));
        var browserNames = new[] { "msedge", "chrome" };
        bool anyRunning = browserNames.Any(n => Process.GetProcessesByName(n).Length > 0);
        if (anyRunning)
        {
            if (!allowCloseBrowser)
            {
                result.Actions.Add(new FixAction
                {
                    Title = I18n.S("清理 Cloudflare 验证 Cookie", "Clean Cloudflare cookies"),
                    Result = I18n.S("已跳过", "Skipped"), Level = Level.Warn,
                    Detail = I18n.S("浏览器正在运行且未允许自动关闭，跳过 Cookie 清理。关闭浏览器后重新运行即可清理。",
                        "Browsers are running and auto-close was not allowed; cookie cleanup skipped. Close them and run again.")
                });
            }
            else
            {
                foreach (var n in browserNames)
                {
                    var procs = Process.GetProcessesByName(n);
                    foreach (var p in procs)
                    {
                        try { p.CloseMainWindow(); } catch { }
                    }
                    if (procs.Length > 0) result.BrowsersClosed++;
                }
                await Task.Delay(5000, ct);
                foreach (var n in browserNames)
                {
                    foreach (var p in Process.GetProcessesByName(n))
                    {
                        try { p.Kill(true); } catch { }
                    }
                }
                await Task.Delay(1500, ct);
            }
        }

        int removed = 0;
        var perBrowser = new List<string>();
        foreach (var b in d.Browsers)
        {
            foreach (var p in b.Profiles)
            {
                if (!File.Exists(p.CookiesPath)) continue;
                try
                {
                    if (!string.IsNullOrEmpty(backupRoot))
                    {
                        var backupDir = Path.Combine(backupRoot, b.Name + "_" + p.Name);
                        Directory.CreateDirectory(backupDir);
                        var backupFile = Path.Combine(backupDir, "Cookies");
                        try { File.Copy(p.CookiesPath, backupFile, true); } catch { }
                    }

                    using var conn = new SqliteConnection($"Data Source={p.CookiesPath};Default Timeout=5");
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"delete from cookies
                        where lower(host_key) like '%cloudflare%'
                           or lower(name) like 'cf_%'
                           or lower(name) in ('cf_clearance','__cf_bm')";
                    var n = cmd.ExecuteNonQuery();
                    removed += n;
                    if (n > 0) perBrowser.Add($"{b.Name}/{p.Name}: {n}");
                    using var vcmd = conn.CreateCommand();
                    vcmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE)";
                    try { vcmd.ExecuteNonQuery(); } catch { }
                }
                catch (Exception ex)
                {
                    perBrowser.Add($"{b.Name}/{p.Name}: " + I18n.S("清理失败（", "failed (") + ex.Message + "）");
                }
            }
        }
        result.CookiesRemoved = removed;
        result.Actions.Add(new FixAction
        {
            Title = I18n.S("清理 Cloudflare 验证残留 Cookie", "Clean Cloudflare cookies"),
            Result = removed > 0 ? I18n.S("已修复", "Fixed") : I18n.S("无需处理", "Nothing to do"),
            Level = removed > 0 ? Level.Ok : Level.Info,
            Detail = removed > 0
                ? I18n.S($"共删除 {removed} 条 cf_clearance / __cf_bm / Cloudflare 相关 Cookie。", $"Removed {removed} cf_clearance / __cf_bm / Cloudflare cookies.") +
                  (perBrowser.Count > 0 ? " " + string.Join("; ", perBrowser) : "") +
                  (string.IsNullOrEmpty(backupRoot) ? "" : I18n.S($"。原始数据库已备份到 {backupRoot}", $". Original DB backed up to {backupRoot}"))
                : I18n.S("没有找到需要清理的 Cookie。", "No cookies needed cleaning.")
        });

        progress.Report(I18n.S("刷新 DNS 缓存 ...", "Flushing DNS ..."));
        var flush = RunProcess("ipconfig", "/flushdns");
        result.Actions.Add(new FixAction
        {
            Title = I18n.S("刷新 DNS 缓存", "Flush DNS cache"),
            Result = flush.Ok ? I18n.S("已修复", "Fixed") : I18n.S("需管理员", "Needs admin"),
            Level = flush.Ok ? Level.Ok : Level.Warn,
            Detail = flush.Ok
                ? I18n.S("已执行 ipconfig /flushdns，使 Cloudflare 域名的解析缓存失效。", "Ran ipconfig /flushdns; Cloudflare lookups are no longer cached.")
                : I18n.S("刷新失败（可能需要管理员权限）：", "Failed (may need admin): ") + flush.Output
        });

        if (Math.Abs(d.TimeOffsetSec) > 300)
        {
            progress.Report(I18n.S("校准系统时间 ...", "Syncing system clock ..."));
            var resync = RunProcess("w32tm", "/resync");
            result.Actions.Add(new FixAction
            {
                Title = I18n.S("校准系统时间", "Sync system clock"),
                Result = resync.Ok ? I18n.S("已修复", "Fixed") : I18n.S("需管理员", "Needs admin"),
                Level = resync.Ok ? Level.Ok : Level.Warn,
                Detail = resync.Ok
                    ? I18n.S($"原偏差 {Math.Abs(d.TimeOffsetSec):F0} 秒，已请求与时间服务器同步。", $"Was off by {Math.Abs(d.TimeOffsetSec):F0}s; sync requested.")
                    : I18n.S("自动校准失败（需要管理员权限）。请手动开启“自动设置时间”。", "Auto-sync failed (needs admin). Enable automatic time manually.")
            });
        }
        else
        {
            result.Actions.Add(new FixAction
            {
                Title = I18n.S("校准系统时间", "Sync system clock"),
                Result = I18n.S("无需处理", "Nothing to do"), Level = Level.Info,
                Detail = I18n.S($"时间偏差仅 {Math.Abs(d.TimeOffsetSec):F1} 秒，未做改动。", $"Clock is only {Math.Abs(d.TimeOffsetSec):F1}s off; left unchanged.")
            });
        }

        if (d.HostsHit)
        {
            progress.Report(I18n.S("修复 hosts 中的 Cloudflare 屏蔽条目 ...", "Fixing hosts entries ..."));
            var fixedCount = TryFixHosts(backupRoot, out var hostsDetail);
            result.Actions.Add(new FixAction
            {
                Title = I18n.S("修复 hosts 屏蔽条目", "Fix hosts entries"),
                Result = fixedCount > 0 ? I18n.S("已修复", "Fixed") : I18n.S("需管理员", "Needs admin"),
                Level = fixedCount > 0 ? Level.Ok : Level.Warn,
                Detail = hostsDetail
            });
        }
        else
        {
            result.Actions.Add(new FixAction
            {
                Title = I18n.S("修复 hosts 屏蔽条目", "Fix hosts entries"),
                Result = I18n.S("无需处理", "Nothing to do"), Level = Level.Info,
                Detail = I18n.S("hosts 中未发现 Cloudflare 屏蔽条目。", "No Cloudflare blocking entries found.")
            });
        }

        progress.Report(I18n.S("检查失效的代理配置 ...", "Checking dead proxy ..."));
        var proxyDetail = TryDisableDeadProxy();
        if (proxyDetail != null)
            result.Actions.Add(new FixAction
            {
                Title = I18n.S("关闭失效的系统代理", "Disable dead system proxy"),
                Result = I18n.S("已修复", "Fixed"), Level = Level.Ok, Detail = proxyDetail
            });
        else
            result.Actions.Add(new FixAction
            {
                Title = I18n.S("关闭失效的系统代理", "Disable dead system proxy"),
                Result = I18n.S("无需处理", "Nothing to do"), Level = Level.Info,
                Detail = I18n.S("系统代理未开启或代理端口可正常连接。", "Proxy is off or reachable.")
            });

        if (d.EnabledBlockers > 0)
        {
            var names = d.Browsers.SelectMany(b => b.Extensions).Where(e => e.Enabled && e.Blocker)
                .Select(e => e.Name).Distinct().ToList();
            result.Actions.Add(new FixAction
            {
                Title = I18n.S("处理拦截类浏览器扩展", "Handle blocker extensions"),
                Result = I18n.S("需手动", "Manual"), Level = Level.Warn,
                Detail = I18n.S("检测到：", "Detected: ") + string.Join("、", names) +
                    I18n.S("。出于安全考虑不自动禁用扩展。请在扩展设置中把卡验证的网站加入白名单（例如 AdGuard：点击工具栏图标 → “在此网站上暂停过滤”），或临时关闭这些扩展后刷新页面。",
                        ". Extensions are not disabled automatically. Allow-list the site (e.g. AdGuard: pause filtering on this site) or disable them temporarily.")
            });
        }
        var proxyExts = d.Browsers.SelectMany(b => b.Extensions)
            .Where(e => e.Enabled && e.Proxy).Select(e => e.Name).Distinct().ToList();
        if (proxyExts.Count > 0)
        {
            result.Actions.Add(new FixAction
            {
                Title = I18n.S("处理代理类浏览器扩展", "Handle proxy extensions"),
                Result = I18n.S("需手动", "Manual"), Level = Level.Warn,
                Detail = I18n.S("检测到：", "Detected: ") + string.Join("、", proxyExts) +
                    I18n.S("。Cloudflare 可能因其出口 IP 被风控，建议临时关闭扩展或更换节点后重试。",
                        ". Cloudflare may flag the exit IP; disable the extension or switch nodes.")
            });
        }

        if (mode == ProblemMode.SlowLoading)
        {
            result.Actions.Add(new FixAction
            {
                Title = I18n.S("加载慢的额外建议", "Extra advice for slow loading"),
                Result = I18n.S("需手动", "Manual"), Level = Level.Info,
                Detail = I18n.S("若页面仍然转圈：关闭浏览器 QUIC（edge://flags 搜索 quic 设为 Disabled）、更新浏览器、换手机热点对比；若 cloudflarestatus.com 显示故障，只能等 Cloudflare 恢复。",
                    "If it still spins: disable QUIC (edge://flags), update the browser, compare with a hotspot; if cloudflarestatus.com shows an outage, wait it out.")
            });
        }

        var sb = new StringBuilder();
        sb.AppendLine(I18n.S("Cloudflare 加载慢诊断报告", "Cloudflare slow-loading report"));
        sb.AppendLine(I18n.S("生成时间：", "Generated: ") + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine(new string('-', 60));
        sb.AppendLine(I18n.S("【检测结论：大概率问题】", "[Most likely cause]"));
        var top = d.Findings.FirstOrDefault();
        sb.AppendLine("  " + (top?.Title ?? "-"));
        if (top != null) sb.AppendLine(I18n.S("  依据：", "  Reason: ") + top.Reason);
        sb.AppendLine();
        sb.AppendLine(I18n.S("【本次修复动作】", "[Actions]"));
        foreach (var a in result.Actions)
        {
            sb.AppendLine($"  [{a.Result}] {a.Title}");
            sb.AppendLine("      " + a.Detail);
        }
        sb.AppendLine();
        sb.AppendLine(I18n.S("【建议下一步】", "[Next steps]"));
        int idx = 1;
        if (d.EnabledBlockers > 0)
            sb.AppendLine($"  {idx++}. " + I18n.S("先处理拦截类扩展（白名单/暂停过滤），这是本次检测到的最可能原因。", "Handle blocker extensions first (allow-list/pause filtering)."));
        if (removed > 0)
            sb.AppendLine($"  {idx++}. " + I18n.S("重新打开浏览器，访问卡验证的网站并刷新页面。", "Reopen the browser and refresh the page."));
        sb.AppendLine($"  {idx}. " + I18n.S("若仍然失败：换无痕窗口、换浏览器、换手机热点对比；必要时联系网站客服。", "If it still fails: incognito, another browser, a hotspot; contact the site's support."));
        sb.AppendLine();
        sb.AppendLine(I18n.S("原始 Cookie 数据库备份：", "Original cookie DB backup: ") +
            (string.IsNullOrEmpty(backupRoot) ? I18n.S("（未能创建备份目录，未备份）", "(no backup dir)") : backupRoot));
        sb.AppendLine();
        sb.AppendLine(I18n.S("工具：" + AppInfo.ProductName + " v" + AppInfo.Version + " · 作者：" + AppInfo.Author,
            "Tool: " + AppInfo.ProductNameEn + " v" + AppInfo.Version + " · Author: " + AppInfo.Author));
        result.ReportText = sb.ToString();

        try
        {
            var candidates = new List<string>
            {
                Path.Combine(AppContext.BaseDirectory, I18n.S("修复报告_", "fix-report_") + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt"),
                Path.Combine(Path.GetTempPath(), I18n.S("修复报告_", "fix-report_") + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), I18n.S("修复报告_", "fix-report_") + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt")
            };
            foreach (var path in candidates)
            {
                try
                {
                    File.WriteAllText(path, result.ReportText, Encoding.UTF8);
                    result.ReportPath = path;
                    break;
                }
                catch { }
            }
        }
        catch { }

        return result;
    }

    private static string GetBackupRoot(string stamp)
    {
        var candidates = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "backup", stamp),
            Path.Combine(Path.GetTempPath(), "CfValidateFixer", "backup", stamp),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "CfValidateFixer_backup", stamp)
        };
        foreach (var c in candidates)
        {
            try
            {
                Directory.CreateDirectory(c);
                return c;
            }
            catch { }
        }
        return "";
    }

    private static (bool Ok, string Output) RunProcess(string file, string args)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(file, args)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            });
            if (p == null) return (false, I18n.S("无法启动进程", "Cannot start process"));
            var output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
            if (!p.WaitForExit(8000)) { try { p.Kill(); } catch { } return (false, I18n.S("执行超时", "Timed out")); }
            return (p.ExitCode == 0, output.Trim());
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static int TryFixHosts(string backupRoot, out string detail)
    {
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");
            var lines = File.ReadAllLines(path, Encoding.UTF8).ToList();
            if (!string.IsNullOrEmpty(backupRoot))
            {
                var backup = Path.Combine(backupRoot, "hosts");
                try { File.Copy(path, backup, true); } catch { }
            }
            var targets = new[] { "challenges.cloudflare.com", "cloudflare.com", "www.cloudflare.com" };
            int changed = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith('#')) continue;
                var parts = line.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;
                var host = parts[1].ToLowerInvariant().TrimEnd('.');
                if (targets.Contains(host))
                {
                    lines[i] = "# [CfValidateFixer] " + lines[i];
                    changed++;
                }
            }
            if (changed > 0)
            {
                File.WriteAllLines(path, lines, new UTF8Encoding(false));
                detail = I18n.S($"已注释 {changed} 条屏蔽条目，原文件备份到 {backupRoot}。", $"Commented {changed} blocking entries; backup at {backupRoot}.");
                return changed;
            }
            detail = I18n.S("未找到可修复的条目。", "No fixable entries found.");
            return 0;
        }
        catch (Exception ex)
        {
            detail = I18n.S("修复失败（需要管理员权限写入 hosts）：", "Fix failed (admin required to write hosts): ") + ex.Message;
            return 0;
        }
    }

    private static string? TryDisableDeadProxy()
    {
        try
        {
            var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true);
            if (key == null) return null;
            var enable = key.GetValue("ProxyEnable");
            if (!(enable is int i && i == 1)) return null;
            var server = key.GetValue("ProxyServer") as string;
            if (string.IsNullOrWhiteSpace(server)) return null;
            var hostPort = server.Contains('=') ? server.Split(';').FirstOrDefault(s => s.Contains('='))?.Split('=')[1] : server;
            if (string.IsNullOrWhiteSpace(hostPort)) return null;
            var hp = hostPort.Split(':');
            var host = hp[0];
            var port = hp.Length > 1 && int.TryParse(hp[1], out var p) ? p : 8080;
            bool reachable;
            try
            {
                using var client = new TcpClient();
                var task = client.ConnectAsync(host, port);
                reachable = task.Wait(800) && client.Connected;
            }
            catch { reachable = false; }
            if (reachable) return null;
            key.SetValue("ProxyEnable", 0, RegistryValueKind.DWord);
            return I18n.S($"系统代理 {server} 无法连接，已关闭系统代理（不影响代理软件本身，随时可在 Windows 设置中重新打开）。",
                $"System proxy {server} is unreachable; disabled it (the proxy app itself is untouched; re-enable anytime in Windows Settings).");
        }
        catch { return null; }
    }
}
