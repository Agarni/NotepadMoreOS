using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NotepadMoreOS.Models;

namespace NotepadMoreOS.Services;

/// <summary>
/// Persists a crash-recovery snapshot of the workspace. Content for dirty and
/// untitled documents is written to a dedicated recovery folder, while metadata
/// is kept in a small JSON file.
/// </summary>
public sealed class SessionService : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _directory;
    private readonly string _statePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, string> _contentHashes = new(StringComparer.Ordinal);
    private bool _disposed;

    public SessionService() : this(null)
    {
    }

    public SessionService(string? recoveryDirectory)
    {
        _directory = recoveryDirectory ?? AppPaths.RecoveryDirectory;
        _statePath = Path.Combine(_directory, "recovery.json");
    }

    public string DirectoryPath => _directory;

    public bool HasRecovery => File.Exists(_statePath);

    public async Task SaveAsync(RecoverySnapshot snapshot, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_directory);

            var keepFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var document in snapshot.Documents)
            {
                if (string.IsNullOrEmpty(document.Id))
                {
                    document.Id = Guid.NewGuid().ToString("N");
                }

                if (document.Content is null)
                {
                    document.BackupFile = null;
                    continue;
                }

                var contentFile = document.Id + ".txt";
                var contentPath = Path.Combine(_directory, contentFile);
                keepFiles.Add(contentFile);

                var hash = ComputeHash(document.Content);
                if (!_contentHashes.TryGetValue(document.Id, out var previousHash) ||
                    previousHash != hash ||
                    !File.Exists(contentPath))
                {
                    await File.WriteAllTextAsync(contentPath, document.Content, new UTF8Encoding(false), cancellationToken)
                        .ConfigureAwait(false);
                    _contentHashes[document.Id] = hash;
                }

                document.BackupFile = contentFile;
            }

            foreach (var file in Directory.EnumerateFiles(_directory, "*.txt"))
            {
                if (!keepFiles.Contains(Path.GetFileName(file)))
                {
                    TryDelete(file);
                }
            }

            snapshot.SavedAtUtc = DateTime.UtcNow;
            var json = JsonSerializer.Serialize(snapshot, JsonOptions);
            var temporaryPath = _statePath + ".tmp";
            await File.WriteAllTextAsync(temporaryPath, json, cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, _statePath, overwrite: true);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<RecoverySnapshot?> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_statePath))
            {
                return null;
            }

            RecoverySnapshot? snapshot;
            try
            {
                var json = await File.ReadAllTextAsync(_statePath, cancellationToken).ConfigureAwait(false);
                snapshot = JsonSerializer.Deserialize<RecoverySnapshot>(json, JsonOptions);
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                return null;
            }

            if (snapshot is null)
            {
                return null;
            }

            foreach (var document in snapshot.Documents)
            {
                if (string.IsNullOrWhiteSpace(document.BackupFile))
                {
                    continue;
                }

                var contentPath = Path.Combine(_directory, document.BackupFile);
                try
                {
                    document.Content = await File.ReadAllTextAsync(contentPath, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    document.Content = null;
                }
            }

            return snapshot;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ClearCore();
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Clear() => ClearAsync().GetAwaiter().GetResult();

    private void ClearCore()
    {
        _contentHashes.Clear();

        TryDelete(_statePath);

        try
        {
            if (!Directory.Exists(_directory))
            {
                return;
            }

            foreach (var file in Directory.EnumerateFiles(_directory, "*.txt"))
            {
                TryDelete(file);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Recovery cleanup is best-effort.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _gate.Dispose();
    }

    private static string ComputeHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort.
        }
    }
}
