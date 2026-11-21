using System.Globalization;
using NotepadMoreOS.Resources;

namespace NotepadMoreOS.Services;

public sealed record LanguageOption(string Id, string DisplayName);

/// <summary>Maps the persisted language preference to the UI culture used by resources.</summary>
public static class LocalizationService
{
    public const string System = "system";
    public const string English = "en";
    public const string PortugueseBrazil = "pt-BR";

    public static IReadOnlyList<LanguageOption> GetLanguages() =>
    [
        new(System, Strings.Settings_AutoLanguage),
        new(English, "English"),
        new(PortugueseBrazil, "Português (Brasil)")
    ];

    public static string Normalize(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return System;
        }

        return language.Trim().ToLowerInvariant() switch
        {
            English => English,
            "pt-br" or "pt" => PortugueseBrazil,
            _ => System
        };
    }

    /// <summary>Applies the UI culture. Formatting culture is left untouched.</summary>
    public static void Apply(string? language)
    {
        var culture = Normalize(language) switch
        {
            PortugueseBrazil => CultureInfo.GetCultureInfo(PortugueseBrazil),
            English => CultureInfo.GetCultureInfo(English),
            _ => DetectSystemCulture()
        };

        if (culture is null)
        {
            return;
        }

        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    /// <summary>
    /// Linux packages run with invariant globalization (no ICU dependency), where
    /// CurrentUICulture is not populated from the environment, so LANG is read
    /// directly before falling back to the process culture.
    /// </summary>
    private static CultureInfo? DetectSystemCulture()
    {
        var name = CultureInfo.CurrentUICulture.Name;
        if (string.IsNullOrEmpty(name) || name == "iv" || name.StartsWith("iv-", StringComparison.Ordinal))
        {
            var locale = Environment.GetEnvironmentVariable("LC_ALL")
                         ?? Environment.GetEnvironmentVariable("LC_MESSAGES")
                         ?? Environment.GetEnvironmentVariable("LANG");

            if (!string.IsNullOrWhiteSpace(locale))
            {
                // "pt_BR.UTF-8" -> "pt-BR"
                var normalized = locale.Split('.')[0].Replace('_', '-');
                if (normalized.Length >= 2)
                {
                    try
                    {
                        name = CultureInfo.GetCultureInfo(normalized).Name;
                    }
                    catch (CultureNotFoundException)
                    {
                        return null;
                    }
                }
            }
        }

        return string.IsNullOrEmpty(name) || name == "iv" ? null : CultureInfo.GetCultureInfo(name);
    }
}
