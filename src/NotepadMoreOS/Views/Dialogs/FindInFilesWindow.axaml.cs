using NotepadMoreOS.Resources;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NotepadMoreOS.Models;
using NotepadMoreOS.ViewModels;

namespace NotepadMoreOS.Views.Dialogs;

public partial class FindInFilesWindow : Window
{
    private readonly FindInFilesViewModel? _viewModel;

    public FindInFilesWindow()
    {
        InitializeComponent();
    }

    public FindInFilesWindow(FindInFilesViewModel viewModel, string? startFolder, string? initialPattern)
        : this()
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.Initialize(startFolder, initialPattern);
    }

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Strings.Dialog_ChooseFolder,
            AllowMultiple = false
        });

        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            _viewModel.Folder = path;
        }
    }

    private void OnFieldKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _viewModel is null)
        {
            return;
        }

        _viewModel.SearchCommand.Execute(null);
        e.Handled = true;
    }

    private void OnResultsSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.SelectedResult = ResultsTree.SelectedItem as FileSearchResultViewModel;
        }
    }

    private void OnResultsDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ResultsTree.SelectedItem is FileSearchResultViewModel result)
        {
            Close(result.ToModel());
            e.Handled = true;
        }
    }

    private void OnResultsKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || ResultsTree.SelectedItem is not FileSearchResultViewModel result)
        {
            return;
        }

        Close(result.ToModel());
        e.Handled = true;
    }

    private void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        var result = _viewModel?.SelectedResultModel();
        if (result is not null)
        {
            Close(result);
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close((FileSearchResult?)null);
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel?.Dispose();
        base.OnClosed(e);
    }
}
