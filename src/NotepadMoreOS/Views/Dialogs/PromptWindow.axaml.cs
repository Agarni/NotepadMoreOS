using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace NotepadMoreOS.Views.Dialogs;

public partial class PromptWindow : Window
{
    public PromptWindow()
    {
        InitializeComponent();
    }

    public PromptWindow(string title, string message, string? initialValue = null) : this()
    {
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
        ValueBox.Text = initialValue ?? string.Empty;

        Opened += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            ValueBox.Focus();
            ValueBox.SelectAll();
        });
    }

    private void OnValueKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Accept();
            e.Handled = true;
        }
    }

    private void OnOkClick(object? sender, RoutedEventArgs e) => Accept();

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);

    private void Accept()
    {
        var value = ValueBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(value))
        {
            Close(value);
        }
    }
}
