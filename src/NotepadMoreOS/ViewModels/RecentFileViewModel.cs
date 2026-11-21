using System.Windows.Input;

namespace NotepadMoreOS.ViewModels;

public sealed class RecentFileViewModel
{
    public RecentFileViewModel(string path, ICommand openCommand)
    {
        Path = path;
        OpenCommand = openCommand;
        DisplayName = System.IO.Path.GetFileName(path);
        Directory = System.IO.Path.GetDirectoryName(path) ?? string.Empty;
    }

    public string Path { get; }
    public string DisplayName { get; }
    public string Directory { get; }
    public ICommand OpenCommand { get; }
}
