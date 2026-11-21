using NotepadMoreOS.Models;
using TextMateSharp.Grammars;

namespace NotepadMoreOS.Services;

/// <summary>Maps files and extensions to TextMate grammars.</summary>
public sealed class LanguageService
{
    private readonly Dictionary<string, LanguageInfo> _byExtension = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<LanguageInfo> _languages;

    public LanguageService()
    {
        RegistryOptions = new RegistryOptions(ThemeName.DarkPlus);

        var languages = new List<LanguageInfo> { LanguageInfo.PlainText };

        foreach (var language in RegistryOptions.GetAvailableLanguages())
        {
            var displayName = language.Aliases?.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = language.Id;
            }

            var scope = RegistryOptions.GetScopeByLanguageId(language.Id);
            if (string.IsNullOrWhiteSpace(scope))
            {
                continue;
            }

            var info = new LanguageInfo(language.Id, displayName, scope);
            languages.Add(info);

            foreach (var extension in language.Extensions ?? [])
            {
                _byExtension.TryAdd(extension, info);
            }
        }

        _languages = languages
            .OrderBy(language => language.IsPlainText ? 0 : 1)
            .ThenBy(language => language.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public RegistryOptions RegistryOptions { get; }

    public IReadOnlyList<LanguageInfo> Languages => _languages;

    public LanguageInfo Detect(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return LanguageInfo.PlainText;
        }

        var extension = Path.GetExtension(path);
        if (string.IsNullOrEmpty(extension))
        {
            return LanguageInfo.PlainText;
        }

        return _byExtension.TryGetValue(extension, out var language) ? language : LanguageInfo.PlainText;
    }

    public LanguageInfo FindById(string? id) =>
        _languages.FirstOrDefault(language => string.Equals(language.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? LanguageInfo.PlainText;
}
