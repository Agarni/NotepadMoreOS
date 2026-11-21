# AGENTS.md

Context for AI agents and developers working on **NotepadMoreOS**.

## Project

A cross-platform Notepad++-inspired text/code editor built with **Avalonia 12.1.6**, **.NET 10**, **AvaloniaEdit 12**, **TextMateSharp** and **CommunityToolkit.Mvvm**. Current version: **1.7.2** (About dialog links to this repository, macOS DMG, Windows Inno Setup installer and Linux deb/tarball, English and Brazilian Portuguese UI, arrow navigation for overflowing tabs, selectable tab styles without hover fill, extended find search with escapes, live find match count, theme previews, configurable accent color palette, modernized brand icon, named sessions with unsaved drafts and prompt-free switching, tab context menu, crash recovery, encoding reopen, native macOS menu + custom title bar, external change detection, modern UI).

- UI languages: English and Brazilian Portuguese, resolved from the persisted language preference or the system culture.
- Documentation language: Portuguese (with this file in English for agents).
- License: MIT.
- Repository root: this directory.

## Commands

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project src/NotepadMoreOS

# Visual QA; writes PNGs to docs/ and prints binding errors
dotnet run --project tools/NotepadMoreOS.UiSnapshot

# Publish (instaladores em artifacts/)
tools/release/build-installers.sh
```

Target framework is `net10.0`. Keep the build free of errors and warnings.

## Read first

1. `docs/ARQUITETURA.md` — architecture, data flow, decisions.
2. `docs/DESENVOLVIMENTO.md` — invariants, recipes, pitfalls.
3. `docs/GUIA-USUARIO.md` — behavior from the user's perspective.
4. `docs/ROADMAP.md` — what is intentionally not implemented.

## Architecture in 30 seconds

- `MainWindowViewModel` owns the workspace: open documents, recent files, session, commands and status proxies.
- `DocumentViewModel` owns a `TextDocument`, dirty state, encoding/EOL and caret state. Views never bind `TextEditor.Text`.
- `DocumentView` hosts the `TextEditor`, assigns `Document`, installs/disposes TextMate, applies theme/grammar/indentation and forwards caret/selection.
- Services: `FileService` (bytes/encoding/EOL/atomic save), `SearchService` (document search), `FileSearchService` (background folder search), `SettingsService`, `ThemeService`, `LanguageService`, `DialogService`.
- `IEditorHost`/`EditorHost` is the only imperative bridge from ViewModels to the editor.
- Everything is registered as a singleton in `App.axaml.cs`; documents are created manually per tab.

## Non-negotiable invariants

1. Do not bind `TextEditor.Text`. `TextDocument` is the model; assign it in `DocumentView` and handle `Document` replacement.
2. Dirty state = `!UndoStack.IsOriginalFile || Encoding.Kind != savedEncoding`. Call `MarkAsOriginalFile()` after load/save.
3. All file writes go through `FileService` (EOL normalization, encoding preamble, temp file + atomic move).
4. TextMate installation is per view and must be disposed on detach. Do not share it across documents.
5. Any control derived from an Avalonia control must override `StyleKeyOverride` (see `NotepadTextEditor`).
6. Use compiled bindings with `x:DataType`. Avoid reflection bindings.
7. Use `controls:Mod` for primary shortcuts; `Meta` on macOS, `Control` elsewhere. Do not bind reserved macOS shortcuts (Cmd+Q/H/Tab).
8. Business logic belongs in ViewModels/Services, not code-behind. Code-behind is for view wiring, pickers, focus and dialog closing.
9. File search runs off the UI thread, is cancellable, and respects the 10 MB / 5,000 result caps.
10. AvaloniaEdit `TextDocument` is thread-affine. Tests that touch it after an `await` must run their body through `AsyncTest.Run` (see `tests/NotepadMoreOS.Tests/AsyncTest.cs`).
11. Settings persistence is best-effort and debounced; never crash because settings could not be saved.
12. Keep UI strings in `Resources/Strings*.resx` (source: `tools/localization/strings.tsv`); never hardcode visible text. Documentation is Portuguese.
13. Use `LogToTrace`/the snapshot tool for binding diagnostics; do not add `Avalonia.Diagnostics`.
14. When `RecoveryEnabled` is on, a normal close saves the recovery snapshot, skips save prompts and **keeps** drafts; original files are untouched. When off, prompt and clear recovery on close.
15. Suppress `FileWatchService` around our own saves; otherwise the editor can reload itself or show false external-change prompts.
16. Native macOS menus are installed once, after `Loaded`; setting a second `NativeMenu` on the same window throws `The menu being updated does not match`. Clear `Window.KeyBindings` (and hidden in-window HotKeys) on real macOS to avoid double execution, re-add `Esc`, and keep the in-window menu for headless rendering.
17. On macOS the window uses `WindowDecorations.BorderOnly` + `ExtendClientAreaToDecorationsHint`, with custom traffic lights and drag handling in `MainWindow.axaml.cs`. Keep the traffic lights visible in headless (they are our controls) and hide the in-window menu there.
18. Tab context actions are routed through `DocumentViewModel.RequestActionCommand` → `DocumentActionEventArgs` → `MainWindowViewModel.HandleDocumentActionAsync`. Always operate on the document passed in the event, not `ActiveDocument`.
19. Named sessions store file paths and per-tab state plus drafts for untitled or dirty tabs under `session-drafts/`. Untitled drafts restore clean and Save writes back to `SessionDraftPath` without asking for a name; Save As exports. Never write draft content into `settings.json`, and never modify original files when saving a session. Switching sessions must snapshot the current session (or an automatic `Unsaved workspace` session) and close tabs without save or confirmation prompts.
20. Custom `Flyout` content does not close automatically when one of its action buttons is clicked. Close it explicitly in the `Click` handler (see `OnSessionFlyoutItemClick`), but defer the hide with `Dispatcher.UIThread.Post` — hiding synchronously can clear the command parameter before the command runs. `MenuFlyout`/`MenuItem` closes itself, but the parent flyout still needs handling. The snapshot tool verifies session selection from the flyout.

## Key files

| Area | File |
|---|---|
| Shell/layout/menus/shortcuts | `src/NotepadMoreOS/Views/MainWindow.axaml` |
| Editor hosting | `src/NotepadMoreOS/Views/DocumentView.axaml(.cs)` |
| Workspace commands/session | `src/NotepadMoreOS/ViewModels/MainWindowViewModel.cs` |
| Document model | `src/NotepadMoreOS/ViewModels/DocumentViewModel.cs` |
| In-file search | `src/NotepadMoreOS/ViewModels/FindReplaceViewModel.cs`, `Services/SearchService.cs` |
| Folder search | `src/NotepadMoreOS/Views/Dialogs/FindInFilesWindow.axaml`, `ViewModels/FindInFilesViewModel.cs`, `Services/FileSearchService.cs` |
| File/encoding | `src/NotepadMoreOS/Services/FileService.cs` |
| Settings/session | `Models/AppSettings.cs`, `Services/SettingsService.cs`, `Services/AppPaths.cs` |
| Crash recovery | `Services/SessionService.cs`, `Models/RecoveryModels.cs` |
| External changes | `Services/FileWatchService.cs`, `ViewModels/MainWindowViewModel.cs` |
| macOS native menu | `Views/MainWindow.NativeMenu.cs`, `App.axaml.cs` |
| Named sessions | `Models/NamedSession.cs`, `ViewModels/SessionItemViewModel.cs`, `ViewModels/MainWindowViewModel.cs` |
| Tab actions | `DocumentViewModel.RequestActionCommand`, `MainWindowViewModel.HandleDocumentActionAsync`, tab `ContextFlyout` |
| Themes | `Services/ThemeService.cs`, `Styles/Palette.axaml`, `Styles/AppStyles.axaml` |
| Application icon | `src/NotepadMoreOS/Assets/appicon.svg`, `.png`, `.ico` |
| Localization | `src/NotepadMoreOS/Resources/Strings*.resx`, `Services/LocalizationService.cs`, `tools/localization/` |
| Installers | `tools/release/setup.iss`, `build-macos.sh`, `build-windows.sh`, `.github/workflows/release.yml` |
| Brand display | `Views/WelcomeView.axaml`, `Views/Dialogs/AboutWindow.axaml`, `Views/MainWindow.axaml` |
| Tests | `tests/NotepadMoreOS.Tests/` |
| Screenshots | `tools/NotepadMoreOS.UiSnapshot/`, `docs/screenshot-*.png` |

## Verification checklist

Before finishing a change:

1. `dotnet build` — 0 errors, 0 warnings.
2. `dotnet test` — all tests pass (138 at the time of writing; count may grow).
3. `dotnet run --project tools/NotepadMoreOS.UiSnapshot` — exits 0; any `[Binding]` line fails the run.
4. If UI changed, inspect the regenerated screenshots.
5. If behavior changed, update `README.md`, `docs/GUIA-USUARIO.md`, `docs/ARQUITETURA.md`, `docs/DESENVOLVIMENTO.md` and `CHANGELOG.md` as appropriate.

## Known limitations (do not present as done)

Print, macros/plugins, function list/document map/folder explorer, file compare, custom find-in-files exclusions, localization, signed installers, large-file virtualization. See `docs/ROADMAP.md`.

## Current features

Named sessions, tabs, tab context menu, TextMate highlighting (63 languages), workspace bar used as a custom macOS title bar, language-colored badges, find/replace with regex and selection scope, find-all results, find in files (masks, subfolders, binary/encoding handling, cancellation), encoding/EOL conversion, reopen with encoding, external change detection, crash recovery of unsaved documents, atomic save, recent files, session restore, native macOS menu, modern light/dark themes, zoom/wrap, indentation, duplicate/delete line, toggle comment, go to line, insert date/time, drag & drop, status bar, preferences.
