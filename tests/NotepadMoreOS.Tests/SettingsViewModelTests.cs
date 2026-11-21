using NotepadMoreOS.Models;
using NotepadMoreOS.Services;
using NotepadMoreOS.ViewModels;

namespace NotepadMoreOS.Tests;

public sealed class SettingsViewModelTests
{
    [Fact]
    public void ThemeAndAccentChanges_RequestLivePreviewAndApplyToSettings()
    {
        var viewModel = new SettingsViewModel(new AppSettings(), new ThemeService());
        var previewCount = 0;
        viewModel.PreviewChanged += (_, _) => previewCount++;

        viewModel.Theme = viewModel.Themes.First(theme => !theme.IsDark);
        viewModel.AccentColor = "#4c8dff";

        Assert.Equal(2, previewCount);
        Assert.True(viewModel.IsAccentColorValid);
        Assert.Equal("#4C8DFF", viewModel.AccentColor);

        var settings = new AppSettings();
        viewModel.ApplyTo(settings);

        Assert.Equal(viewModel.Theme.Name.ToString(), settings.Theme);
        Assert.Equal("#4C8DFF", settings.AccentColor);
    }

    [Fact]
    public void InvalidAccentColor_DisablesSaveAndDoesNotPreview()
    {
        var viewModel = new SettingsViewModel(new AppSettings(), new ThemeService());
        var previewCount = 0;
        viewModel.PreviewChanged += (_, _) => previewCount++;

        viewModel.AccentColor = "#12G45Z";

        Assert.False(viewModel.IsAccentColorValid);
        Assert.True(viewModel.IsAccentColorInvalid);
        Assert.Equal(0, previewCount);
    }

    [Fact]
    public void AccentPalette_ContainsNamedColorOptionsWithHexCodes()
    {
        var viewModel = new SettingsViewModel(new AppSettings(), new ThemeService());

        Assert.Equal(24, viewModel.AccentPalette.Count);
        Assert.Contains(viewModel.AccentPalette, color => color.Name == "Cyan" && color.Hex == "#42D8D0");
        Assert.All(viewModel.AccentPalette, color => Assert.True(ThemeService.TryNormalizeAccentColor(color.Hex, out _)));
    }

    [Fact]
    public void AccentPalette_TracksTheSelectedColorAndCanResetToDefault()
    {
        var viewModel = new SettingsViewModel(new AppSettings(), new ThemeService());

        var defaultPreset = Assert.Single(viewModel.AccentPalette, color => color.IsSelected);
        Assert.Equal(ThemeService.DefaultAccentColor, defaultPreset.Hex);

        viewModel.SetAccentPresetCommand.Execute("#4C8DFF");

        var selected = Assert.Single(viewModel.AccentPalette, color => color.IsSelected);
        Assert.Equal("Blue", selected.Name);
        Assert.Equal("#4C8DFF", viewModel.AccentColor);

        viewModel.ResetAccentColorCommand.Execute(null);

        Assert.Equal(ThemeService.DefaultAccentColor, viewModel.AccentColor);
        Assert.Equal(ThemeService.DefaultAccentColor, Assert.Single(viewModel.AccentPalette, color => color.IsSelected).Hex);
    }

    [Fact]
    public void CustomAccentColor_LeavesNoPalettePresetSelected()
    {
        var viewModel = new SettingsViewModel(new AppSettings(), new ThemeService());

        viewModel.AccentColor = "#123456";

        Assert.DoesNotContain(viewModel.AccentPalette, color => color.IsSelected);
    }

    [Fact]
    public void TabStyle_DefaultsToRoundedAndCanBePreviewedAndPersisted()
    {
        var viewModel = new SettingsViewModel(new AppSettings(), new ThemeService());
        var previewCount = 0;
        viewModel.PreviewChanged += (_, _) => previewCount++;

        Assert.Equal(TabStyles.Rounded, viewModel.TabStyle.Id);
        Assert.Equal(3, viewModel.TabStyles.Count);

        viewModel.TabStyle = viewModel.TabStyles.First(style => style.Id == TabStyles.Floating);

        Assert.Equal(1, previewCount);

        var settings = new AppSettings();
        viewModel.ApplyTo(settings);
        Assert.Equal(TabStyles.Floating, settings.TabStyle);
    }

    [Fact]
    public void Language_DefaultsToSystemAndPersistsTheChoice()
    {
        var viewModel = new SettingsViewModel(new AppSettings(), new ThemeService());

        Assert.Equal(LocalizationService.System, viewModel.Language.Id);
        Assert.Equal(3, viewModel.Languages.Count);

        viewModel.Language = viewModel.Languages.First(option => option.Id == LocalizationService.PortugueseBrazil);

        var settings = new AppSettings();
        viewModel.ApplyTo(settings);

        Assert.Equal(LocalizationService.PortugueseBrazil, settings.Language);
    }

    [Fact]
    public void UnknownTabStyle_FallsBackToRounded()
    {
        var viewModel = new SettingsViewModel(new AppSettings { TabStyle = "does-not-exist" }, new ThemeService());

        Assert.Equal(TabStyles.Rounded, viewModel.TabStyle.Id);
    }

    [Theory]
    [InlineData("#4c8dff", "#4C8DFF")]
    [InlineData("red", "#5BD08E")]
    [InlineData("#123", "#5BD08E")]
    public void NormalizeAccentColor_ValidatesAndNormalizesInput(string input, string expected)
    {
        Assert.Equal(expected, ThemeService.NormalizeAccentColor(input));
    }
}
