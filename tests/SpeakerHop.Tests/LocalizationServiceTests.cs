using System.Globalization;
using System.Reflection;
using SpeakerHop.Services;

namespace SpeakerHop.Tests;

public sealed class LocalizationServiceTests
{
    [Fact]
    public void SupportedLanguages_ExposeSystemLanguageFirst()
    {
        var first = Assert.IsType<LanguageOption>(LocalizationService.SupportedLanguages[0]);

        Assert.Equal(LocalizationService.SystemLanguage, first.Code);
        Assert.Equal("language.system", first.ResourceKey);
    }

    [Fact]
    public void Text_ReturnsConfiguredLanguageResourceCaseInsensitively()
    {
        var text = LocalizationService.Text("NAV.DEVICES", "EN-us");

        Assert.Equal("Devices", text);
    }

    [Fact]
    public void Text_WhenSelectedResourceMissesKey_FallsBackToEnglish()
    {
        var resources = ResourceDictionaries();
        var german = resources[LocalizationService.German];
        Assert.True(german.Remove("nav.devices", out var original));

        try
        {
            Assert.Equal("Devices", LocalizationService.Text("nav.devices", LocalizationService.German));
        }
        finally
        {
            german["nav.devices"] = original;
        }
    }

    [Fact]
    public void Text_WhenNoResourceContainsKey_ReturnsKey()
    {
        Assert.Equal("missing.key", LocalizationService.Text("missing.key", LocalizationService.English));
    }

    [Theory]
    [InlineData("EN-us", LocalizationService.English)]
    [InlineData("ja-jp", LocalizationService.Japanese)]
    public void ResolveLanguage_UsesConfiguredSupportedLanguageCaseInsensitively(
        string configuredLanguage,
        string expected)
    {
        Assert.Equal(expected, LocalizationService.ResolveLanguage(configuredLanguage));
    }

    [Theory]
    [InlineData("zh-Hant-HK", LocalizationService.ChineseTraditional)]
    [InlineData("zh-TW", LocalizationService.ChineseTraditional)]
    [InlineData("zh-HK", LocalizationService.ChineseTraditional)]
    [InlineData("zh-MO", LocalizationService.ChineseTraditional)]
    [InlineData("zh-CN", LocalizationService.ChineseSimplified)]
    public void ResolveLanguage_MapsChineseCultures(string cultureName, string expected)
    {
        using var _ = UseCurrentUiCulture(cultureName);

        Assert.Equal(expected, LocalizationService.ResolveLanguage(LocalizationService.SystemLanguage));
    }

    [Fact]
    public void ResolveLanguage_UsesExactSupportedCulture()
    {
        using var _ = UseCurrentUiCulture("fr-FR");

        Assert.Equal(LocalizationService.French, LocalizationService.ResolveLanguage(null));
    }

    [Fact]
    public void ResolveLanguage_UsesSupportedLanguagePrefix()
    {
        using var _ = UseCurrentUiCulture("de-AT");

        Assert.Equal(LocalizationService.German, LocalizationService.ResolveLanguage(null));
    }

    [Fact]
    public void ResolveLanguage_WhenCultureIsUnsupported_FallsBackToEnglish()
    {
        using var _ = UseCurrentUiCulture("it-IT");

        Assert.Equal(LocalizationService.English, LocalizationService.ResolveLanguage(null));
    }

    private static IDisposable UseCurrentUiCulture(string cultureName)
    {
        return new CultureScope(new CultureInfo(cultureName));
    }

    private static Dictionary<string, Dictionary<string, string>> ResourceDictionaries()
    {
        var field = typeof(LocalizationService).GetField(
            "Resources",
            BindingFlags.NonPublic | BindingFlags.Static);
        return Assert.IsType<Dictionary<string, Dictionary<string, string>>>(field?.GetValue(null));
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _previousCulture;
        private readonly CultureInfo _previousUiCulture;

        public CultureScope(CultureInfo culture)
        {
            _previousCulture = CultureInfo.CurrentCulture;
            _previousUiCulture = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _previousCulture;
            CultureInfo.CurrentUICulture = _previousUiCulture;
        }
    }
}
