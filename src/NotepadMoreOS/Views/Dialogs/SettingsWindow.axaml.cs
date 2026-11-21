using Avalonia.Controls;
using Avalonia.Interactivity;
using NotepadMoreOS.ViewModels;

namespace NotepadMoreOS.Views.Dialogs;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    public SettingsWindow(SettingsViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
