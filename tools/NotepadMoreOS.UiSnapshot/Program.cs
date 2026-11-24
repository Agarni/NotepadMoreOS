using NotepadMoreOS.Resources;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using NotepadMoreOS;
using NotepadMoreOS.Models;
using NotepadMoreOS.Services;
using NotepadMoreOS.ViewModels;
using NotepadMoreOS.Views;
using NotepadMoreOS.Views.Dialogs;

namespace NotepadMoreOS.UiSnapshot;

/// <summary>
/// Renders the main window and key dialogs with Avalonia's headless Skia backend.
/// Useful for visual QA and for documenting the UI without opening a real window.
/// Usage: dotnet run --project tools/NotepadMoreOS.UiSnapshot [-- --output docs]
/// </summary>
internal static class Program
{
    private static string _outputDirectory = string.Empty;
    private static BindingTraceListener? _bindingListener;

    public static int Main(string[] args)
    {
        var output = ReadOption(args, "--output") ?? FindRepositoryDirectory() + "/docs";
        _outputDirectory = Path.GetFullPath(output);
        Directory.CreateDirectory(_outputDirectory);
        LocalizationService.Apply(LocalizationService.English);

        _bindingListener = new BindingTraceListener();
        Trace.Listeners.Add(_bindingListener);

        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .LogToTrace()
            .SetupWithoutStarting();

        Run();

        Console.WriteLine($"Screenshots written to {_outputDirectory}");
        return 0;
    }

    /// <summary>
    /// Fails the run when the renderer reported any binding error.
    /// Called right before the headless process exits with success.
    /// </summary>
    private static void FailOnBindingErrors()
    {
        var errors = _bindingListener?.BindingErrors ?? [];
        if (errors.Count == 0)
        {
            return;
        }

        Console.Error.WriteLine($"{errors.Count} binding error(s) detected while rendering:");
        foreach (var error in errors)
        {
            Console.Error.WriteLine($"  {error}");
        }

        Environment.Exit(1);
    }

    /// <summary>
    /// Forwards Avalonia trace output to the console and fails the run when the
    /// renderer reports a binding error, so regressions cannot pass unnoticed.
    /// </summary>
    private sealed class BindingTraceListener : TraceListener
    {
        private readonly List<string> _bindingErrors = [];
        private readonly object _gate = new();

        public IReadOnlyList<string> BindingErrors
        {
            get
            {
                lock (_gate)
                {
                    return _bindingErrors.ToArray();
                }
            }
        }

        public override void Write(string? message)
        {
            Console.Write(message);
            Capture(message);
        }

        public override void WriteLine(string? message)
        {
            Console.WriteLine(message);
            Capture(message);
        }

        private void Capture(string? message)
        {
            if (string.IsNullOrEmpty(message) ||
                !message.Contains("[Binding]", StringComparison.Ordinal))
            {
                return;
            }

            lock (_gate)
            {
                _bindingErrors.Add(message.Trim());
            }
        }
    }

    private static void Run()
    {
        var samples = Path.Combine(Path.GetTempPath(), "notepadmoreos-snapshot", "demo");
        Directory.CreateDirectory(samples);

        var codePath = Path.Combine(samples, "Program.cs");
        var notesPath = Path.Combine(samples, "notes.md");
        File.WriteAllText(codePath, SampleCode);
        File.WriteAllText(notesPath, SampleMarkdown);

        var settingsPath = Path.Combine(Path.GetTempPath(), "notepadmoreos-snapshot", "settings.json");
        var settingsService = new SettingsService(settingsPath);
        var settings = new AppSettings { RestoreSession = true };

        var languageService = new LanguageService();
        var themeService = new ThemeService();
        themeService.Apply(themeService.Find("DarkPlus"));
        themeService.ApplyAccentColor(settings.AccentColor);

        var viewModel = new MainWindowViewModel(
            settingsService,
            new FileService(),
            languageService,
            themeService,
            new SearchService(),
            new DialogService(),
            new SessionService(Path.Combine(Path.GetTempPath(), "notepadmoreos-snapshot", "recovery")),
            new FileWatchService(),
            settings);

        viewModel.RecentFiles.Add(new RecentFileViewModel("/Projects/website/index.html", viewModel.OpenRecentCommand));
        viewModel.RecentFiles.Add(new RecentFileViewModel("/Projects/api/Program.cs", viewModel.OpenRecentCommand));
        viewModel.RecentFiles.Add(new RecentFileViewModel("/Documents/notes.md", viewModel.OpenRecentCommand));

        var window = new MainWindow { DataContext = viewModel };
        window.Show();
        Pump();

        Capture(window, "screenshot-welcome.png");
        viewModel.RecentFiles.Clear();

        Wait(viewModel.OpenPathsAsync([codePath, notesPath]));
        viewModel.ActiveDocument = viewModel.Documents[1];
        Pump();
        var documentView = window.GetVisualDescendants().OfType<DocumentView>().First();
        var layout = documentView.FindControl<Grid>("EditorLayout")!;
        var editor = documentView.FindControl<AvaloniaEdit.TextEditor>("Editor")!;
        var fullHeight = editor.Bounds.Height;
        viewModel.ActiveDocument.MarkdownPreviewOpen = true;
        Pump();
        if (layout.RowDefinitions[2].ActualHeight <= 80 || editor.Bounds.Height >= fullHeight)
            throw new InvalidOperationException("Markdown preview did not share the editor area.");
        layout.RowDefinitions[0].Height = new GridLength(3, GridUnitType.Star);
        layout.RowDefinitions[2].Height = new GridLength(1, GridUnitType.Star);
        Pump();
        viewModel.ActiveDocument.MarkdownPreviewOpen = false;
        Pump();
        if (layout.RowDefinitions[2].ActualHeight > 0.5 || Math.Abs(editor.Bounds.Height - fullHeight) > 1)
            throw new InvalidOperationException("Closing Markdown preview did not restore the editor area.");
        Console.WriteLine("Markdown preview layout OK");
        viewModel.Documents[1].TabColorHex = "#4A9EDB";
        Wait(viewModel.SaveSessionWithNameAsync("Demo workspace", replaceExisting: false));
        viewModel.ActiveDocument = viewModel.Documents[0];
        Pump();
        Pump();

        if (viewModel.ActiveDocument?.Host is { } host)
        {
            host.CaretOffset = viewModel.ActiveDocument.Document.GetLineByNumber(14).Offset + 8;
        }

        Pump();
        Capture(window, "screenshot-dark.png");

        var sessionButton = window.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(button => button.Classes.Contains("session-chip"));
        if (sessionButton?.Flyout is { } sessionFlyout)
        {
            sessionFlyout.ShowAt(sessionButton);
            Pump();
            Capture(window, "screenshot-sessions.png");
            sessionFlyout.Hide();
            Pump();

            VerifySessionFlyoutSelection(viewModel, sessionButton);
        }

        var tabButton = window.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(button => button.Classes.Contains("tab"));
        if (tabButton?.ContextFlyout is { } tabFlyout)
        {
            tabFlyout.ShowAt(tabButton);
            Pump();
            Capture(window, "screenshot-tab-menu.png");
            tabFlyout.Hide();
            Pump();
        }

        themeService.Apply(themeService.Find("LightPlus"));
        Pump();
        Capture(window, "screenshot-light.png");

        themeService.Apply(themeService.Find("DarkPlus"));
        viewModel.ActiveDocument = viewModel.Documents[0];
        Pump();
        viewModel.OpenFindCommand.Execute(null);
        viewModel.FindReplace.SearchText = "Console";
        var liveMatchStatus = viewModel.FindReplace.Status;
        viewModel.FindReplace.FindAllCommand.Execute(null);
        if (string.IsNullOrWhiteSpace(liveMatchStatus) ||
            !liveMatchStatus.Any(char.IsDigit) ||
            !string.Equals(liveMatchStatus, viewModel.FindReplace.Status, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The live match count '{liveMatchStatus}' did not match Find All '{viewModel.FindReplace.Status}'.");
        }
        Pump();
        Capture(window, "screenshot-find.png");

        // Extended search must interpret escapes without turning on regex.
        viewModel.FindReplace.UseExtendedEscapes = true;
        viewModel.FindReplace.SearchText = @"\s";
        if (viewModel.FindReplace.Status != Strings.Find_NoMatches)
        {
            throw new InvalidOperationException(
                $"Extended search treated '\\s' as a class (status '{viewModel.FindReplace.Status}').");
        }

        viewModel.FindReplace.SearchText = @"\n";
        if (!viewModel.FindReplace.Status.Any(char.IsDigit))
        {
            throw new InvalidOperationException("Extended search did not find the line breaks.");
        }

        viewModel.FindReplace.UseRegex = true;
        if (viewModel.FindReplace.UseExtendedEscapes)
        {
            throw new InvalidOperationException("Regular expression and extended escapes are not mutually exclusive.");
        }

        viewModel.FindReplace.UseRegex = false;
        viewModel.FindReplace.SearchText = "Console";
        Pump();

        var findInFilesViewModel = new FindInFilesViewModel(new FileSearchService(new SearchService()));
        var findInFiles = new FindInFilesWindow(findInFilesViewModel, samples, "Console");
        findInFiles.Show(window);
        Pump();
        findInFilesViewModel.SearchCommand.Execute(null);
        WaitUntil(() => !findInFilesViewModel.IsSearching, TimeSpan.FromSeconds(15));
        Pump();
        Capture(findInFiles, "screenshot-findinfiles.png");
        findInFiles.Close();

        var settingsWindow = new SettingsWindow(new SettingsViewModel(settings, themeService));
        var settingsViewModel = (SettingsViewModel)settingsWindow.DataContext!;
        EventHandler previewHandler = (_, _) =>
        {
            themeService.Apply(settingsViewModel.Theme);
            themeService.ApplyAccentColor(settingsViewModel.AccentColor);
        };
        settingsViewModel.PreviewChanged += previewHandler;
        settingsViewModel.Theme = themeService.Find("Dracula");
        settingsViewModel.AccentColor = "#42D8D0";
        settingsWindow.Show(window);
        Pump();

        var blueSwatch = settingsWindow.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(button => button.DataContext is AccentColorPreset preset && preset.Hex == "#4C8DFF");
        if (blueSwatch?.Command?.CanExecute(blueSwatch.CommandParameter) != true)
        {
            throw new InvalidOperationException("The accent palette's blue swatch is not available.");
        }

        blueSwatch.Command.Execute(blueSwatch.CommandParameter);
        Pump();
        if (settingsViewModel.AccentColor != "#4C8DFF")
        {
            throw new InvalidOperationException("Selecting a palette swatch did not update the accent color.");
        }

        Capture(settingsWindow, "screenshot-settings.png");
        settingsWindow.Close();
        settingsViewModel.PreviewChanged -= previewHandler;
        themeService.Apply(themeService.Find(settings.Theme));
        themeService.ApplyAccentColor(settings.AccentColor);
        Pump();

        var aboutWindow = new AboutWindow();
        aboutWindow.Show(window);
        Pump();
        Capture(aboutWindow, "screenshot-about.png");
        aboutWindow.Close();

        // Regression guard: switching between documents of the same type must
        // replace the visible text instead of reusing the previous document.
        viewModel.NewDocumentCommand.Execute(null);
        var firstNewDocument = viewModel.ActiveDocument!;
        firstNewDocument.Document.Text = "FIRST_TAB_CONTENT_MARKER";

        viewModel.NewDocumentCommand.Execute(null);
        var secondNewDocument = viewModel.ActiveDocument!;
        secondNewDocument.Document.Text = "SECOND_TAB_CONTENT_MARKER";
        Pump();

        viewModel.ActiveDocument = firstNewDocument;
        Pump();
        var visibleText = window.GetVisualDescendants().OfType<TextEditor>().FirstOrDefault()?.Text ?? string.Empty;
        if (!visibleText.Contains("FIRST_TAB_CONTENT_MARKER", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The first new tab did not show its own content.");
        }

        viewModel.ActiveDocument = secondNewDocument;
        Pump();
        visibleText = window.GetVisualDescendants().OfType<TextEditor>().FirstOrDefault()?.Text ?? string.Empty;
        if (!visibleText.Contains("SECOND_TAB_CONTENT_MARKER", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The second new tab did not show its own content.");
        }

        Console.WriteLine("Tab content switching OK");

        secondNewDocument.Document.Text = "{\n  \"items\": [\n    1\n  ]\n}";
        secondNewDocument.Language = languageService.FindById("json");
        Pump();
        var foldingMargin = window.GetVisualDescendants().OfType<AvaloniaEdit.Folding.FoldingMargin>().FirstOrDefault();
        var folds = foldingMargin?.FoldingManager?.AllFoldings.ToArray();
        if (folds is null || folds.Length != 2)
            throw new InvalidOperationException("JSON folding regions were not installed.");
        folds[0].IsFolded = true;
        Pump();
        if (!folds[0].IsFolded || !secondNewDocument.Document.Text.Contains("items", StringComparison.Ordinal))
            throw new InvalidOperationException("Folding changed the document or failed to collapse.");
        folds[0].IsFolded = false;
        Pump();
        Console.WriteLine("JSON folding OK");

        // Regression guard: many tabs expose arrow navigation instead of a scrollbar.
        for (var index = 0; index < 10; index++)
        {
            viewModel.NewDocumentCommand.Execute(null);
        }

        Pump();
        Pump();
        var tabScroll = window.GetVisualDescendants().OfType<ScrollViewer>()
            .FirstOrDefault(scrollViewer => scrollViewer.Name == "TabScroll");
        var scrollRight = window.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(button => button.Name == "TabScrollRight");
        if (tabScroll is null || scrollRight is null ||
            tabScroll.Extent.Width <= tabScroll.Viewport.Width + 4)
        {
            throw new InvalidOperationException("The tab strip did not overflow as expected.");
        }

        tabScroll.Offset = new Vector(0, 0);
        Pump();
        if (!scrollRight.IsVisible)
        {
            throw new InvalidOperationException("The tab scroll-right button is not available on overflow.");
        }

        scrollRight.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Pump();
        if (tabScroll.Offset.X <= 0)
        {
            throw new InvalidOperationException("The tab scroll-right button did not scroll the strip.");
        }

        Console.WriteLine("Tab overflow navigation OK");

        FailOnBindingErrors();

        // The headless lifetime has no message loop to complete the async close flow.
        Environment.Exit(0);
    }

    private static void VerifySessionFlyoutSelection(MainWindowViewModel viewModel, Button sessionButton)
    {
        if (sessionButton.Flyout is not Flyout flyout || flyout.Content is not Control content)
        {
            throw new InvalidOperationException("The session flyout content is not available.");
        }

        Wait(viewModel.SaveSessionWithNameAsync("Session switch target", replaceExisting: false));
        if (viewModel.CurrentSessionName == "Demo workspace")
        {
            throw new InvalidOperationException("The session switch target did not become active.");
        }

        flyout.ShowAt(sessionButton);
        Pump();

        var sessionItem = content.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(button => button.DataContext is SessionItemViewModel item &&
                                      item.Name == "Demo workspace");
        if (sessionItem is null)
        {
            throw new InvalidOperationException("Could not find the Demo workspace item in the session flyout.");
        }

        var topLevel = TopLevel.GetTopLevel(sessionItem);
        if (topLevel is null)
        {
            throw new InvalidOperationException("Could not find the session item's top-level window.");
        }

        var point = sessionItem.TranslatePoint(
            new Point(sessionItem.Bounds.Width / 2, sessionItem.Bounds.Height / 2),
            topLevel);
        if (point is null)
        {
            throw new InvalidOperationException("Could not resolve the session item click position.");
        }

        topLevel.MouseDown(point.Value, MouseButton.Left);
        topLevel.MouseUp(point.Value, MouseButton.Left);

        WaitUntil(
            () => viewModel.CurrentSessionName == "Demo workspace",
            TimeSpan.FromSeconds(5));
        Pump();

        if (viewModel.CurrentSessionName != "Demo workspace")
        {
            throw new InvalidOperationException("Selecting a session in the flyout did not open it.");
        }

        if (flyout.IsOpen)
        {
            throw new InvalidOperationException("The session flyout stayed open after selecting a session.");
        }

        viewModel.DeleteSession("Session switch target");
        Console.WriteLine("Session flyout selection OK");
    }

    private static void Capture(Window window, string fileName)
    {
        var frame = window.CaptureRenderedFrame();
        if (frame is null)
        {
            Console.WriteLine($"Could not capture {fileName}");
            return;
        }

        var path = Path.Combine(_outputDirectory, fileName);
#pragma warning disable CS0618
        frame.Save(path);
#pragma warning restore CS0618
        Console.WriteLine($"Captured {fileName}");
    }

    private static void Pump(int iterations = 12)
    {
        for (var i = 0; i < iterations; i++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(30);
        }
    }

    private static void Wait(Task task)
    {
        while (!task.IsCompleted)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(20);
        }

        task.GetAwaiter().GetResult();
    }

    private static void WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition() && stopwatch.Elapsed < timeout)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(20);
        }
    }

    private static string? ReadOption(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static string FindRepositoryDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NotepadMoreOS.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private const string SampleCode = """
using System;
using System.Collections.Generic;
using System.Linq;

namespace NotepadMoreOS.Demo;

/// <summary>A small demo used to show syntax highlighting.</summary>
public static class Program
{
    public static void Main()
    {
        var languages = new[] { "C#", "Rust", "TypeScript", "Python" };

        foreach (var language in languages.OrderBy(name => name))
        {
            Console.WriteLine($"Hello from {language}!");
        }

        // The status bar tracks line, column, selection, length and encoding.
        var total = languages.Length;
        Console.WriteLine($"Total: {total}");
    }
}
""";

    private const string SampleMarkdown = """
# NotepadMoreOS

A modern, cross-platform text editor inspired by Notepad++.

- Multi-tab editing
- TextMate syntax highlighting
- Find and replace with regex
- Encoding and line-ending control
- Session restore and themes
""";
}
