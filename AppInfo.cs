namespace CfValidateFixer;

/// <summary>版本与产品信息的唯一来源。</summary>
public static class AppInfo
{
    public const string Version = "1.0.0";
    public const string ProductName = "Cloudflare 修复工坊";
    public const string ProductNameEn = "Cloudflare Fix Workshop";
    public const string Author = "火爆辣椒鸡";

    public static string Title => ProductName + " v" + Version;
    public static string TitleEn => ProductNameEn + " v" + Version;
}
