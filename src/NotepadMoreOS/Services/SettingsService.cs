using System.Text.Json;
using NotepadMoreOS.Models;

namespace NotepadMoreOS.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly object _gate = new();
    private readonly string _settingsFile;

    public SettingsService() : this(null)
    {
    }

    public SettingsService(string? settingsFile)
    {
        _settingsFile = settingsFile ?? AppPaths.SettingsFile;
    }

    public string SettingsFile => _settingsFile;

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsFile))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(_settingsFile);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        lock (_gate)
        {
            try
            {
                var directory = Path.GetDirectoryName(_settingsFile);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(settings, JsonOptions);
                var temporaryPath = _settingsFile + ".tmp";
                File.WriteAllText(temporaryPath, json);
                File.Move(temporaryPath, _settingsFile, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Preferences are best-effort; never take down the editor because settings could not be persisted.
                System.Diagnostics.Debug.WriteLine($"Could not save settings: {ex.Message}");
            }
        }
    }
}
