using NotepadMoreOS.Models;
using NotepadMoreOS.Services;
using NotepadMoreOS.ViewModels;

namespace NotepadMoreOS.Tests;

public sealed class FindInFilesViewModelTests
{
    private static FindInFilesViewModel CreateViewModel() =>
        new(new FileSearchService(new SearchService()));

    [Fact]
    public async Task Search_PopulatesGroupsAndStatus()
    {
        var root = TestEnvironment.CreateTempDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "a.cs"), "needle one\nneedle two\n");
            await File.WriteAllTextAsync(Path.Combine(root, "b.md"), "needle three\n");

            using var viewModel = CreateViewModel();
            viewModel.Folder = root;
            viewModel.Pattern = "needle";
            viewModel.FileMasks = "*.cs;*.md";

            await viewModel.SearchCommand.ExecuteAsync(null);

            Assert.Equal(2, viewModel.Groups.Count);
            Assert.True(viewModel.HasResults);
            Assert.Contains("3 results", viewModel.Status);
            Assert.Contains("2 files", viewModel.Status);
            Assert.False(viewModel.IsSearching);
            Assert.False(viewModel.HasError);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Search_MissingPattern_ReportsValidationError()
    {
        using var viewModel = CreateViewModel();
        viewModel.Folder = Path.GetTempPath();
        viewModel.Pattern = " ";

        await viewModel.SearchCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasError);
        Assert.Contains("Type something", viewModel.Status);
        Assert.False(viewModel.IsSearching);
    }

    [Fact]
    public async Task SelectedResult_EnablesOpeningAndProducesModel()
    {
        var root = TestEnvironment.CreateTempDirectory();
        try
        {
            var path = Path.Combine(root, "a.txt");
            await File.WriteAllTextAsync(path, "needle\n");

            using var viewModel = CreateViewModel();
            viewModel.Folder = root;
            viewModel.Pattern = "needle";
            viewModel.FileMasks = "*.txt";

            await viewModel.SearchCommand.ExecuteAsync(null);
            var result = viewModel.Groups.Single().Results.Single();
            viewModel.SelectedResult = result;

            Assert.True(viewModel.CanOpenResult);
            var model = viewModel.SelectedResultModel();
            Assert.NotNull(model);
            Assert.Equal(path, model!.FilePath);
            Assert.Equal(6, model.Length);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
