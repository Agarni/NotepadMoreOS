using Avalonia.Controls;
using Avalonia.Interactivity;

namespace NotepadMoreOS.Views.Dialogs;

public partial class MessageWindow : Window
{
    public MessageWindow()
    {
        InitializeComponent();
    }

    public MessageWindow(string title, string message, string primaryText, string? secondaryText, bool destructive)
        : this()
    {
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
        PrimaryButton.Content = primaryText;

        if (destructive)
        {
            PrimaryButton.Classes.Remove("accent");
            PrimaryButton.Classes.Add("danger");
        }

        if (secondaryText is null)
        {
            SecondaryButton.IsVisible = false;
        }
        else
        {
            SecondaryButton.Content = secondaryText;
        }
    }

    private void OnPrimaryClick(object? sender, RoutedEventArgs e) => Close(true);

    private void OnSecondaryClick(object? sender, RoutedEventArgs e) => Close(false);
}
