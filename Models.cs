namespace CfValidateFixer;

public enum Level { Ok, Info, Warn, Bad }

public enum ProblemMode { VerifyFailed, SlowLoading }

public sealed class CheckItem
{
    public string Name = "";
    public Level Level = Level.Info;
    public string Detail = "";
    public string Advice = "";
}

public sealed class ExtensionInfo
{
    public string Id = "";
    public string Name = "";
    public string Version = "";
    public bool Enabled;
    public bool Blocker;
    public bool Proxy;
}

public sealed class BrowserInfo
{
    public string Name = "";
    public string Version = "";
    public string UserData = "";
    public bool Running;
    public List<ProfileInfo> Profiles = new();
    public List<ExtensionInfo> Extensions = new();
    public string Note = "";
}

public sealed class ProfileInfo
{
    public string Dir = "";
    public string Name = "";
    public string CookiesPath = "";
    public int CookieCount;
    public int CfCookieCount;
}

public sealed class Finding
{
    public int Rank;
    public string Title = "";
    public string Reason = "";
    public string Fix = "";
    public string WeightText = "";
}

public sealed class FixAction
{
    public string Title = "";
    public string Result = "";   // 已修复 / 已跳过 / 需手动
    public Level Level = Level.Ok;
    public string Detail = "";
}

public sealed class Diagnosis
{
    public ProblemMode Mode = ProblemMode.VerifyFailed;
    public List<CheckItem> Items = new();
    public List<BrowserInfo> Browsers = new();
    public List<Finding> Findings = new();
    public List<FixAction> FixPlan = new();
    public string Summary = "";
    public bool NetworkOk = true;
    public double TimeOffsetSec;
    public bool HostsHit;
    public bool ProxyOn;
    public int EnabledBlockers;
    public int CfCookies;
    public string TimeText = "";
    public string HostsDetail = "";
    public string ProxyDetail = "";
    public string NetworkDetail = "";
    public string DnsDetail = "";

    // 加载时间过长模式专用
    public double LatencyAvgMs;
    public double LatencyMaxMs;
    public int ProbeFailures;
    public double DnsMs;
    public double TcpMs;
    public bool CfServiceDown;
    public string CfStatusText = "";
    public string BrowserVersionText = "";
}
