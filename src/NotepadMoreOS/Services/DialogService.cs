using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using NotepadMoreOS.Models;
using NotepadMoreOS.ViewModels;
using NotepadMoreOS.Views.Dialogs;

namespace NotepadMoreOS.Services;

public enum SaveChangesChoice
{
    Cancel,
    DontSave,
    Save
}

public sealed class DialogService
{
    private readonly FileSearchService _fileSearchService;
    private Window? _owner;

    public DialogService(FileSearchService? fileSearchService = null)
    {
        _fileSearchService = fileSearchService ?? new FileSearchService(new SearchService());
    }

    private Window Owner => _owner ?? throw new InvalidOperationException("The dialog service has not been attached to a window.");

    public void Attach(Window owner) => _owner = owner;

    public async Task<IReadOnlyList<string>> PickOpenFilesAsync()
    {
        var files = await Owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open files",
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType("Text and source files")
                {
                    Patterns = new[]
                    {
                        "*.txt", "*.md", "*.log", "*.json", "*.xml", "*.html", "*.htm", "*.css", "*.scss",
                        "*.js", "*.ts", "*.jsx", "*.tsx", "*.cs", "*.csproj", "*.sln", "*.c", "*.h", "*.cpp",
                        "*.hpp", "*.py", "*.rb", "*.go", "*.rs", "*.java", "*.kt", "*.php", "*.sql", "*.yml",
                        "*.yaml", "*.ini", "*.conf", "*.sh", "*.bat", "*.ps1", "*.lua", "*.toml"
                    }
                },
                FilePickerFileTypes.All
            ]
        });

        return files
            .Select(file => file.TryGetLocalPath())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .ToList();
    }

    public async Task<string?> PickSaveFileAsync(DocumentViewModel document)
    {
        var suggestedName = document.FilePath is { } existingPath
            ? Path.GetFileName(existingPath)
            : document.Title + ".txt";

        var extension = Path.GetExtension(suggestedName).TrimStart('.');
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = "txt";
        }

        var file = await Owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save as",
            SuggestedFileName = suggestedName,
            DefaultExtension = extension,
            ShowOverwritePrompt = true,
            FileTypeChoices =
            [
                new FilePickerFileType("Text and source files")
                {
                    Patterns = new[] { "*." + extension, "*.txt", "*.md" }
                },
                FilePickerFileTypes.All
            ]
        });

        return file?.TryGetLocalPath();
    }

    public async Task<SaveChangesChoice> AskSaveChangesAsync(string documentName)
    {
        var window = new SaveChangesWindow(documentName);
        return await window.ShowDialog<SaveChangesChoice>(Owner);
    }

    public async Task ShowErrorAsync(string title, string message)
    {
        var window = new MessageWindow(title, message, "OK", secondaryText: null, destructive: false);
        await window.ShowDialog(Owner);
    }

    public async Task<string?> PromptTextAsync(string title, string message, string? initialValue = null)
    {
        var window = new PromptWindow(title, message, initialValue);
        return await window.ShowDialog<string?>(Owner);
    }

    public async Task<bool> ConfirmAsync(string title, string message, string okText = "OK")
    {
        var window = new MessageWindow(title, message, okText, secondaryText: "Cancel", destructive: false);
        return await window.ShowDialog<bool>(Owner);
    }

    public async Task<int?> ShowGoToLineAsync(int currentLine, int lineCount)
    {
        var window = new GoToLineWindow(currentLine, lineCount);
        return await window.ShowDialog<int?>(Owner);
    }

    public async Task<FileSearchResult?> ShowFindInFilesAsync(string? startFolder, string? initialPattern)
    {
        var viewModel = new FindInFilesViewModel(_fileSearchService);
        var window = new FindInFilesWindow(viewModel, startFolder, initialPattern);
        return await window.ShowDialog<FileSearchResult?>(Owner);
    }

    public async Task<bool> ShowSettingsAsync(SettingsViewModel viewModel)
    {
        var window = new SettingsWindow(viewModel);
        return await window.ShowDialog<bool>(Owner);
    }

    public async Task ShowAboutAsync()
    {
        var window = new AboutWindow();
        await window.ShowDialog(Owner);
    }

    public async Task SetClipboardTextAsync(string text)
    {
        if (Owner.Clipboard is { } clipboard)
        {
            await clipboard.SetValueAsync(DataFormat.Text, text);
        }
    }
}
