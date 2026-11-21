using NotepadMoreOS.Models;
using NotepadMoreOS.Services;

namespace NotepadMoreOS.Tests;

public sealed class SettingsServiceTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsPreferences()
    {
        var directory = TestEnvironment.CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "settings.json");
            var service = new SettingsService(path);
            var settings = new AppSettings
            {
                Theme = "LightPlus",
                AccentColor = "#4C8DFF",
                TabStyle = TabStyles.Connected,
                Language = "pt-BR",
                FontFamily = "Menlo",
                FontSize = 16,
                WordWrap = true,
                TabSize = 2,
                RestoreSession = false,
                RecoveryEnabled = false
            };
            settings.RecentFiles.Add("/tmp/a.txt");
            settings.Session.Documents.Add(new SessionDocument { Path = "/tmp/a.txt", CaretOffset = 12 });
            settings.Session.ActiveIndex = 0;

            service.Save(settings);
            var loaded = service.Load();

            Assert.Equal("LightPlus", loaded.Theme);
            Assert.Equal("#4C8DFF", loaded.AccentColor);
            Assert.Equal(TabStyles.Connected, loaded.TabStyle);
            Assert.Equal("pt-BR", loaded.Language);
            Assert.Equal("Menlo", loaded.FontFamily);
            Assert.Equal(16, loaded.FontSize);
            Assert.True(loaded.WordWrap);
            Assert.Equal(2, loaded.TabSize);
            Assert.False(loaded.RestoreSession);
            Assert.False(loaded.RecoveryEnabled);
            Assert.Single(loaded.RecentFiles);
            Assert.Single(loaded.Session.Documents);
            Assert.Equal(12, loaded.Session.Documents[0].CaretOffset);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Load_ReturnsDefaultsForMissingOrBrokenFile()
    {
        var directory = TestEnvironment.CreateTempDirectory();
        try
        {
            var missing = new SettingsService(Path.Combine(directory, "missing.json"));
            Assert.Equal("DarkPlus", missing.Load().Theme);

            var brokenPath = Path.Combine(directory, "broken.json");
            File.WriteAllText(brokenPath, "{ this is not json");
            var broken = new SettingsService(brokenPath);
            Assert.Equal("DarkPlus", broken.Load().Theme);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
