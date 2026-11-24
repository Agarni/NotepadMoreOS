using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Indentation;
using AvaloniaEdit.Indentation.CSharp;
using AvaloniaEdit.TextMate;
using NotepadMoreOS.Services;
using NotepadMoreOS.ViewModels;
using TextMateInstallation = AvaloniaEdit.TextMate.TextMate.Installation;

namespace NotepadMoreOS.Views;

public partial class DocumentView : UserControl
{
    private TextEditor? _editor;
    private DocumentViewModel? _viewModel;
    private TextMateInstallation? _textMate;
    private bool _attached;

    public DocumentView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        // Avalonia may reuse a DocumentView when switching between documents of
        // the same type. Detach the previous document and install the new one;
        // otherwise the editor keeps showing the old text.
        Detach();
        base.OnDataContextChanged(e);
        Attach();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Attach();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Detach();
    }

    private void Attach()
    {
        if (_attached || DataContext is not DocumentViewModel viewModel)
        {
            return;
        }

        var editor = this.FindControl<TextEditor>("Editor");
        if (editor is null)
        {
            return;
        }

        _attached = true;
        _editor = editor;
        _viewModel = viewModel;
        UpdatePreviewLayout();
        editor.Document = viewModel.Document;
        _viewModel.Host = new EditorHost(editor);

        // NotepadMoreOS has its own find bar; disable AvaloniaEdit's built-in search commands.
        try
        {
            editor.SearchPanel.Uninstall();
        }
        catch
        {
            // The search panel may not have been installed yet.
        }

        editor.TextArea.Background = Brushes.Transparent;
        editor.TextArea.SelectionCornerRadius = 2;
        ApplyIndentation();
        editor.TextArea.Caret.PositionChanged += OnCaretChanged;
        editor.TextArea.SelectionChanged += OnSelectionChanged;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        viewModel.ThemeService.ThemeChanged += OnThemeChanged;

        try
        {
            _textMate = editor.InstallTextMate(viewModel.ThemeService.RegistryOptions);
            _textMate.AppliedTheme += OnAppliedTheme;
            ApplyGrammar();
            ApplyTheme();
        }
        catch
        {
            // Syntax highlighting is optional; the editor still works without it.
        }

        var caret = Math.Clamp(viewModel.SavedCaretOffset, 0, editor.Document?.TextLength ?? 0);
        Dispatcher.UIThread.Post(() =>
        {
            if (_editor is null)
            {
                return;
            }

            _editor.CaretOffset = Math.Clamp(caret, 0, _editor.Document?.TextLength ?? 0);
            _editor.TextArea.Caret.BringCaretToView();

            if (_viewModel?.Host is { } host)
            {
                _viewModel.ApplyPendingSelection(host);
            }

            _editor.Focus();
        }, DispatcherPriority.Loaded);
    }

    private void Detach()
    {
        if (!_attached)
        {
            return;
        }

        if (_editor is not null)
        {
            _editor.TextArea.Caret.PositionChanged -= OnCaretChanged;
            _editor.TextArea.SelectionChanged -= OnSelectionChanged;
            _viewModel!.SavedCaretOffset = _editor.CaretOffset;
            _viewModel.Host = null;
        }

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.ThemeService.ThemeChanged -= OnThemeChanged;
        }

        if (_textMate is not null)
        {
            _textMate.AppliedTheme -= OnAppliedTheme;
            _textMate.Dispose();
            _textMate = null;
        }

        _viewModel = null;
        _editor = null;
        _attached = false;
    }

    private void ApplyGrammar()
    {
        if (_textMate is null || _viewModel is null)
        {
            return;
        }

        try
        {
            _textMate.SetGrammar(string.IsNullOrWhiteSpace(_viewModel.LanguageScope) ? null! : _viewModel.LanguageScope);
        }
        catch
        {
            // Ignore grammars that cannot be loaded.
        }
    }

    private void ApplyIndentation()
    {
        if (_editor is null || _viewModel is null)
        {
            return;
        }

        _editor.TextArea.IndentationStrategy = _viewModel.Language.Id switch
        {
            "csharp" => new CSharpIndentationStrategy(_editor.Options),
            _ => new DefaultIndentationStrategy()
        };
    }

    private void ApplyTheme()
    {
        if (_viewModel is null)
        {
            return;
        }

        var theme = _viewModel.ThemeService.Current;
        var rawTheme = _viewModel.ThemeService.LoadTheme(theme);
        if (rawTheme is not null && _textMate is not null)
        {
            try
            {
                _textMate.SetTheme(rawTheme);
            }
            catch
            {
                // Keep the previous theme if this one cannot be applied.
            }
        }

        ApplyFallbackColors(theme.IsDark);
    }

    private void ApplyFallbackColors(bool isDark)
    {
        if (_editor is null)
        {
            return;
        }

        var background = isDark ? Color.Parse("#1E1F23") : Colors.White;
        var foreground = isDark ? Color.Parse("#D4D4D4") : Color.Parse("#1E1E1E");
        var selection = TryGetResource("EditorSelectionBrush", out var selectionResource) && selectionResource is ISolidColorBrush selectionBrush
            ? selectionBrush.Color
            : isDark ? Color.Parse("#3C5B4A") : Color.Parse("#CBE8D8");
        var muted = isDark ? Color.Parse("#858585") : Color.Parse("#6E7681");
        var lineHighlight = isDark ? Color.FromArgb(18, 255, 255, 255) : Color.FromArgb(13, 0, 0, 0);

        _editor.Background = new SolidColorBrush(background);
        _editor.Foreground = new SolidColorBrush(foreground);
        _editor.LineNumbersForeground = new SolidColorBrush(muted);
        _editor.TextArea.SelectionBrush = new SolidColorBrush(selection);
        _editor.TextArea.TextView.CurrentLineBackground = new SolidColorBrush(lineHighlight);
        _editor.TextArea.TextView.CurrentLineBorder = null;
        _editor.TextArea.Caret.CaretBrush = new SolidColorBrush(foreground);
    }

    private void OnAppliedTheme(object? sender, TextMateInstallation installation)
    {
        if (_editor is null)
        {
            return;
        }

        if (TryGetColor(installation, "editor.background", out var background))
        {
            _editor.Background = new SolidColorBrush(background);
        }

        if (TryGetColor(installation, "editor.foreground", out var foreground))
        {
            _editor.Foreground = new SolidColorBrush(foreground);
            _editor.TextArea.Caret.CaretBrush = new SolidColorBrush(foreground);
        }

        if (TryGetColor(installation, "editor.selectionBackground", out var selection))
        {
            _editor.TextArea.SelectionBrush = new SolidColorBrush(selection);
        }
        else if (TryGetResource("EditorSelectionBrush", out var selectionBrush) && selectionBrush is IBrush brush)
        {
            _editor.TextArea.SelectionBrush = brush;
        }

        if (TryGetColor(installation, "editor.lineHighlightBackground", out var lineHighlight))
        {
            _editor.TextArea.TextView.CurrentLineBackground = new SolidColorBrush(lineHighlight);
        }
        else if (TryGetResource("EditorLineHighlightBrush", out var lineBrush) && lineBrush is IBrush lineHighlightBrush)
        {
            _editor.TextArea.TextView.CurrentLineBackground = lineHighlightBrush;
        }

        if (TryGetColor(installation, "editorLineNumber.foreground", out var lineNumber))
        {
            _editor.LineNumbersForeground = new SolidColorBrush(lineNumber);
        }

        _editor.TextArea.TextView.CurrentLineBorder = null;
    }

    private static bool TryGetColor(TextMateInstallation installation, string key, out Color color)
    {
        color = default;
        return installation.TryGetThemeColor(key, out var value) &&
               Color.TryParse(value, out color);
    }

    private static bool TryGetResource(string key, out object? value)
    {
        value = null;
        return Application.Current?.TryGetResource(key, out value) == true;
    }

    private void OnCaretChanged(object? sender, EventArgs e)
    {
        if (_editor is null || _viewModel is null)
        {
            return;
        }

        _viewModel.SavedCaretOffset = _editor.CaretOffset;
        _viewModel.UpdateCaret(_editor.TextArea.Caret.Line, _editor.TextArea.Caret.Column, _editor.SelectionLength);
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        if (_editor is null || _viewModel is null)
        {
            return;
        }

        _viewModel.UpdateCaret(_editor.TextArea.Caret.Line, _editor.TextArea.Caret.Column, _editor.SelectionLength);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DocumentViewModel.ShowMarkdownPreview))
        {
            UpdatePreviewLayout();
        }
        else if (e.PropertyName == nameof(DocumentViewModel.LanguageScope))
        {
            ApplyGrammar();
            ApplyIndentation();
        }
        else if (e.PropertyName == nameof(DocumentViewModel.Document) && _editor is not null && _viewModel is not null)
        {
            _editor.Document = _viewModel.Document;
        }
    }

    private void UpdatePreviewLayout()
    {
        var rows = EditorLayout.RowDefinitions;
        rows[0].Height = new GridLength(1, GridUnitType.Star);
        rows[2].Height = _viewModel?.ShowMarkdownPreview == true
            ? new GridLength(1, GridUnitType.Star)
            : new GridLength(0);
    }

    private void OnThemeChanged(object? sender, EventArgs e) => ApplyTheme();

    private void OnUndoClick(object? sender, RoutedEventArgs e) => _viewModel?.Host?.Undo();
    private void OnRedoClick(object? sender, RoutedEventArgs e) => _viewModel?.Host?.Redo();
    private void OnCutClick(object? sender, RoutedEventArgs e) => _viewModel?.Host?.Cut();
    private void OnCopyClick(object? sender, RoutedEventArgs e) => _viewModel?.Host?.Copy();
    private void OnPasteClick(object? sender, RoutedEventArgs e) => _viewModel?.Host?.Paste();
    private void OnDuplicateLineClick(object? sender, RoutedEventArgs e) => _viewModel?.Host?.DuplicateLine();
    private void OnDeleteLineClick(object? sender, RoutedEventArgs e) => _viewModel?.Host?.DeleteLine();
    private void OnSelectAllClick(object? sender, RoutedEventArgs e) => _viewModel?.Host?.SelectAll();

    private void OnToggleCommentClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel?.CommentPrefix is { } prefix)
        {
            _viewModel.Host?.ToggleComment(prefix);
        }
    }
}
