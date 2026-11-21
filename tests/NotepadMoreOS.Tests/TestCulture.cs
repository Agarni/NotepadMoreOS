using System.Runtime.CompilerServices;
using NotepadMoreOS.Services;

namespace NotepadMoreOS.Tests;

/// <summary>
/// Pins the UI culture to English so assertions that check user-facing text are
/// stable regardless of the machine locale. Localization-specific tests read the
/// resources with an explicit culture instead of changing the global one.
/// </summary>
internal static class TestCulture
{
    [ModuleInitializer]
    internal static void Initialize() => LocalizationService.Apply(LocalizationService.English);
}
