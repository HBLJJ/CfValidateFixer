namespace CfValidateFixer;

/// <summary>用户设置（settings.ini）：语言、是否显示启动简介。</summary>
public static class UserSettings
{
    public static string Language = "zh";
    public static bool ShowIntro = true;

    public static string FilePath => Path.Combine(AppContext.BaseDirectory, "settings.ini");

    public static void Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return;
            foreach (var raw in File.ReadAllLines(FilePath))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#')) continue;
                var idx = line.IndexOf('=');
                if (idx <= 0) continue;
                var key = line[..idx].Trim().ToLowerInvariant();
                var val = line[(idx + 1)..].Trim();
                switch (key)
                {
                    case "language":
                        Language = val.Equals("en", StringComparison.OrdinalIgnoreCase) ? "en" : "zh";
                        break;
                    case "intro":
                        ShowIntro = !val.Equals("off", StringComparison.OrdinalIgnoreCase);
                        break;
                }
            }
        }
        catch { }
    }

    public static void Save()
    {
        try
        {
            File.WriteAllText(FilePath,
                "language=" + Language + Environment.NewLine +
                "intro=" + (ShowIntro ? "on" : "off") + Environment.NewLine);
        }
        catch { }
    }
}
