using CfValidateFixer;

namespace UnitTests;

public class AppInfoTests
{
    [Fact]
    public void Version_Is_Not_Empty()
    {
        Assert.False(string.IsNullOrWhiteSpace(AppInfo.Version));
    }

    [Fact]
    public void Title_Contains_Version()
    {
        Assert.Contains(AppInfo.Version, AppInfo.Title);
        Assert.Contains(AppInfo.Version, AppInfo.TitleEn);
    }

    [Fact]
    public void Author_Is_Set()
    {
        Assert.False(string.IsNullOrWhiteSpace(AppInfo.Author));
    }
}

public class I18nTests
{
    [Fact]
    public void Chinese_Returns_Chinese()
    {
        I18n.Set(Lang.Zh);
        Assert.Equal("中文", I18n.S("中文", "English"));
    }

    [Fact]
    public void English_Returns_English()
    {
        I18n.Set(Lang.En);
        Assert.Equal("English", I18n.S("中文", "English"));
        I18n.Set(Lang.Zh);
    }
}

public class UserSettingsTests
{
    [Fact]
    public void Defaults_Are_Sane()
    {
        UserSettings.Load();
        Assert.Contains(UserSettings.Language, new[] { "zh", "en" });
    }
}
