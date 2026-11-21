using NotepadMoreOS.Resources;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace NotepadMoreOS.Views.Dialogs;

public partial class GoToLineWindow : Window
{
    private readonly int _lineCount;

    public GoToLineWindow()
    {
        InitializeComponent();
    }

    public GoToLineWindow(int currentLine, int lineCount) : this()
    {
        _lineCount = Math.Max(1, lineCount);
        Title = Strings.Dialog_GoToLineTitle;
        RangeText.Text = $"Enter a line number between 1 and {_lineCount:N0}.";
        LineBox.Text = currentLine.ToString();
        Opened += (_, _) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                LineBox.Focus();
                LineBox.SelectAll();
            });
        };
    }

    private void OnLineBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Accept();
            e.Handled = true;
        }
    }

    private void OnGoClick(object? sender, RoutedEventArgs e) => Accept();

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);

    private void Accept()
    {
        if (!int.TryParse(LineBox.Text?.Trim(), out var line) || line < 1 || line > _lineCount)
        {
            ErrorText.Text = $"1 – {_lineCount}";
            ErrorText.IsVisible = true;
            return;
        }

        Close((int?)line);
    }
}
