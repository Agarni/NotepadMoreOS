using NotepadMoreOS.Resources;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using NotepadMoreOS.ViewModels;

namespace NotepadMoreOS.Views.Dialogs;

public partial class AboutWindow : Window
{
    private const string ProjectUrl = "https://github.com/notepad-plus-plus/notepad-plus-plus";
    private const string RepositoryUrl = "https://github.com/Agarni/NotepadMoreOS";
    private readonly UpdateCheckViewModel _updates = new();

    public AboutWindow()
    {
        InitializeComponent();
        DataContext = _updates;

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = version is null
            ? string.Format(Strings.About_Version, "1.0")
            : string.Format(Strings.About_Version, $"{version.Major}.{version.Minor}.{version.Build}");

        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("NotepadMoreOS.Changelog.md");
        if (stream is not null)
        {
            using var reader = new StreamReader(stream);
            ChangelogViewer.Markdown = reader.ReadToEnd();
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private async void OnRepositoryClick(object? sender, RoutedEventArgs e) => await OpenAsync(RepositoryUrl);

    private async void OnProjectClick(object? sender, RoutedEventArgs e) => await OpenAsync(ProjectUrl);

    protected override void OnClosed(EventArgs e)
    {
        _updates.Cancel();
        base.OnClosed(e);
    }

    private async void OnReleaseClick(object? sender, RoutedEventArgs e) => await OpenAsync(RepositoryUrl + "/releases/latest");

    private async Task OpenAsync(string url)
    {
        if (TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
        {
            await launcher.LaunchUriAsync(new Uri(url));
        }
    }
}
