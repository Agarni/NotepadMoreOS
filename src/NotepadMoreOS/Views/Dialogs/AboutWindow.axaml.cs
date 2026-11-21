using NotepadMoreOS.Resources;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NotepadMoreOS.Views.Dialogs;

public partial class AboutWindow : Window
{
    private const string ProjectUrl = "https://github.com/notepad-plus-plus/notepad-plus-plus";

    public AboutWindow()
    {
        InitializeComponent();

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = version is null
            ? string.Format(Strings.About_Version, "1.0")
            : string.Format(Strings.About_Version, $"{version.Major}.{version.Minor}.{version.Build}");
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private async void OnProjectClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
        {
            await launcher.LaunchUriAsync(new Uri(ProjectUrl));
        }
    }
}
