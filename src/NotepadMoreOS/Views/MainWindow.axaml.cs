using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NotepadMoreOS.Models;
using NotepadMoreOS.Services;
using NotepadMoreOS.ViewModels;

namespace NotepadMoreOS.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;
    private ScrollViewer? _tabScroll;
    private bool _closeApproved;
    private bool _closeInProgress;
    private bool _navigatingResults;

    public MainWindow()
    {
        InitializeComponent();
        InitializeCustomChrome();
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);

        if (this.FindControl<ScrollViewer>("TabScroll") is { } tabScroll)
        {
            _tabScroll = tabScroll;
            _tabScroll.ScrollChanged += (_, _) => UpdateTabScrollButtons();
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.FindFocusRequested -= OnFindFocusRequested;
            _viewModel.ExitRequested -= OnExitRequested;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as MainWindowViewModel;

        if (_viewModel is not null)
        {
            _viewModel.FindFocusRequested += OnFindFocusRequested;
            _viewModel.ExitRequested += OnExitRequested;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        base.OnDataContextChanged(e);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        Dispatcher.UIThread.Post(InitializeNativeMenu, DispatcherPriority.Loaded);
        Dispatcher.UIThread.Post(UpdateTabScrollButtons, DispatcherPriority.Loaded);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.ActiveDocument))
        {
            Dispatcher.UIThread.Post(BringActiveTabIntoView, DispatcherPriority.Loaded);
        }
    }

    private void BringActiveTabIntoView()
    {
        if (_tabScroll is null || _viewModel?.ActiveDocument is null)
        {
            return;
        }

        var tab = _tabScroll.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(button => ReferenceEquals(button.DataContext, _viewModel.ActiveDocument));
        tab?.BringIntoView();
        UpdateTabScrollButtons();
    }

    private void OnTabScrollLeftClick(object? sender, RoutedEventArgs e) => ScrollTabs(-1);

    private void OnTabScrollRightClick(object? sender, RoutedEventArgs e) => ScrollTabs(1);

    private void ScrollTabs(int direction)
    {
        if (_tabScroll is null)
        {
            return;
        }

        var step = Math.Max(120, _tabScroll.Viewport.Width * 0.75);
        var max = Math.Max(0, _tabScroll.Extent.Width - _tabScroll.Viewport.Width);
        var target = Math.Clamp(_tabScroll.Offset.X + direction * step, 0, max);
        _tabScroll.Offset = new Vector(target, _tabScroll.Offset.Y);
        UpdateTabScrollButtons();
    }

    private void OnTabStripPointerWheel(object? sender, PointerWheelEventArgs e)
    {
        if (_tabScroll is null || e.Delta.Y == 0)
        {
            return;
        }

        var max = Math.Max(0, _tabScroll.Extent.Width - _tabScroll.Viewport.Width);
        if (max <= 0)
        {
            return;
        }

        var target = Math.Clamp(_tabScroll.Offset.X - e.Delta.Y * 60, 0, max);
        _tabScroll.Offset = new Vector(target, _tabScroll.Offset.Y);
        e.Handled = true;
        UpdateTabScrollButtons();
    }

    private void UpdateTabScrollButtons()
    {
        if (_tabScroll is null)
        {
            return;
        }

        var max = Math.Max(0, _tabScroll.Extent.Width - _tabScroll.Viewport.Width);
        const double tolerance = 4;
        if (this.FindControl<Button>("TabScrollLeft") is { } left)
        {
            left.IsVisible = max > tolerance && _tabScroll.Offset.X > tolerance;
        }

        if (this.FindControl<Button>("TabScrollRight") is { } right)
        {
            right.IsVisible = max > tolerance && _tabScroll.Offset.X < max - tolerance;
        }
    }

    private void OnSessionFlyoutItemClick(object? sender, RoutedEventArgs e)
    {
        // Click is raised before the button command runs. Hiding the flyout
        // immediately can clear the command parameter, so close it only after
        // the current input event has completed.
        if (this.FindControl<Button>("SessionButton")?.Flyout is { } flyout)
        {
            Dispatcher.UIThread.Post(flyout.Hide, DispatcherPriority.Background);
        }
    }

    private void InitializeCustomChrome()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        // The custom traffic lights are drawn by us; headless screenshots also
        // show them, while the real macOS window uses client-side decorations.
        TrafficLights.IsVisible = true;

        if (IsHeadlessPlatform())
        {
            // Headless has no native macOS menu bar; hide the in-window menu so
            // screenshots represent the real macOS custom-titlebar layout.
            AppMenu.IsVisible = false;
            return;
        }

        ExtendClientAreaToDecorationsHint = true;
        this.WindowDecorations = WindowDecorations.BorderOnly;
        ExtendClientAreaTitleBarHeightHint = 52;
        WorkspaceBar.PointerPressed += OnWorkspaceBarPointerPressed;
    }

    private void OnWorkspaceBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Handled ||
            IsInsideButton(e.Source) ||
            !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
            e.Handled = true;
            return;
        }

        BeginMoveDrag(e);
    }

    private static bool IsInsideButton(object? source)
    {
        var control = source as Control;
        while (control is not null)
        {
            if (control is Button or ToggleButton)
            {
                return true;
            }

            control = control.Parent as Control;
        }

        return false;
    }

    private void OnChromeCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void OnChromeMinimizeClick(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void OnChromeMaximizeClick(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void OnExitRequested(object? sender, EventArgs e) => Close();

    private void OnRecentMenuOpened(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not MenuItem menu)
        {
            return;
        }

        menu.Items.Clear();

        foreach (var recent in _viewModel.RecentFiles)
        {
            var item = new MenuItem
            {
                Header = recent.DisplayName,
                Command = recent.OpenCommand,
                CommandParameter = recent.Path
            };
            ToolTip.SetTip(item, recent.Path);
            menu.Items.Add(item);
        }

        if (_viewModel.RecentFiles.Count == 0)
        {
            menu.Items.Add(new MenuItem { Header = "No recent files", IsEnabled = false });
        }
    }

    private void OnReopenEncodingMenuOpened(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not MenuItem menu)
        {
            return;
        }

        menu.Items.Clear();
        menu.Items.Add(new MenuItem
        {
            Header = "Detect automatically",
            Command = _viewModel.ReopenWithDetectedEncodingCommand
        });
        menu.Items.Add(new Separator());

        foreach (var option in _viewModel.EncodingOptions)
        {
            var item = new MenuItem
            {
                Header = option.Name,
                Command = _viewModel.ReopenWithEncodingCommand,
                CommandParameter = option
            };

            if (_viewModel.ActiveDocument?.Encoding.Kind == option.Kind)
            {
                item.ToggleType = MenuItemToggleType.Radio;
                item.GroupName = "reopen-encoding";
                item.IsChecked = true;
            }

            menu.Items.Add(item);
        }
    }

    private void OnSaveEncodingMenuOpened(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not MenuItem menu)
        {
            return;
        }

        menu.Items.Clear();

        foreach (var option in _viewModel.EncodingOptions)
        {
            menu.Items.Add(new MenuItem
            {
                Header = option.Name,
                ToggleType = MenuItemToggleType.Radio,
                GroupName = "save-encoding",
                IsChecked = _viewModel.ActiveDocument?.Encoding.Kind == option.Kind,
                Command = _viewModel.SaveWithEncodingCommand,
                CommandParameter = option
            });
        }
    }

    private void OnThemeMenuOpened(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not MenuItem menu)
        {
            return;
        }

        menu.Items.Clear();

        foreach (var theme in _viewModel.Themes)
        {
            menu.Items.Add(new MenuItem
            {
                Header = theme.DisplayName,
                ToggleType = MenuItemToggleType.Radio,
                GroupName = "theme",
                IsChecked = string.Equals(theme.Name.ToString(), _viewModel.Settings.Theme, StringComparison.OrdinalIgnoreCase),
                Command = _viewModel.SetThemeCommand,
                CommandParameter = theme
            });
        }
    }

    private void OnAccentMenuOpened(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not MenuItem menu)
        {
            return;
        }

        menu.Items.Clear();

        foreach (var option in _viewModel.AccentPalette)
        {
            menu.Items.Add(new MenuItem
            {
                Header = option.Name,
                ToggleType = MenuItemToggleType.Radio,
                GroupName = "accent",
                IsChecked = string.Equals(option.Hex, _viewModel.Settings.AccentColor, StringComparison.OrdinalIgnoreCase),
                Command = _viewModel.SetAccentColorCommand,
                CommandParameter = option.Hex
            });
        }

        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem
        {
            Header = "Reset to Default",
            Command = _viewModel.SetAccentColorCommand,
            CommandParameter = ThemeService.DefaultAccentColor
        });
    }

    private void OnFindFocusRequested(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var searchBox = this.FindControl<TextBox>("SearchBox");
            searchBox?.Focus();
            searchBox?.SelectAll();
        }, DispatcherPriority.Input);
    }

    private void OnFindBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel is null || e.Key != Key.Enter)
        {
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            _viewModel.FindReplace.FindPreviousCommand.Execute(null);
        }
        else
        {
            _viewModel.FindReplace.FindNextCommand.Execute(null);
        }

        e.Handled = true;
    }

    private void OnSearchResultSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_navigatingResults ||
            _viewModel is null ||
            sender is not ListBox { SelectedItem: SearchResultItem item } listBox)
        {
            return;
        }

        _viewModel.OpenSearchResult(item);

        _navigatingResults = true;
        listBox.SelectedItem = null;
        _navigatingResults = false;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var files = e.DataTransfer.TryGetFiles();
        if (files is null || files.Length == 0)
        {
            return;
        }

        var paths = files
            .Select(file => file.TryGetLocalPath())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .ToList();

        if (paths.Count > 0)
        {
            await _viewModel.OpenPathsAsync(paths);
        }

        e.Handled = true;
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeApproved)
        {
            return;
        }

        e.Cancel = true;

        if (_closeInProgress)
        {
            return;
        }

        _closeInProgress = true;
        try
        {
            if (_viewModel is null || await _viewModel.RequestCloseApplicationAsync())
            {
                _closeApproved = true;
                Dispatcher.UIThread.Post(Close);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to close cleanly: {ex}");
        }
        finally
        {
            _closeInProgress = false;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel?.Dispose();
        base.OnClosed(e);
    }
}
