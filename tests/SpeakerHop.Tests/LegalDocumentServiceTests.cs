using SpeakerHop.Services;

namespace SpeakerHop.Tests;

public sealed class LegalDocumentServiceTests
{
    public static IEnumerable<object[]> Documents =>
        LocalizationService.SupportedLanguages.Skip(1).SelectMany(language =>
            new[] { "Copyright.txt", "Terms.txt", "Privacy.txt", "ThirdPartyNotices.txt" }
                .Select(document => new object[] { language.Code, document }));

    [Theory]
    [MemberData(nameof(Documents))]
    public void EverySupportedLanguage_HasItsOwnCompleteDocument(string language, string document)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Legal");
        var expected = Path.Combine(directory, language, document);
        Assert.True(File.Exists(expected), $"Missing translation: {language}/{document}");
        Assert.Equal(expected, LegalDocumentService.GetPath(directory, document, language));
        var text = File.ReadAllText(expected);
        Assert.True(text.Length > 250, $"Incomplete translation: {language}/{document}");
        if (document != "ThirdPartyNotices.txt") Assert.Contains("2026-11-01", text);
        Assert.Contains(document == "ThirdPartyNotices.txt" ? "NAudio" : "mmiyaji", text);
    }

    [Theory]
    [InlineData("system")]
    [InlineData(null)]
    public void SystemLanguage_UsesTheSameResolvedLanguageAsUi(string? language)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Legal");
        var resolved = LocalizationService.ResolveLanguage(language);
        Assert.Equal(Path.Combine(directory, resolved, "Terms.txt"),
            LegalDocumentService.GetPath(directory, "Terms.txt", language));
    }

    [Theory]
    [InlineData("zh-CN")]
    [InlineData("zh-TW")]
    [InlineData("ko-KR")]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    [InlineData("es-ES")]
    public void NewControlsAndLegalLabels_DoNotFallBackToEnglish(string language)
    {
        foreach (var key in new[] { "devices.switch", "shortcuts.unassigned", "shortcuts.chooseDevice",
            "shortcuts.clear", "about.terms", "about.privacyPolicy",
            "about.copyright", "about.copyrightNotice", "about.thirdParty", "about.thirdPartyOriginal" })
            Assert.NotEqual(LocalizationService.Text(key, "en-US"), LocalizationService.Text(key, language));
    }

    [Fact]
    public void TranslatedMessages_PreserveTheirFormattingArguments()
    {
        foreach (var language in LocalizationService.SupportedLanguages.Skip(1))
        foreach (var key in new[] { "settings.persistenceError", "shortcuts.addBindingFor",
            "shortcuts.registrationFailureDetails", "shortcuts.startupRegistrationFailed" })
        {
            var text = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                LocalizationService.Text(key, language.Code), "COUNT-MARKER", "DETAIL-MARKER");
            Assert.Contains("COUNT-MARKER", text);
            if (key == "shortcuts.registrationFailureDetails") Assert.Contains("DETAIL-MARKER", text);
        }
    }

    [Fact]
    public void MissingTranslation_FallsBackToEnglishThenOriginal()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(directory, "en-US"));
        try
        {
            var english = Path.Combine(directory, "en-US", "Terms.txt");
            File.WriteAllText(english, "English terms");
            Assert.Equal(english, LegalDocumentService.GetPath(directory, "Terms.txt", "ja-JP"));
            File.Delete(english);
            Assert.Equal(Path.Combine(directory, "Terms.txt"), LegalDocumentService.GetPath(directory, "Terms.txt", "ja-JP"));
            Assert.Throws<ArgumentException>(() => LegalDocumentService.GetPath(directory, "../Terms.txt", "en-US"));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
