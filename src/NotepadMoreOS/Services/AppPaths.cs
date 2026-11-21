using System.Runtime.InteropServices;

namespace NotepadMoreOS.Services;

public static class AppPaths
{
    public static string ConfigDirectory { get; } = ResolveConfigDirectory();

    public static string SettingsFile => Path.Combine(ConfigDirectory, "settings.json");

    public static string RecoveryDirectory => Path.Combine(ConfigDirectory, "recovery");

    public static void EnsureConfigDirectory() => Directory.CreateDirectory(ConfigDirectory);

    private static string ResolveConfigDirectory()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(home))
            {
                home = Environment.GetEnvironmentVariable("HOME") ?? Directory.GetCurrentDirectory();
            }

            return Path.Combine(home, "Library", "Application Support", "NotepadMoreOS");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "NotepadMoreOS");
        }

        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (!string.IsNullOrWhiteSpace(xdg))
        {
            return Path.Combine(xdg, "notepadmoreos");
        }

        var unixHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(unixHome))
        {
            unixHome = Environment.GetEnvironmentVariable("HOME") ?? Directory.GetCurrentDirectory();
        }

        return Path.Combine(unixHome, ".config", "notepadmoreos");
    }
}
