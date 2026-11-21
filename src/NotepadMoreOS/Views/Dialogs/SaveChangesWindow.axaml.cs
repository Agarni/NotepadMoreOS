using NotepadMoreOS.Resources;
using Avalonia.Controls;
using Avalonia.Interactivity;
using NotepadMoreOS.Services;

namespace NotepadMoreOS.Views.Dialogs;

public partial class SaveChangesWindow : Window
{
    public SaveChangesWindow()
    {
        InitializeComponent();
    }

    public SaveChangesWindow(string documentName) : this()
    {
        Title = Strings.Dialog_UnsavedTitle;
        MessageText.Text = $"Do you want to save the changes you made to \u201c{documentName}\u201d?";
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e) => Close(SaveChangesChoice.Save);
    private void OnDontSaveClick(object? sender, RoutedEventArgs e) => Close(SaveChangesChoice.DontSave);
    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(SaveChangesChoice.Cancel);
}
