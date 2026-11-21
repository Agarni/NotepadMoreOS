using NotepadMoreOS.Resources;
using System.Globalization;
using System.Windows.Input;
using NotepadMoreOS.Models;

namespace NotepadMoreOS.ViewModels;

public sealed class SessionItemViewModel
{
    public SessionItemViewModel(
        NamedSession session,
        ICommand openCommand,
        ICommand renameCommand,
        ICommand deleteCommand)
    {
        Session = session;
        OpenCommand = openCommand;
        RenameCommand = renameCommand;
        DeleteCommand = deleteCommand;
    }

    public NamedSession Session { get; }

    public string Name => Session.Name;

    public string Details
    {
        get
        {
            var savedAt = Session.SavedAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
            return Session.Files.Count == 0
                ? string.Format(Strings.Session_EmptySummary, savedAt)
                : string.Format(
                    Session.Files.Count == 1 ? Strings.Session_FileCount : Strings.Session_FilesCount,
                    Session.Files.Count) + $" · {savedAt}";
        }
    }

    public ICommand OpenCommand { get; }
    public ICommand RenameCommand { get; }
    public ICommand DeleteCommand { get; }
}
