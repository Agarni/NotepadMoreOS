using System.Runtime.InteropServices;

namespace NotepadMoreOS.Services;

/// <summary>
/// Watches individual files for changes made by other programs. Events are
/// debounced and can be suppressed around the editor's own saves.
/// </summary>
public sealed class FileWatchService : IDisposable
{
    private static readonly TimeSpan DefaultDebounce = TimeSpan.FromMilliseconds(600);
    private static readonly TimeSpan DefaultSuppression = TimeSpan.FromSeconds(2);

    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _entries = new(PathComparer);
    private readonly Dictionary<string, DateTime> _suppressedUntil = new(PathComparer);
    private readonly TimeSpan _debounce;
    private bool _disposed;

    public FileWatchService() : this(null)
    {
    }

    public FileWatchService(TimeSpan? debounce)
    {
        _debounce = debounce ?? DefaultDebounce;
    }

    /// <summary>Raised on a background thread with the full path that changed.</summary>
    public event EventHandler<string>? FileChanged;

    public void Track(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        var fileName = Path.GetFileName(fullPath);

        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName) || !Directory.Exists(directory))
        {
            return;
        }

        lock (_gate)
        {
            if (_disposed || _entries.ContainsKey(fullPath))
            {
                return;
            }

            try
            {
                var watcher = new FileSystemWatcher(directory, fileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true
                };

                var entry = new Entry(watcher);
                entry.Timer = new Timer(
                    _ => OnDebounceElapsed(fullPath),
                    null,
                    Timeout.InfiniteTimeSpan,
                    Timeout.InfiniteTimeSpan);

                FileSystemEventHandler handler = (_, _) => Schedule(fullPath, entry);
                RenamedEventHandler renamedHandler = (_, _) => Schedule(fullPath, entry);

                watcher.Changed += handler;
                watcher.Created += handler;
                watcher.Deleted += handler;
                watcher.Renamed += renamedHandler;
                watcher.Error += (_, _) => { /* ignore watcher overflow/permission errors */ };

                entry.Handler = handler;
                entry.RenamedHandler = renamedHandler;
                _entries[fullPath] = entry;
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException or NotSupportedException)
            {
                // Watching is best-effort; the editor still works without it.
            }
        }
    }

    public void Untrack(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var fullPath = Path.GetFullPath(path);

        lock (_gate)
        {
            if (_entries.Remove(fullPath, out var entry))
            {
                entry.Dispose();
            }
        }
    }

    /// <summary>Ignores events for a path for a short period, used around our own saves.</summary>
    public void Suppress(string path, TimeSpan? duration = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var fullPath = Path.GetFullPath(path);

        lock (_gate)
        {
            _suppressedUntil[fullPath] = DateTime.UtcNow + (duration ?? DefaultSuppression);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (var entry in _entries.Values)
            {
                entry.Dispose();
            }

            _entries.Clear();
            _suppressedUntil.Clear();
        }
    }

    private void Schedule(string path, Entry entry)
    {
        lock (_gate)
        {
            if (_disposed || !_entries.ContainsKey(path))
            {
                return;
            }

            try
            {
                entry.Timer.Change(_debounce, Timeout.InfiniteTimeSpan);
            }
            catch (ObjectDisposedException)
            {
                // The watcher was removed concurrently.
            }
        }
    }

    private void OnDebounceElapsed(string path)
    {
        lock (_gate)
        {
            if (_disposed || !_entries.ContainsKey(path))
            {
                return;
            }

            if (_suppressedUntil.TryGetValue(path, out var until))
            {
                if (until > DateTime.UtcNow)
                {
                    return;
                }

                _suppressedUntil.Remove(path);
            }
        }

        FileChanged?.Invoke(this, path);
    }

    private static StringComparer PathComparer =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private sealed class Entry(FileSystemWatcher watcher) : IDisposable
    {
        public FileSystemWatcher Watcher { get; } = watcher;
        public Timer Timer { get; set; } = null!;
        public FileSystemEventHandler Handler { get; set; } = null!;
        public RenamedEventHandler RenamedHandler { get; set; } = null!;

        public void Dispose()
        {
            try
            {
                Watcher.EnableRaisingEvents = false;
                Watcher.Changed -= Handler;
                Watcher.Created -= Handler;
                Watcher.Deleted -= Handler;
                Watcher.Renamed -= RenamedHandler;
                Watcher.Dispose();
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException)
            {
                // Best effort.
            }

            Timer.Dispose();
        }
    }
}
