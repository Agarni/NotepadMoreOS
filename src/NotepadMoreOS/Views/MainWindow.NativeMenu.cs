using NotepadMoreOS.Resources;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using NotepadMoreOS.Models;
using NotepadMoreOS.Services;
using NotepadMoreOS.ViewModels;

namespace NotepadMoreOS.Views;

/// <summary>
/// Builds the native macOS menu bar. The in-window menu remains the source of
/// truth on Windows, Linux and headless rendering.
/// </summary>
public partial class MainWindow
{
    private bool _nativeMenuInitialized;
    private NativeMenu? _windowNativeMenu;

    private void InitializeNativeMenu()
    {
        var viewModel = _viewModel;
        if (_nativeMenuInitialized ||
            _windowNativeMenu is not null ||
            viewModel is null ||
            !OperatingSystem.IsMacOS() ||
            IsHeadlessPlatform())
        {
            return;
        }

        _windowNativeMenu = BuildWindowMenu(viewModel);
        NativeMenu.SetMenu(this, _windowNativeMenu);

        // The native menu owns the app-level gestures. Keeping the window key
        // bindings active as well would execute commands twice on macOS.
        KeyBindings.Clear();
        KeyBindings.Add(new KeyBinding
        {
            Gesture = new KeyGesture(Key.Escape),
            Command = viewModel.CloseFindCommand
        });

        ClearInWindowHotKeys(AppMenu.Items);
        AppMenu.IsVisible = false;
        _nativeMenuInitialized = true;
    }

    private bool IsHeadlessPlatform()
    {
        if (PlatformImpl?.GetType().FullName is { } fullName &&
            fullName.Contains("Headless", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return AppDomain.CurrentDomain.GetAssemblies().Any(assembly =>
            string.Equals(assembly.GetName().Name, "Avalonia.Headless", StringComparison.Ordinal));
    }

    private static void ClearInWindowHotKeys(IEnumerable<object?> items)
    {
        foreach (var entry in items)
        {
            if (entry is MenuItem item)
            {
                item.HotKey = null;
                ClearInWindowHotKeys(item.Items.Cast<object>());
            }
        }
    }

    private static NativeMenu BuildWindowMenu(MainWindowViewModel viewModel)
    {
        var menu = new NativeMenu();
        menu.Items.Add(SubMenu(Strings.NativeMenu_File, BuildFileMenu(viewModel)));
        menu.Items.Add(SubMenu(Strings.NativeMenu_Edit, BuildEditMenu(viewModel)));
        menu.Items.Add(SubMenu(Strings.NativeMenu_Search, BuildSearchMenu(viewModel)));
        menu.Items.Add(SubMenu(Strings.NativeMenu_Encoding, BuildEncodingMenu(viewModel)));
        menu.Items.Add(SubMenu(Strings.NativeMenu_View, BuildViewMenu(viewModel)));
        menu.Items.Add(SubMenu(Strings.NativeMenu_Help, BuildHelpMenu(viewModel)));
        return menu;
    }

    private static NativeMenu BuildFileMenu(MainWindowViewModel viewModel)
    {
        var menu = new NativeMenu();
        menu.Items.Add(Item(Strings.Menu_New, viewModel.NewDocumentCommand, Command(Key.N)));
        menu.Items.Add(Item(Strings.Menu_Open, viewModel.OpenCommand, Command(Key.O)));
        menu.Items.Add(SubMenu(Strings.Menu_OpenRecent, BuildRecentMenu(viewModel)));
        menu.Items.Add(Separate());
        menu.Items.Add(Item(Strings.Menu_Save, viewModel.SaveCommand, Command(Key.S)));
        menu.Items.Add(Item(Strings.Menu_SaveAs, viewModel.SaveAsCommand, Command(Key.S, KeyModifiers.Shift)));
        menu.Items.Add(Item(Strings.Menu_SaveAll, viewModel.SaveAllCommand, Command(Key.S, KeyModifiers.Alt)));
        menu.Items.Add(Separate());
        menu.Items.Add(Item(Strings.Menu_Reload, viewModel.ReloadCommand));
        menu.Items.Add(Item(Strings.Menu_OpenFolder, viewModel.OpenContainingFolderCommand));
        menu.Items.Add(Item(Strings.Menu_CopyPath, viewModel.CopyFilePathCommand));
        menu.Items.Add(Separate());
        menu.Items.Add(Item(Strings.Menu_CloseTab, viewModel.CloseTabCommand, Command(Key.W)));
        menu.Items.Add(Item(Strings.Menu_CloseAllTabs, viewModel.CloseAllCommand, Command(Key.W, KeyModifiers.Shift)));
        return menu;
    }

    private static NativeMenu BuildEditMenu(MainWindowViewModel viewModel)
    {
        var menu = new NativeMenu();

        // Editing commands intentionally have no Gesture here: macOS must route
        // standard Cmd+C/V/X/Z to the focused control (including text boxes in
        // dialogs), not unconditionally to the main editor.
        menu.Items.Add(Item(Strings.Menu_Undo, viewModel.UndoCommand));
        menu.Items.Add(Item(Strings.Menu_Redo, viewModel.RedoCommand));
        menu.Items.Add(Separate());
        menu.Items.Add(Item(Strings.Menu_Cut, viewModel.CutCommand));
        menu.Items.Add(Item(Strings.Menu_Copy, viewModel.CopyCommand));
        menu.Items.Add(Item(Strings.Menu_Paste, viewModel.PasteCommand));
        menu.Items.Add(Item(Strings.Menu_Delete, viewModel.DeleteCommand));
        menu.Items.Add(Item(Strings.Menu_SelectAll, viewModel.SelectAllCommand));
        menu.Items.Add(Separate());
        menu.Items.Add(Item(Strings.Menu_DuplicateLine, viewModel.DuplicateLineCommand, Command(Key.D)));
        menu.Items.Add(Item(Strings.Menu_DeleteLine, viewModel.DeleteLineCommand, Command(Key.L)));
        menu.Items.Add(Item(Strings.Menu_ToggleComment, viewModel.ToggleCommentCommand, Command(Key.C, KeyModifiers.Alt)));
        menu.Items.Add(Item(Strings.Menu_InsertDateTime, viewModel.InsertDateTimeCommand, new KeyGesture(Key.F5)));
        menu.Items.Add(new NativeMenuItem { Header = Strings.Menu_FormatDocument, Command = viewModel.FormatDocumentCommand });
        return menu;
    }

    private static NativeMenu BuildSearchMenu(MainWindowViewModel viewModel)
    {
        var menu = new NativeMenu();
        menu.Items.Add(Item(Strings.Menu_Find, viewModel.OpenFindCommand, Command(Key.F)));
        menu.Items.Add(Item(Strings.Menu_Replace, viewModel.OpenReplaceCommand, Command(Key.F, KeyModifiers.Alt)));
        menu.Items.Add(Item(Strings.Menu_FindNext, viewModel.FindNextCommand, new KeyGesture(Key.F3)));
        menu.Items.Add(Item(Strings.Menu_FindPrevious, viewModel.FindPreviousCommand, new KeyGesture(Key.F3, KeyModifiers.Shift)));
        menu.Items.Add(Item(Strings.Menu_FindAll, viewModel.FindAllCommand));
        menu.Items.Add(Separate());
        menu.Items.Add(Item(Strings.Menu_FindInFiles, viewModel.FindInFilesCommand, Command(Key.F, KeyModifiers.Shift)));
        menu.Items.Add(Item(Strings.Menu_GoToLine, viewModel.GoToLineCommand, Command(Key.G)));
        return menu;
    }

    private static NativeMenu BuildEncodingMenu(MainWindowViewModel viewModel)
    {
        var menu = new NativeMenu();
        menu.Items.Add(SubMenu(Strings.Menu_ReopenEncoding, BuildReopenEncodingMenu(viewModel)));
        menu.Items.Add(SubMenu(Strings.Menu_SaveEncoding, BuildSaveEncodingMenu(viewModel)));
        menu.Items.Add(Separate());
        menu.Items.Add(Item(Strings.Menu_Reload, viewModel.ReloadCommand));
        return menu;
    }

    private static NativeMenu BuildReopenEncodingMenu(MainWindowViewModel viewModel)
    {
        var menu = new NativeMenu();

        void Populate()
        {
            menu.Items.Clear();
            menu.Items.Add(Item(Strings.NativeMenu_DetectEncoding, viewModel.ReopenWithDetectedEncodingCommand));
            menu.Items.Add(Separate());

            foreach (var option in viewModel.EncodingOptions)
            {
                menu.Items.Add(Item(option.Name, viewModel.ReopenWithEncodingCommand, parameter: option));
            }
        }

        menu.NeedsUpdate += (_, _) => Populate();
        Populate();
        return menu;
    }

    private static NativeMenu BuildSaveEncodingMenu(MainWindowViewModel viewModel)
    {
        var menu = new NativeMenu();

        void Populate()
        {
            menu.Items.Clear();
            foreach (var option in viewModel.EncodingOptions)
            {
                menu.Items.Add(new NativeMenuItem
                {
                    Header = option.Name,
                    ToggleType = MenuItemToggleType.Radio,
                    IsChecked = viewModel.ActiveDocument?.Encoding.Kind == option.Kind,
                    Command = viewModel.SaveWithEncodingCommand,
                    CommandParameter = option
                });
            }
        }

        menu.NeedsUpdate += (_, _) => Populate();
        Populate();
        return menu;
    }

    private static NativeMenu BuildViewMenu(MainWindowViewModel viewModel)
    {
        var menu = new NativeMenu();
        var toggles = new List<(NativeMenuItem Item, Func<bool> Get)>();

        NativeMenuItem Toggle(string header, Func<bool> get, Action<bool> set)
        {
            var item = new NativeMenuItem
            {
                Header = header,
                ToggleType = MenuItemToggleType.CheckBox,
                IsChecked = get()
            };

            item.Click += (_, _) => set(!get());
            toggles.Add((item, get));
            return item;
        }

        var wrap = new NativeMenuItem { Header = Strings.Menu_WordWrap, Command = viewModel.ToggleWordWrapCommand };
        menu.Items.Add(wrap);
        menu.Items.Add(Toggle(Strings.Menu_LineNumbers, () => viewModel.EditorSettings.ShowLineNumbers, value => viewModel.EditorSettings.ShowLineNumbers = value));
        menu.Items.Add(Toggle(Strings.Menu_Whitespace, () => viewModel.EditorSettings.ShowWhitespace, value => viewModel.EditorSettings.ShowWhitespace = value));
        menu.Items.Add(Toggle(Strings.Menu_EndOfLine, () => viewModel.EditorSettings.ShowEndOfLine, value => viewModel.EditorSettings.ShowEndOfLine = value));
        menu.Items.Add(Toggle(Strings.Menu_HighlightLine, () => viewModel.EditorSettings.HighlightCurrentLine, value => viewModel.EditorSettings.HighlightCurrentLine = value));
        menu.Items.Add(Toggle(Strings.Menu_ColumnRuler, () => viewModel.EditorSettings.EnableColumnRuler, value => viewModel.EditorSettings.EnableColumnRuler = value));
        menu.Items.Add(Separate());
        menu.Items.Add(SubMenu(Strings.Menu_Theme, BuildThemeMenu(viewModel)));
        menu.Items.Add(SubMenu(Strings.Menu_AccentColor, BuildAccentMenu(viewModel)));
        menu.Items.Add(Separate());
        menu.Items.Add(Item(Strings.Menu_ZoomIn, viewModel.ZoomInCommand, Command(Key.OemPlus)));
        menu.Items.Add(Item(Strings.Menu_ZoomOut, viewModel.ZoomOutCommand, Command(Key.OemMinus)));
        menu.Items.Add(Item(Strings.Menu_ResetZoom, viewModel.ZoomResetCommand, Command(Key.D0)));
        menu.Items.Add(Separate());
        menu.Items.Add(Item(Strings.Menu_NextTab, viewModel.NextDocumentCommand, new KeyGesture(Key.Tab, KeyModifiers.Control)));
        menu.Items.Add(Item(Strings.Menu_PreviousTab, viewModel.PreviousDocumentCommand, new KeyGesture(Key.Tab, KeyModifiers.Control | KeyModifiers.Shift)));

        menu.NeedsUpdate += (_, _) =>
        {
            foreach (var (item, get) in toggles)
            {
                item.IsChecked = get();
            }
        };

        return menu;
    }

    private static NativeMenu BuildThemeMenu(MainWindowViewModel viewModel)
    {
        var menu = new NativeMenu();

        void Populate()
        {
            menu.Items.Clear();
            foreach (var theme in viewModel.Themes)
            {
                menu.Items.Add(new NativeMenuItem
                {
                    Header = theme.DisplayName,
                    ToggleType = MenuItemToggleType.Radio,
                    IsChecked = string.Equals(theme.Name.ToString(), viewModel.Settings.Theme, StringComparison.OrdinalIgnoreCase),
                    Command = viewModel.SetThemeCommand,
                    CommandParameter = theme
                });
            }
        }

        menu.NeedsUpdate += (_, _) => Populate();
        Populate();
        return menu;
    }

    private static NativeMenu BuildAccentMenu(MainWindowViewModel viewModel)
    {
        var menu = new NativeMenu();

        void Populate()
        {
            menu.Items.Clear();
            foreach (var option in viewModel.AccentPalette)
            {
                menu.Items.Add(new NativeMenuItem
                {
                    Header = option.Name,
                    ToggleType = MenuItemToggleType.Radio,
                    IsChecked = string.Equals(option.Hex, viewModel.Settings.AccentColor, StringComparison.OrdinalIgnoreCase),
                    Command = viewModel.SetAccentColorCommand,
                    CommandParameter = option.Hex
                });
            }

            menu.Items.Add(new NativeMenuItemSeparator());
            menu.Items.Add(Item(Strings.NativeMenu_ResetAccent, viewModel.SetAccentColorCommand, parameter: ThemeService.DefaultAccentColor));
        }

        menu.NeedsUpdate += (_, _) => Populate();
        Populate();
        return menu;
    }

    private static NativeMenu BuildRecentMenu(MainWindowViewModel viewModel)    {
        var menu = new NativeMenu();

        void Populate()
        {
            menu.Items.Clear();

            if (viewModel.RecentFiles.Count == 0)
            {
                menu.Items.Add(new NativeMenuItem { Header = Strings.Tool_NoRecentFiles });
                return;
            }

            foreach (var recent in viewModel.RecentFiles)
            {
                menu.Items.Add(Item(recent.DisplayName, viewModel.OpenRecentCommand, parameter: recent.Path));
            }
        }

        menu.NeedsUpdate += (_, _) => Populate();
        Populate();
        return menu;
    }

    private static NativeMenu BuildHelpMenu(MainWindowViewModel viewModel)
    {
        var menu = new NativeMenu();
        menu.Items.Add(Item(Strings.Menu_About, viewModel.ShowAboutCommand));
        return menu;
    }

    private static NativeMenuItem Item(
        string header,
        ICommand command,
        KeyGesture? gesture = null,
        object? parameter = null)
    {
        var item = new NativeMenuItem
        {
            Header = header,
            Command = command,
            CommandParameter = parameter
        };

        if (gesture is not null)
        {
            item.Gesture = gesture;
        }

        return item;
    }

    private static NativeMenuItem SubMenu(string header, NativeMenu menu) => new()
    {
        Header = header,
        Menu = menu
    };

    private static NativeMenuItemSeparator Separate() => new();

    private static KeyGesture Command(Key key, KeyModifiers modifiers = KeyModifiers.None) =>
        new(key, KeyModifiers.Meta | modifiers);
}
