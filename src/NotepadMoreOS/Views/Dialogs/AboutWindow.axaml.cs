using NotepadMoreOS.Resources;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NotepadMoreOS.Views.Dialogs;

public partial class AboutWindow : Window
{
    private const string ProjectUrl = "https://github.com/notepad-plus-plus/notepad-plus-plus";
    private const string RepositoryUrl = "https://github.com/Agarni/NotepadMoreOS";

    public AboutWindow()
    {
        InitializeComponent();

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = version is null
            ? string.Format(Strings.About_Version, "1.0")
            : string.Format(Strings.About_Version, $"{version.Major}.{version.Minor}.{version.Build}");
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private async void OnRepositoryClick(object? sender, RoutedEventArgs e) => await OpenAsync(RepositoryUrl);

    private async void OnProjectClick(object? sender, RoutedEventArgs e) => await OpenAsync(ProjectUrl);

    private async Task OpenAsync(string url)
    {
        if (TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
        {
            await launcher.LaunchUriAsync(new Uri(url));
        }
    }
}
