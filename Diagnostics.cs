using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;

namespace CfValidateFixer;

public static class Diagnostics
{
    private const string ProbeUrl = "https://challenges.cloudflare.com/turnstile/v0/api.js";
    private const string TimeUrl = "https://www.cloudflare.com/cdn-cgi/trace";

    private static readonly string[] BlockerKeywords =
    {
        "adguard", "ublock", "adblock", "ad block", "adblocker", "ghostery", "privacy badger",
        "disconnect", "noscript", "scriptsafe", "tampermonkey", "violentmonkey", "stylus",
        "decentraleyes", "privacy", "tracker", "anti-", "blocker", "广告", "拦截", "去广告", "过滤", "隐私"
    };

    public static async Task<Diagnosis> RunAsync(ProblemMode mode, IProgress<string> progress, CancellationToken ct)
    {
        var d = new Diagnosis { Mode = mode };
        var http = CreateHttp();
        try
        {
            progress.Report(I18n.S("[1/8] 检查系统时间与服务器时间偏差 ...", "[1/8] Checking system clock ..."));
            d.TimeOffsetSec = await CheckTimeAsync(http, ct);
            bool timeBad = Math.Abs(d.TimeOffsetSec) > 300;
            d.Items.Add(new CheckItem
            {
                Name = I18n.S("系统时间", "System clock"),
                Level = timeBad ? Level.Bad : Level.Ok,
                Detail = I18n.S($"与服务器时间偏差 {d.TimeOffsetSec:F1} 秒", $"Offset vs server: {d.TimeOffsetSec:F1}s"),
                Advice = timeBad
                    ? I18n.S("偏差超过 5 分钟，Cloudflare 会判为时钟异常，必须校准", "Over 5 minutes off: Cloudflare treats it as a clock error; must sync")
                    : I18n.S("正常", "OK")
            });
            d.TimeText = timeBad
                ? I18n.S($"系统时间与网络时间相差 {Math.Abs(d.TimeOffsetSec):F0} 秒", $"Clock is off by {Math.Abs(d.TimeOffsetSec):F0}s")
                : I18n.S("系统时间准确", "Clock is accurate");

            progress.Report(I18n.S("[2/8] 检查 challenges.cloudflare.com 的 DNS 解析 ...", "[2/8] Resolving challenges.cloudflare.com ..."));
            var dns = await CheckDnsAsync(d, ct);
            d.DnsDetail = dns.Detail;
            d.Items.Add(dns);
            if (dns.Level == Level.Bad) d.NetworkOk = false;

            progress.Report(I18n.S("[3/8] 测试 Cloudflare 验证域名是否可访问 ...", "[3/8] Testing Cloudflare reachability ..."));
            var net = await CheckHttpAsync(http, ct);
            d.NetworkDetail = net.Detail;
            d.Items.Add(net);
            if (net.Level == Level.Bad) d.NetworkOk = false;

            progress.Report(I18n.S("[4/8] 检查 hosts 是否屏蔽 Cloudflare 域名 ...", "[4/8] Checking hosts file ..."));
            var hosts = CheckHosts();
            d.HostsHit = hosts.Level == Level.Bad;
            d.HostsDetail = hosts.Detail;
            d.Items.Add(hosts);

            progress.Report(I18n.S("[5/8] 检查系统代理 / VPN 配置 ...", "[5/8] Checking proxy / VPN ..."));
            var proxy = CheckProxy();
            d.ProxyOn = proxy.Level != Level.Ok;
            d.ProxyDetail = proxy.Detail;
            d.Items.Add(proxy);

            progress.Report(I18n.S("[6/8] 扫描浏览器扩展（广告拦截、隐私类）...", "[6/8] Scanning browser extensions ..."));
            var browsers = await ProbeBrowsersAsync(progress, ct);
            d.Browsers = browsers;
            foreach (var b in browsers)
            {
                var enabledExts = b.Extensions.Where(e => e.Enabled).ToList();
                var blockers = enabledExts.Where(e => e.Blocker).Select(e => e.Name).ToList();
                var proxies = enabledExts.Where(e => e.Proxy).Select(e => e.Name).ToList();
                var parts = new List<string>();
                if (blockers.Count > 0) parts.Add(I18n.S("拦截类：", "blockers: ") + string.Join(", ", blockers));
                if (proxies.Count > 0) parts.Add(I18n.S("代理类：", "proxy/VPN: ") + string.Join(", ", proxies));
                d.Items.Add(new CheckItem
                {
                    Name = b.Name + I18n.S(" 扩展", " extensions"),
                    Level = blockers.Count > 0 ? Level.Bad : proxies.Count > 0 ? Level.Warn : Level.Ok,
                    Detail = b.Extensions.Count == 0
                        ? I18n.S("未发现扩展信息", "No extension info found")
                        : I18n.S($"共 {b.Extensions.Count} 个扩展，启用 {enabledExts.Count} 个", $"{b.Extensions.Count} extensions, {enabledExts.Count} enabled") +
                          (parts.Count > 0 ? "；" + string.Join("；", parts) : I18n.S("；未发现拦截类/代理类扩展", "; no blocker/proxy extensions")),
                    Advice = blockers.Count > 0
                        ? I18n.S("拦截类扩展会阻止 Cloudflare 验证脚本", "Blockers can stop Cloudflare verification scripts")
                        : proxies.Count > 0
                            ? I18n.S("代理类扩展可能改变出口 IP，触发 Cloudflare 风控", "Proxy extensions may change your exit IP and trigger risk control")
                            : I18n.S("正常", "OK")
                });
            }
            d.EnabledBlockers = browsers.SelectMany(b => b.Extensions).Count(e => e.Enabled && e.Blocker);

            progress.Report(I18n.S("[7/8] 检查浏览器中的 Cloudflare 验证残留数据 ...", "[7/8] Checking leftover Cloudflare cookies ..."));
            int cf = 0, total = 0;
            var cookieItems = new List<CheckItem>();
            foreach (var b in browsers)
            {
                foreach (var p in b.Profiles)
                {
                    cf += p.CfCookieCount;
                    total += p.CookieCount;
                    cookieItems.Add(new CheckItem
                    {
                        Name = $"{b.Name} / {p.Name}",
                        Level = Level.Info,
                        Detail = I18n.S($"Cookie 共 {p.CookieCount} 条，其中 Cloudflare 相关 {p.CfCookieCount} 条", $"{p.CookieCount} cookies, {p.CfCookieCount} Cloudflare-related"),
                        Advice = p.CfCookieCount > 0
                            ? I18n.S("残留的 cf_clearance 等可能已失效，建议清理", "Stale cf_clearance may be invalid; cleaning is recommended")
                            : I18n.S("无残留", "No leftovers")
                    });
                }
            }
            d.CfCookies = cf;
            d.Items.AddRange(cookieItems);
            if (browsers.Count == 0)
                d.Items.Add(new CheckItem
                {
                    Name = I18n.S("浏览器", "Browser"), Level = Level.Info,
                    Detail = I18n.S("未检测到 Edge / Chrome 用户配置", "No Edge / Chrome profile found"),
                    Advice = I18n.S("请手动检查浏览器", "Please check your browser manually")
                });

            progress.Report(I18n.S("[8/8] 读取浏览器隐私设置 ...", "[8/8] Reading browser privacy settings ..."));
            d.Items.AddRange(ProbeBrowserSettings(browsers));

            if (mode == ProblemMode.SlowLoading)
            {
                progress.Report(I18n.S("[9/11] 测试到 Cloudflare 边缘的网络延迟 ...", "[9/11] Measuring network latency ..."));
                await CheckLatencyAsync(d, http, ct);
                progress.Report(I18n.S("[10/11] 查询 Cloudflare 服务状态 ...", "[10/11] Checking Cloudflare status ..."));
                await CheckCloudflareStatusAsync(d, http, ct);
                progress.Report(I18n.S("[11/11] 检查浏览器版本 ...", "[11/11] Checking browser version ..."));
                CheckBrowserVersions(d);
            }

            BuildFindings(d);
            return d;
        }
        finally
        {
            http.Dispose();
        }
    }

    private static HttpClient CreateHttp()
    {
        var handler = new HttpClientHandler { UseProxy = false, AutomaticDecompression = DecompressionMethods.All };
        var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36 Edg/151.0.0.0");
        return http;
    }

    private static async Task<double> CheckTimeAsync(HttpClient http, CancellationToken ct)
    {
        try
        {
            using var resp = await http.GetAsync(TimeUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            if (resp.Headers.Date.HasValue) return (DateTimeOffset.UtcNow - resp.Headers.Date.Value).TotalSeconds;
        }
        catch { }
        try
        {
            using var resp = await http.GetAsync("https://www.baidu.com/", HttpCompletionOption.ResponseHeadersRead, ct);
            if (resp.Headers.Date.HasValue) return (DateTimeOffset.UtcNow - resp.Headers.Date.Value).TotalSeconds;
        }
        catch { }
        return 0;
    }

    private static async Task<CheckItem> CheckDnsAsync(Diagnosis d, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var task = Dns.GetHostAddressesAsync("challenges.cloudflare.com", ct);
            var done = await Task.WhenAny(task, Task.Delay(6000, ct));
            if (done != task)
                return new CheckItem
                {
                    Name = I18n.S("DNS 解析", "DNS lookup"), Level = Level.Bad,
                    Detail = I18n.S("解析超时", "Lookup timed out"),
                    Advice = I18n.S("DNS 可能被污染或网络异常，建议更换公共 DNS", "DNS may be poisoned or network is unstable; try public DNS")
                };
            var addrs = await task;
            d.DnsMs = sw.Elapsed.TotalMilliseconds;
            if (addrs.Length == 0)
                return new CheckItem
                {
                    Name = I18n.S("DNS 解析", "DNS lookup"), Level = Level.Bad,
                    Detail = I18n.S("无法解析 challenges.cloudflare.com", "Cannot resolve challenges.cloudflare.com"),
                    Advice = I18n.S("更换 DNS（如 223.5.5.5）后重试", "Try another DNS (e.g. 223.5.5.5)")
                };
            return new CheckItem
            {
                Name = I18n.S("DNS 解析", "DNS lookup"), Level = Level.Ok,
                Detail = I18n.S("解析到 ", "Resolved to ") + string.Join(", ", addrs.Take(3).Select(a => a.ToString())) + I18n.S($"（耗时 {d.DnsMs:F0} ms）", $" ({d.DnsMs:F0} ms)"),
                Advice = I18n.S("正常", "OK")
            };
        }
        catch (Exception ex)
        {
            return new CheckItem
            {
                Name = I18n.S("DNS 解析", "DNS lookup"), Level = Level.Bad,
                Detail = I18n.S("解析失败：", "Lookup failed: ") + ex.Message,
                Advice = I18n.S("更换公共 DNS 或检查网络", "Change DNS or check your network")
            };
        }
    }

    private static async Task<CheckItem> CheckHttpAsync(HttpClient http, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var resp = await http.GetAsync(ProbeUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            sw.Stop();
            bool ok = (int)resp.StatusCode < 500;
            return new CheckItem
            {
                Name = I18n.S("验证域名连通性", "Endpoint reachability"),
                Level = ok ? Level.Ok : Level.Warn,
                Detail = I18n.S($"HTTP {(int)resp.StatusCode}，耗时 {sw.ElapsedMilliseconds} ms", $"HTTP {(int)resp.StatusCode}, {sw.ElapsedMilliseconds} ms"),
                Advice = ok ? I18n.S("正常", "OK") : I18n.S("返回异常状态码，可能是代理/防火墙拦截", "Unexpected status; possibly proxy/firewall interception")
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new CheckItem
            {
                Name = I18n.S("验证域名连通性", "Endpoint reachability"), Level = Level.Bad,
                Detail = I18n.S("无法访问 challenges.cloudflare.com（", "Cannot reach challenges.cloudflare.com (") + ex.Message + "）",
                Advice = I18n.S("检查网络、代理或防火墙；Turnstile 的 iframe 依赖该域名", "Check network, proxy or firewall; the Turnstile iframe depends on this domain")
            };
        }
    }

    private static CheckItem CheckHosts()
    {
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");
            if (!File.Exists(path))
                return new CheckItem { Name = I18n.S("hosts 文件", "hosts file"), Level = Level.Info, Detail = I18n.S("hosts 文件不存在（正常）", "hosts file not found (normal)"), Advice = I18n.S("无", "None") };
            var hits = new List<string>();
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#')) continue;
                var parts = line.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;
                var host = parts[1].ToLowerInvariant().TrimEnd('.');
                if (host is "challenges.cloudflare.com" or "cloudflare.com" or "www.cloudflare.com")
                    hits.Add(line);
            }
            if (hits.Count > 0)
                return new CheckItem
                {
                    Name = I18n.S("hosts 文件", "hosts file"), Level = Level.Bad,
                    Detail = I18n.S("发现屏蔽 Cloudflare 的条目：", "Blocking entries found: ") + string.Join(" | ", hits),
                    Advice = I18n.S("删除或注释这些行后刷新 DNS", "Remove/comment these lines and flush DNS")
                };
            return new CheckItem { Name = I18n.S("hosts 文件", "hosts file"), Level = Level.Ok, Detail = I18n.S("未发现 Cloudflare 相关屏蔽条目", "No Cloudflare blocking entries"), Advice = I18n.S("正常", "OK") };
        }
        catch (Exception ex)
        {
            return new CheckItem
            {
                Name = I18n.S("hosts 文件", "hosts file"), Level = Level.Warn,
                Detail = I18n.S("读取失败：", "Read failed: ") + ex.Message,
                Advice = I18n.S("以管理员运行可检查", "Run as administrator to check")
            };
        }
    }

    private static CheckItem CheckProxy()
    {
        try
        {
            var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings");
            var enable = key?.GetValue("ProxyEnable");
            var server = key?.GetValue("ProxyServer") as string;
            var auto = key?.GetValue("AutoConfigURL") as string;
            bool on = enable is int i && i == 1;
            if (on || !string.IsNullOrWhiteSpace(auto))
            {
                var detail = on
                    ? I18n.S($"系统代理已开启：{server ?? "(未填地址)"}", $"System proxy is on: {server ?? "(no address)"}")
                    : I18n.S($"自动配置脚本（PAC）：{auto}", $"PAC script: {auto}");
                return new CheckItem
                {
                    Name = I18n.S("系统代理", "System proxy"), Level = Level.Warn, Detail = detail,
                    Advice = I18n.S("若代理/VPN 出口 IP 被 Cloudflare 风控，验证会反复失败；可尝试关闭代理或更换节点", "If your proxy/VPN exit IP is flagged, verification keeps failing; turn it off or switch nodes")
                };
            }
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HTTP_PROXY")) ||
                !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HTTPS_PROXY")))
                return new CheckItem
                {
                    Name = I18n.S("系统代理", "System proxy"), Level = Level.Warn,
                    Detail = I18n.S("检测到 HTTP_PROXY/HTTPS_PROXY 环境变量", "HTTP_PROXY/HTTPS_PROXY env vars detected"),
                    Advice = I18n.S("代理可能导致验证失败", "Proxy may cause verification failures")
                };
            return new CheckItem { Name = I18n.S("系统代理", "System proxy"), Level = Level.Ok, Detail = I18n.S("未开启系统代理", "No system proxy"), Advice = I18n.S("正常", "OK") };
        }
        catch (Exception ex)
        {
            return new CheckItem { Name = I18n.S("系统代理", "System proxy"), Level = Level.Info, Detail = I18n.S("读取失败：", "Read failed: ") + ex.Message, Advice = I18n.S("无", "None") };
        }
    }

    private static async Task<List<BrowserInfo>> ProbeBrowsersAsync(IProgress<string> progress, CancellationToken ct)
    {
        var list = new List<BrowserInfo>();
        var edgeBase = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Edge\User Data");
        var chromeBase = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\User Data");
        if (Directory.Exists(edgeBase)) list.Add(await ProbeBrowserAsync("Edge", "msedge", edgeBase, ct));
        if (Directory.Exists(chromeBase)) list.Add(await ProbeBrowserAsync("Chrome", "chrome", chromeBase, ct));
        return list;
    }

    private static async Task<BrowserInfo> ProbeBrowserAsync(string name, string procName, string baseDir, CancellationToken ct)
    {
        var b = new BrowserInfo { Name = name, UserData = baseDir };
        try
        {
            var lastVersion = Path.Combine(baseDir, "Last Version");
            b.Version = File.Exists(lastVersion) ? File.ReadAllText(lastVersion).Trim() : I18n.S("未知", "unknown");
            b.Running = Process.GetProcessesByName(procName).Length > 0;

            var profiles = new List<string>();
            var def = Path.Combine(baseDir, "Default");
            if (Directory.Exists(def)) profiles.Add(def);
            foreach (var dir in Directory.GetDirectories(baseDir, "Profile *"))
                if (Directory.Exists(dir)) profiles.Add(dir);

            var seenExt = new HashSet<string>();
            foreach (var pdir in profiles)
            {
                var p = new ProfileInfo { Dir = pdir, Name = Path.GetFileName(pdir) };
                var prefs = new PreferenceData();
                foreach (var prefFile in new[] { "Preferences", "Secure Preferences" })
                {
                    var prefPath = Path.Combine(pdir, prefFile);
                    if (!File.Exists(prefPath)) continue;
                    try
                    {
                        var parsed = ParsePreferences(await File.ReadAllTextAsync(prefPath, ct));
                        if (string.IsNullOrWhiteSpace(prefs.ProfileName)) prefs.ProfileName = parsed.ProfileName;
                        if (prefs.BlockThirdPartyCookie == null) prefs.BlockThirdPartyCookie = parsed.BlockThirdPartyCookie;
                        if (string.IsNullOrWhiteSpace(prefs.EnhancedSecurity)) prefs.EnhancedSecurity = parsed.EnhancedSecurity;
                        prefs.Extensions.AddRange(parsed.Extensions);
                    }
                    catch { }
                }
                if (!string.IsNullOrWhiteSpace(prefs.ProfileName)) p.Name = prefs.ProfileName;

                var cPath = Path.Combine(pdir, "Network", "Cookies");
                if (!File.Exists(cPath)) cPath = Path.Combine(pdir, "Cookies");
                p.CookiesPath = cPath;
                if (File.Exists(cPath))
                {
                    try
                    {
                        using var conn = new SqliteConnection($"Data Source={cPath};Mode=ReadOnly;Cache=Shared;Default Timeout=3");
                        conn.Open();
                        using var c1 = conn.CreateCommand();
                        c1.CommandText = "select count(*) from cookies";
                        p.CookieCount = Convert.ToInt32(c1.ExecuteScalar() ?? 0);
                        using var c2 = conn.CreateCommand();
                        c2.CommandText = "select count(*) from cookies where lower(host_key) like '%cloudflare%' or lower(name) like 'cf_%' or lower(name) in ('cf_clearance','__cf_bm','cf_chl_opt','cf_chl_2','cf_chl_prog')";
                        p.CfCookieCount = Convert.ToInt32(c2.ExecuteScalar() ?? 0);
                    }
                    catch { }
                }
                b.Profiles.Add(p);

                foreach (var ext in prefs.Extensions)
                {
                    if (seenExt.Add(ext.Id))
                    {
                        b.Extensions.Add(ext);
                    }
                    else
                    {
                        var exist = b.Extensions.First(e => e.Id == ext.Id);
                        exist.Enabled |= ext.Enabled;
                    }
                }
            }

            foreach (var ext in b.Extensions)
            {
                if (string.IsNullOrWhiteSpace(ext.Name) || ext.Name.StartsWith("__MSG_"))
                {
                    var resolved = ResolveExtensionName(baseDir, ext.Id);
                    if (!string.IsNullOrWhiteSpace(resolved)) ext.Name = resolved;
                }
                if (string.IsNullOrWhiteSpace(ext.Name)) ext.Name = ext.Id[..Math.Min(8, ext.Id.Length)];
                ext.Blocker = IsBlocker(ext.Name);
                ext.Proxy = IsProxy(ext.Name);
            }
        }
        catch (Exception ex)
        {
            b.Note = I18n.S("探测失败：", "Probe failed: ") + ex.Message;
        }
        return b;
    }

    private sealed class PreferenceData
    {
        public string ProfileName = "";
        public List<ExtensionInfo> Extensions = new();
        public bool? BlockThirdPartyCookie;
        public string Tracking = "";
        public string EnhancedSecurity = "";
    }

    private static PreferenceData ParsePreferences(string json)
    {
        var data = new PreferenceData();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("profile", out var prof))
            {
                if (prof.TryGetProperty("name", out var pn) && pn.ValueKind == JsonValueKind.String)
                    data.ProfileName = pn.GetString() ?? "";
                if (prof.TryGetProperty("default_content_setting_values", out var dcs) &&
                    dcs.TryGetProperty("cookies", out var ck) && ck.ValueKind == JsonValueKind.Number)
                    data.BlockThirdPartyCookie = ck.GetInt32() == 2;
            }
            if (root.TryGetProperty("extensions", out var exts) && exts.TryGetProperty("settings", out var settings))
            {
                foreach (var ext in settings.EnumerateObject())
                {
                    var id = ext.Name;
                    int? state = null;
                    bool disabled = false;
                    string path = ""; string name = ""; string version = "";
                    if (ext.Value.TryGetProperty("state", out var st) && st.ValueKind == JsonValueKind.Number) state = st.GetInt32();
                    if (ext.Value.TryGetProperty("disable_reasons", out var dr) && dr.ValueKind == JsonValueKind.Array) disabled = dr.GetArrayLength() > 0;
                    if (ext.Value.TryGetProperty("path", out var pt) && pt.ValueKind == JsonValueKind.String) path = pt.GetString() ?? "";
                    if (ext.Value.TryGetProperty("manifest", out var mf))
                    {
                        if (mf.TryGetProperty("name", out var nm) && nm.ValueKind == JsonValueKind.String) name = nm.GetString() ?? "";
                        if (mf.TryGetProperty("version", out var ver) && ver.ValueKind == JsonValueKind.String) version = ver.GetString() ?? "";
                    }
                    if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path)) continue;
                    bool enabled = state == 1 || (state == null && !disabled);
                    data.Extensions.Add(new ExtensionInfo { Id = id, Name = name, Version = version, Enabled = enabled });
                }
            }
            if (root.TryGetProperty("edge", out var edge))
            {
                if (edge.TryGetProperty("tracking_prevention", out var tp))
                    data.Tracking = tp.ValueKind == JsonValueKind.String ? tp.GetString() ?? "" : tp.ToString();
                if (edge.TryGetProperty("enhanced_security_mode", out var es))
                    data.EnhancedSecurity = es.ToString();
            }
        }
        catch { }
        return data;
    }

    private static string ResolveExtensionName(string baseDir, string extId)
    {
        try
        {
            var dir = Path.Combine(baseDir, "Default", "Extensions", extId);
            if (!Directory.Exists(dir)) return "";
            var verDir = Directory.GetDirectories(dir).OrderByDescending(x => x).FirstOrDefault();
            if (verDir == null) return "";
            var mf = Path.Combine(verDir, "manifest.json");
            if (!File.Exists(mf)) return "";
            using var doc = JsonDocument.Parse(File.ReadAllText(mf));
            var name = doc.RootElement.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            if (name.StartsWith("__MSG_") && name.EndsWith("__"))
            {
                var key = name[6..^2];
                foreach (var loc in new[] { "zh_CN", "en", "en_US" })
                {
                    var locFile = Path.Combine(verDir, "_locales", loc, "messages.json");
                    if (!File.Exists(locFile)) continue;
                    using var ldoc = JsonDocument.Parse(File.ReadAllText(locFile));
                    if (ldoc.RootElement.TryGetProperty(key, out var msg) && msg.TryGetProperty("message", out var m))
                        return m.GetString() ?? "";
                }
            }
            return name;
        }
        catch { return ""; }
    }

    private static bool IsBlocker(string name)
    {
        var n = name.ToLowerInvariant();
        return BlockerKeywords.Any(k => n.Contains(k));
    }

    private static readonly string[] ProxyKeywords =
    {
        "vpn", "proxy", "代理", "加速", "翻墙", "科学上网", "shadowsocks", "v2ray", "clash"
    };

    private static bool IsProxy(string name)
    {
        var n = name.ToLowerInvariant();
        return ProxyKeywords.Any(k => n.Contains(k));
    }

    private static List<CheckItem> ProbeBrowserSettings(List<BrowserInfo> browsers)
    {
        var items = new List<CheckItem>();
        foreach (var b in browsers)
        {
            foreach (var p in b.Profiles)
            {
                var prefPath = Path.Combine(p.Dir, "Preferences");
                if (!File.Exists(prefPath)) continue;
                try
                {
                    var data = ParsePreferences(File.ReadAllText(prefPath));
                    if (data.BlockThirdPartyCookie == true)
                        items.Add(new CheckItem
                        {
                            Name = $"{b.Name} / {p.Name} " + I18n.S("第三方 Cookie", "third-party cookies"),
                            Level = Level.Warn,
                            Detail = I18n.S("已设置为阻止第三方 Cookie", "Third-party cookies are blocked"),
                            Advice = I18n.S("Turnstile 依赖第三方 Cookie，建议改为允许", "Turnstile needs third-party cookies; allow them")
                        });
                    if (data.EnhancedSecurity is "1" or "2")
                        items.Add(new CheckItem
                        {
                            Name = $"{b.Name} / {p.Name} " + I18n.S("增强安全模式", "enhanced security"),
                            Level = Level.Warn,
                            Detail = I18n.S("增强安全模式已开启（", "Enhanced security is on (") + data.EnhancedSecurity + "）",
                            Advice = I18n.S("可能阻止验证脚本，建议临时关闭", "May block verification scripts; turn it off temporarily")
                        });
                }
                catch { }
            }
        }
        return items;
    }

    private static async Task CheckLatencyAsync(Diagnosis d, HttpClient http, CancellationToken ct)
    {
        await TryTcpAsync("challenges.cloudflare.com", 443, d, ct);
        var times = new List<double>();
        for (int i = 0; i < 3; i++)
        {
            try
            {
                var s = Stopwatch.StartNew();
                using var resp = await http.GetAsync(ProbeUrl, HttpCompletionOption.ResponseHeadersRead, ct);
                s.Stop();
                if ((int)resp.StatusCode < 500) times.Add(s.Elapsed.TotalMilliseconds);
                else d.ProbeFailures++;
            }
            catch { d.ProbeFailures++; }
            if (i < 2) await Task.Delay(400, ct);
        }
        d.LatencyAvgMs = times.Count > 0 ? times.Average() : 0;
        d.LatencyMaxMs = times.Count > 0 ? times.Max() : 0;

        bool bad = d.ProbeFailures > 0 || d.LatencyAvgMs > 2500;
        bool warn = !bad && d.LatencyAvgMs > 1200;
        d.Items.Add(new CheckItem
        {
            Name = I18n.S("网络延迟（到 Cloudflare 边缘）", "Latency to Cloudflare edge"),
            Level = bad ? Level.Bad : warn ? Level.Warn : Level.Ok,
            Detail = times.Count == 0
                ? I18n.S($"3 次探测全部失败（{d.ProbeFailures} 次超时/失败）", $"All 3 probes failed ({d.ProbeFailures} timeouts)")
                : I18n.S($"3 次探测：平均 {d.LatencyAvgMs:F0} ms，最大 {d.LatencyMaxMs:F0} ms，失败 {d.ProbeFailures} 次", $"3 probes: avg {d.LatencyAvgMs:F0} ms, max {d.LatencyMaxMs:F0} ms, {d.ProbeFailures} failed"),
            Advice = bad
                ? I18n.S("延迟过高或丢包，验证脚本加载会一直转圈", "High latency/packet loss: the widget keeps spinning")
                : warn
                    ? I18n.S("网络偏慢，可能拖长验证时间", "Network is slow; verification takes longer")
                    : I18n.S("正常", "OK")
        });
        if (d.TcpMs > 0)
            d.Items.Add(new CheckItem
            {
                Name = I18n.S("TCP 连接耗时", "TCP connect time"),
                Level = d.TcpMs > 1200 ? Level.Warn : Level.Ok,
                Detail = I18n.S($"到 challenges.cloudflare.com:443 耗时 {d.TcpMs:F0} ms", $"challenges.cloudflare.com:443 took {d.TcpMs:F0} ms"),
                Advice = d.TcpMs > 1200 ? I18n.S("连接建立慢，检查网络/代理/MTU", "Slow connect: check network/proxy/MTU") : I18n.S("正常", "OK")
            });
        if (d.DnsMs > 0)
            d.Items.Add(new CheckItem
            {
                Name = I18n.S("DNS 解析耗时", "DNS lookup time"),
                Level = d.DnsMs > 1000 ? Level.Warn : Level.Ok,
                Detail = I18n.S($"challenges.cloudflare.com 解析耗时 {d.DnsMs:F0} ms", $"Resolving challenges.cloudflare.com took {d.DnsMs:F0} ms"),
                Advice = d.DnsMs > 1000 ? I18n.S("DNS 响应慢，建议换公共 DNS", "Slow DNS; use a public resolver") : I18n.S("正常", "OK")
            });
    }

    private static async Task TryTcpAsync(string host, int port, Diagnosis d, CancellationToken ct)
    {
        try
        {
            using var client = new TcpClient();
            var sw = Stopwatch.StartNew();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(5000);
            await client.ConnectAsync(host, port, cts.Token);
            sw.Stop();
            d.TcpMs = sw.Elapsed.TotalMilliseconds;
        }
        catch { d.TcpMs = 0; }
    }

    private static async Task CheckCloudflareStatusAsync(Diagnosis d, HttpClient http, CancellationToken ct)
    {
        try
        {
            using var resp = await http.GetAsync("https://www.cloudflarestatus.com/api/v2/status.json", HttpCompletionOption.ResponseHeadersRead, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            string indicator = "unknown", desc = "";
            if (doc.RootElement.TryGetProperty("status", out var st))
            {
                if (st.TryGetProperty("indicator", out var ind) && ind.ValueKind == JsonValueKind.String) indicator = ind.GetString() ?? "unknown";
                if (st.TryGetProperty("description", out var dd) && dd.ValueKind == JsonValueKind.String) desc = dd.GetString() ?? "";
            }
            d.CfServiceDown = indicator != "none";
            d.CfStatusText = I18n.S($"indicator={indicator}，{desc}", $"indicator={indicator}, {desc}");
            d.Items.Add(new CheckItem
            {
                Name = I18n.S("Cloudflare 服务状态", "Cloudflare status"),
                Level = d.CfServiceDown ? Level.Bad : Level.Ok,
                Detail = d.CfStatusText,
                Advice = d.CfServiceDown
                    ? I18n.S("Cloudflare 侧有故障，本机修不了，只能等恢复或换站点", "Cloudflare-side outage: nothing local to fix; wait or use another site")
                    : I18n.S("正常", "OK")
            });
        }
        catch (Exception ex)
        {
            d.Items.Add(new CheckItem
            {
                Name = I18n.S("Cloudflare 服务状态", "Cloudflare status"), Level = Level.Info,
                Detail = I18n.S("状态接口访问失败：", "Status API failed: ") + ex.Message,
                Advice = I18n.S("可手动打开 cloudflarestatus.com 查看", "Open cloudflarestatus.com manually")
            });
        }
    }

    private static void CheckBrowserVersions(Diagnosis d)
    {
        var parts = d.Browsers.Select(b => $"{b.Name} {b.Version}").ToList();
        d.BrowserVersionText = parts.Count > 0 ? string.Join(", ", parts) : I18n.S("未检测到浏览器", "No browser found");
        bool tooOld = false;
        foreach (var b in d.Browsers)
        {
            var first = b.Version.Split('.')[0];
            if (int.TryParse(first, out var major) && major > 0 && major < 130) tooOld = true;
        }
        d.Items.Add(new CheckItem
        {
            Name = I18n.S("浏览器版本", "Browser version"),
            Level = tooOld ? Level.Warn : Level.Ok,
            Detail = d.BrowserVersionText,
            Advice = tooOld
                ? I18n.S("版本偏旧，验证页可能误判，建议更新", "Outdated version may be misjudged; update it")
                : I18n.S("正常", "OK")
        });
    }

    private static void BuildFindings(Diagnosis d)
    {
        if (d.Mode == ProblemMode.SlowLoading)
        {
            BuildSlowFindings(d);
            return;
        }

        var findings = new List<Finding>();

        if (d.EnabledBlockers > 0)
        {
            var names = d.Browsers.SelectMany(b => b.Extensions).Where(e => e.Enabled && e.Blocker)
                .Select(e => e.Name).Distinct().Take(5).ToList();
            findings.Add(new Finding
            {
                Rank = 100,
                Title = I18n.S("浏览器广告拦截 / 隐私扩展拦截了 Cloudflare 验证脚本", "A browser ad-blocker/privacy extension is blocking Cloudflare's script"),
                Reason = I18n.S(
                    $"检测到 {d.EnabledBlockers} 个已启用的拦截类扩展：{string.Join("、", names)}。这类扩展会阻止 challenges.cloudflare.com 的脚本或注入 CSS，导致验证框转圈、空白或同时出现“成功/失败”两个状态。",
                    $"{d.EnabledBlockers} enabled blocker(s): {string.Join(", ", names)}. They block challenges.cloudflare.com scripts or inject CSS, causing spinners, blank widgets, or both success/error states."),
                Fix = I18n.S("在扩展中把卡验证的站点加入白名单（如 AdGuard 点“在此网站上暂停过滤”），或临时禁用拦截类扩展后刷新页面。",
                    "Allow-list the site in the extension (e.g. AdGuard: pause filtering on this site), or disable blockers temporarily and reload."),
                WeightText = I18n.S("最高", "top")
            });
        }

        if (Math.Abs(d.TimeOffsetSec) > 300)
            findings.Add(new Finding
            {
                Rank = 90,
                Title = I18n.S("系统时间与网络时间偏差过大", "System clock is far off"),
                Reason = I18n.S($"当前偏差 {Math.Abs(d.TimeOffsetSec):F0} 秒。Cloudflare 会用时间戳校验挑战，偏差超过 5 分钟会直接判定失败（错误码 200100）。",
                    $"Offset is {Math.Abs(d.TimeOffsetSec):F0}s. Cloudflare validates timestamps; offsets over 5 minutes fail (code 200100)."),
                Fix = I18n.S("开启“自动设置时间”，或一键修复尝试同步系统时间；必要时以管理员运行。",
                    "Turn on automatic time, or let Fix All sync it; run as administrator if needed."),
                WeightText = I18n.S("高", "high")
            });

        if (d.HostsHit)
            findings.Add(new Finding
            {
                Rank = 85,
                Title = I18n.S("hosts 文件屏蔽了 Cloudflare 验证域名", "hosts file blocks Cloudflare domains"),
                Reason = d.HostsDetail,
                Fix = I18n.S("删除/注释 hosts 中 challenges.cloudflare.com 等条目，执行 ipconfig /flushdns 后重试。",
                    "Remove/comment the entries in hosts, run ipconfig /flushdns, then retry."),
                WeightText = I18n.S("高", "high")
            });

        if (!d.NetworkOk)
            findings.Add(new Finding
            {
                Rank = 80,
                Title = I18n.S("网络无法正常访问 challenges.cloudflare.com", "Cannot reach challenges.cloudflare.com"),
                Reason = I18n.S("DNS 或 HTTPS 探测失败：", "DNS/HTTPS probe failed: ") + d.DnsDetail + " / " + d.NetworkDetail,
                Fix = I18n.S("检查网络、更换 DNS、关闭不稳定代理；可尝试刷新 DNS 缓存。",
                    "Check network, change DNS, drop unstable proxies; try flushing DNS."),
                WeightText = I18n.S("高", "high")
            });

        if (d.ProxyOn)
            findings.Add(new Finding
            {
                Rank = 60,
                Title = I18n.S("代理 / VPN 出口 IP 可能被 Cloudflare 风控", "Proxy/VPN exit IP may be flagged"),
                Reason = d.ProxyDetail + I18n.S("。共享出口 IP 常被 Cloudflare 标记为高风险，表现为验证反复失败或一直转圈。",
                    ". Shared exit IPs are often flagged, causing repeated failures or endless spinning."),
                Fix = I18n.S("切换代理节点、关闭 VPN 后直连，或用手机热点对比测试。",
                    "Switch nodes, disable VPN and connect directly, or compare with a phone hotspot."),
                WeightText = I18n.S("中", "medium")
            });

        if (d.CfCookies > 0)
            findings.Add(new Finding
            {
                Rank = 50,
                Title = I18n.S("浏览器里残留了失效的 Cloudflare 验证数据", "Stale Cloudflare verification data in the browser"),
                Reason = I18n.S($"共发现 {d.CfCookies} 条 cf_clearance / __cf_bm / Cloudflare 相关 Cookie。过期的 cf_clearance 会让验证循环失败。",
                    $"{d.CfCookies} cf_clearance / __cf_bm / Cloudflare cookies found. Expired cf_clearance makes verification loop."),
                Fix = I18n.S("一键修复会清理这些 Cookie 与站点数据；也可手动清除该站点的 Cookie。",
                    "Fix All removes these cookies; you can also clear them manually."),
                WeightText = I18n.S("中", "medium")
            });

        if (findings.Count == 0)
            findings.Add(new Finding
            {
                Rank = 10,
                Title = I18n.S("未发现本机明显异常，可能是网站侧或 IP 段风控", "Nothing obviously wrong locally: possibly site-side or IP reputation"),
                Reason = I18n.S("网络、时间、hosts、代理和浏览器设置均正常。Cloudflare 也可能因为 IP 段被误判或站点侧配置问题而拒绝验证。",
                    "Network, clock, hosts, proxy and browser settings look fine. Cloudflare may still reject due to IP reputation or site config."),
                Fix = I18n.S("换网络（手机热点）、换浏览器无痕模式、稍后重试；若所有设备都失败，联系网站客服。",
                    "Try another network (hotspot), incognito mode, retry later; if all devices fail, contact the site."),
                WeightText = I18n.S("低", "low")
            });

        d.Findings = findings.OrderByDescending(f => f.Rank).ToList();
        d.Summary = I18n.S(
            $"检测完成：共 {d.Items.Count} 项，问题 {d.Items.Count(i => i.Level is Level.Bad or Level.Warn)} 项，疑似原因 {d.Findings.Count} 条。",
            $"Done: {d.Items.Count} checks, {d.Items.Count(i => i.Level is Level.Bad or Level.Warn)} issues, {d.Findings.Count} suspected causes.");
    }

    private static void BuildSlowFindings(Diagnosis d)
    {
        var findings = new List<Finding>();

        if (d.CfServiceDown)
            findings.Add(new Finding
            {
                Rank = 95,
                Title = I18n.S("Cloudflare 服务状态异常（站点侧故障）", "Cloudflare outage (site-side)"),
                Reason = d.CfStatusText + I18n.S("。这时所有用户都会转圈或报错，本机怎么修都没用。", ". Everyone is affected; local fixes cannot help."),
                Fix = I18n.S("本机无法修复：等待 Cloudflare 恢复，或打开 cloudflarestatus.com 确认。", "Nothing local to fix: wait for recovery or check cloudflarestatus.com."),
                WeightText = I18n.S("最高", "top")
            });

        if (d.ProbeFailures > 0 || d.LatencyAvgMs > 2500)
            findings.Add(new Finding
            {
                Rank = 92,
                Title = I18n.S("网络到 Cloudflare 边缘延迟过高 / 丢包", "High latency / packet loss to Cloudflare edge"),
                Reason = I18n.S(
                    $"3 次探测失败 {d.ProbeFailures} 次，平均 {d.LatencyAvgMs:F0} ms，最大 {d.LatencyMaxMs:F0} ms，TCP 连接 {d.TcpMs:F0} ms。验证 iframe 的脚本要等很久才能拉下来，所以页面一直转圈。",
                    $"3 probes, {d.ProbeFailures} failed, avg {d.LatencyAvgMs:F0} ms, max {d.LatencyMaxMs:F0} ms, TCP {d.TcpMs:F0} ms. The widget script takes too long to load, so the page keeps spinning."),
                Fix = I18n.S("换网络（手机热点对比）、刷新 DNS、关闭不稳定代理/换节点；网络恢复后再试。",
                    "Try another network (hotspot), flush DNS, disable unstable proxies or switch nodes."),
                WeightText = I18n.S("最高", "top")
            });
        else if (d.LatencyAvgMs > 1200)
            findings.Add(new Finding
            {
                Rank = 70,
                Title = I18n.S("网络偏慢，可能拉长验证时间", "Slow network may prolong verification"),
                Reason = I18n.S($"平均 {d.LatencyAvgMs:F0} ms，最大 {d.LatencyMaxMs:F0} ms。", $"avg {d.LatencyAvgMs:F0} ms, max {d.LatencyMaxMs:F0} ms."),
                Fix = I18n.S("换网络或换节点；若只是偶发，可等页面自行完成。", "Switch network or node; if occasional, just wait."),
                WeightText = I18n.S("中", "medium")
            });

        if (d.EnabledBlockers > 0)
        {
            var names = d.Browsers.SelectMany(b => b.Extensions).Where(e => e.Enabled && e.Blocker)
                .Select(e => e.Name).Distinct().Take(5).ToList();
            findings.Add(new Finding
            {
                Rank = 80,
                Title = I18n.S("浏览器扩展拦截/拖慢验证脚本", "Extensions block or slow the verification script"),
                Reason = I18n.S($"检测到 {d.EnabledBlockers} 个已启用的拦截类扩展：{string.Join("、", names)}。扩展拦截后脚本会一直重试，表现为长时间转圈。",
                    $"{d.EnabledBlockers} enabled blocker(s): {string.Join(", ", names)}. The script retries endlessly, causing long spinning."),
                Fix = I18n.S("在扩展里对该站点暂停过滤（如 AdGuard），或临时禁用扩展后刷新。",
                    "Pause filtering for this site in the extension (e.g. AdGuard) or disable it temporarily."),
                WeightText = I18n.S("高", "high")
            });
        }

        if (d.ProxyOn)
            findings.Add(new Finding
            {
                Rank = 75,
                Title = I18n.S("代理 / VPN 出口 IP 信誉差，挑战被拖长", "Proxy/VPN exit IP reputation is poor"),
                Reason = d.ProxyDetail + I18n.S("。共享出口 IP 常被 Cloudflare 判低分，挑战会故意延长或反复转圈。",
                    ". Shared exit IPs score low, so challenges are stretched or loop."),
                Fix = I18n.S("换节点或关闭代理直连，用手机热点对比测试。", "Switch nodes or disable proxy; compare with a hotspot."),
                WeightText = I18n.S("中", "medium")
            });

        if (d.CfCookies > 0)
            findings.Add(new Finding
            {
                Rank = 60,
                Title = I18n.S("浏览器残留失效的 Cloudflare 验证数据", "Stale Cloudflare verification data"),
                Reason = I18n.S($"发现 {d.CfCookies} 条 cf_clearance / __cf_bm 等 Cookie，旧数据会让验证循环加载。",
                    $"{d.CfCookies} cf_clearance / __cf_bm cookies; stale data makes verification loop."),
                Fix = I18n.S("一键修复会清理这些 Cookie。", "Fix All removes these cookies."),
                WeightText = I18n.S("中", "medium")
            });

        if (Math.Abs(d.TimeOffsetSec) > 300)
            findings.Add(new Finding
            {
                Rank = 55,
                Title = I18n.S("系统时间偏差过大", "System clock is far off"),
                Reason = I18n.S($"与网络时间相差 {Math.Abs(d.TimeOffsetSec):F0} 秒，可能导致验证超时。", $"Off by {Math.Abs(d.TimeOffsetSec):F0}s; may cause timeouts."),
                Fix = I18n.S("开启自动时间同步，或一键修复尝试校准。", "Enable automatic time or let Fix All sync it."),
                WeightText = I18n.S("中", "medium")
            });

        if (findings.Count == 0)
            findings.Add(new Finding
            {
                Rank = 10,
                Title = I18n.S("本机链路正常，可能是站点侧或 Cloudflare 风控", "Local network looks fine: possibly site-side or Cloudflare risk control"),
                Reason = I18n.S("延迟、DNS、TCP、时间、扩展、代理均未发现异常。转圈可能由站点配置、Cloudflare 侧波动或 IP 段风控引起。",
                    "Latency, DNS, TCP, clock, extensions and proxy all look fine. Spinning may come from site config, Cloudflare fluctuation or IP reputation."),
                Fix = I18n.S("稍后重试，换网络/无痕窗口对比；持续失败联系网站客服。", "Retry later, compare with another network/incognito; contact the site if it persists."),
                WeightText = I18n.S("低", "low")
            });

        d.Findings = findings.OrderByDescending(f => f.Rank).ToList();
        d.Summary = I18n.S(
            $"诊断完成：共 {d.Items.Count} 项，异常 {d.Items.Count(i => i.Level is Level.Bad or Level.Warn)} 项，疑似原因 {d.Findings.Count} 条。",
            $"Done: {d.Items.Count} checks, {d.Items.Count(i => i.Level is Level.Bad or Level.Warn)} issues, {d.Findings.Count} suspected causes.");
    }
}
