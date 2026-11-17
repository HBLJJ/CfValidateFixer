namespace CfValidateFixer;

public enum Lang { Zh, En }

/// <summary>极简双语支持：S(中文, English)。</summary>
public static class I18n
{
    public static Lang Current { get; private set; } = Lang.Zh;

    public static string S(string zh, string en) => Current == Lang.En ? en : zh;

    public static string F(string zh, string en, params object[] args) => string.Format(S(zh, en), args);

    public static string LangLabel => Current == Lang.En ? "中文" : "EN";

    public static string LangName => Current == Lang.En ? "English" : "中文";

    public static void Load()
    {
        UserSettings.Load();
        Current = UserSettings.Language.Equals("en", StringComparison.OrdinalIgnoreCase) ? Lang.En : Lang.Zh;
    }

    public static void Set(Lang lang)
    {
        Current = lang;
        UserSettings.Language = lang == Lang.En ? "en" : "zh";
        UserSettings.Save();
    }
}
