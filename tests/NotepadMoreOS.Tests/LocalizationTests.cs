using System.Globalization;
using System.Xml.Linq;
using NotepadMoreOS.Resources;
using NotepadMoreOS.Services;

namespace NotepadMoreOS.Tests;

public sealed class LocalizationTests
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en");
    private static readonly CultureInfo Portuguese = CultureInfo.GetCultureInfo("pt-BR");

    private static string? Get(string key, CultureInfo culture) =>
        new System.Resources.ResourceManager("NotepadMoreOS.Resources.Strings", typeof(Strings).Assembly)
            .GetString(key, culture);

    [Theory]
    [InlineData("Menu_Save", "Save", "Salvar")]
    [InlineData("Find_NoMatches", "No matches", "Nenhuma ocorrência")]
    [InlineData("Settings_Title", "Settings", "Configurações")]
    public void Resources_HaveEnglishAndPortugueseValues(string key, string english, string portuguese)
    {
        Assert.Equal(english, Get(key, English));
        Assert.Equal(portuguese, Get(key, Portuguese));
    }

    [Fact]
    public void BothLanguages_DefineTheSameKeys()
    {
        var english = ReadKeys("Strings.resx");
        var portuguese = ReadKeys("Strings.pt-BR.resx");

        Assert.NotEmpty(english);
        Assert.Equal(english.OrderBy(key => key), portuguese.OrderBy(key => key));
    }

    [Theory]
    [InlineData("pt-br", LocalizationService.PortugueseBrazil)]
    [InlineData("pt", LocalizationService.PortugueseBrazil)]
    [InlineData("en-US", LocalizationService.System)]
    [InlineData("en", LocalizationService.English)]
    [InlineData(null, LocalizationService.System)]
    public void Normalize_MapsLanguageIds(string? input, string expected)
    {
        Assert.Equal(expected, LocalizationService.Normalize(input));
    }

    private static HashSet<string> ReadKeys(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "src", "NotepadMoreOS", "Resources", fileName)))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        var path = Path.Combine(directory!.FullName, "src", "NotepadMoreOS", "Resources", fileName);
        return XDocument.Load(path)
            .Root!
            .Elements("data")
            .Select(element => element.Attribute("name")!.Value)
            .ToHashSet();
    }
}
