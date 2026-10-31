namespace SpeakerHop.Services;

public static class LegalDocumentService
{
    public static string GetPath(string legalDirectory, string document, string? configuredLanguage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(legalDirectory);
        if (document is not ("Copyright.txt" or "Terms.txt" or "Privacy.txt" or "ThirdPartyNotices.txt"))
            throw new ArgumentException("Unknown legal document.", nameof(document));

        var language = LocalizationService.ResolveLanguage(configuredLanguage);
        var localized = Path.Combine(legalDirectory, language, document);
        if (File.Exists(localized)) return localized;
        var english = Path.Combine(legalDirectory, LocalizationService.English, document);
        return File.Exists(english) ? english : Path.Combine(legalDirectory, document);
    }
}
