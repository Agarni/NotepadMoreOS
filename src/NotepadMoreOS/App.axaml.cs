using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using NotepadMoreOS.Models;
using NotepadMoreOS.Services;
using NotepadMoreOS.ViewModels;
using NotepadMoreOS.Views;

namespace NotepadMoreOS;

public partial class App : Application
{
    public override void Initialize()
    {
        // The UI culture must be set before the XAML is loaded so resources resolve correctly.
        LocalizationService.Apply(new SettingsService().Load().Language);
        AvaloniaXamlLoader.Load(this);
        InitializeNativeApplicationMenu();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = new ServiceCollection();
            services.AddSingleton<SettingsService>();
            services.AddSingleton<FileService>();
            services.AddSingleton<LanguageService>();
            services.AddSingleton<ThemeService>();
            services.AddSingleton<SearchService>();
            services.AddSingleton<FileSearchService>();
            services.AddSingleton<SessionService>();
            services.AddSingleton<FileWatchService>();
            services.AddSingleton<DialogService>();
            services.AddSingleton<AppSettings>(provider => provider.GetRequiredService<SettingsService>().Load());
            services.AddSingleton<MainWindowViewModel>();

            var provider = services.BuildServiceProvider();

            var settings = provider.GetRequiredService<AppSettings>();
            var themeService = provider.GetRequiredService<ThemeService>();
            settings.AccentColor = ThemeService.NormalizeAccentColor(settings.AccentColor);
            themeService.Apply(themeService.Find(settings.Theme));
            themeService.ApplyAccentColor(settings.AccentColor);

            var viewModel = provider.GetRequiredService<MainWindowViewModel>();
            var dialogService = provider.GetRequiredService<DialogService>();

            var window = new MainWindow { DataContext = viewModel };
            dialogService.Attach(window);

            desktop.MainWindow = window;
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;

            desktop.ShutdownRequested += async (_, args) =>
            {
                if (viewModel.IsCloseApproved)
                {
                    return;
                }

                args.Cancel = true;

                try
                {
                    if (await viewModel.RequestCloseApplicationAsync())
                    {
                        desktop.Shutdown();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to shut down cleanly: {ex}");
                }
            };

            window.Opened += async (_, _) =>
            {
                try
                {
                    await viewModel.InitializeAsync(desktop.Args ?? Array.Empty<string>());
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to initialize the editor: {ex}");
                }
            };

            window.Closed += (_, _) => provider.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void InitializeNativeApplicationMenu()
    {
        if (!OperatingSystem.IsMacOS() || IsHeadless())
        {
            return;
        }

        var menu = new NativeMenu();

        var about = new NativeMenuItem { Header = "About NotepadMoreOS..." };
        about.Click += (_, _) => CurrentViewModel()?.ShowAboutCommand.Execute(null);
        menu.Items.Add(about);

        menu.Items.Add(new NativeMenuItemSeparator());

        var preferences = new NativeMenuItem
        {
            Header = "Preferences...",
            Gesture = new KeyGesture(Key.OemComma, KeyModifiers.Meta)
        };
        preferences.Click += (_, _) => CurrentViewModel()?.OpenSettingsCommand.Execute(null);
        menu.Items.Add(preferences);

        NativeMenu.SetMenu(this, menu);
    }

    private static MainWindowViewModel? CurrentViewModel() =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow?.DataContext
        as MainWindowViewModel;

    private static bool IsHeadless() =>
        AppDomain.CurrentDomain.GetAssemblies().Any(assembly =>
            string.Equals(assembly.GetName().Name, "Avalonia.Headless", StringComparison.Ordinal));
}
