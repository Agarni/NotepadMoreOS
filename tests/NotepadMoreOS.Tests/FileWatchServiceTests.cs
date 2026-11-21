using NotepadMoreOS.Services;

namespace NotepadMoreOS.Tests;

public sealed class FileWatchServiceTests
{
    [Fact]
    public async Task Track_RaisesChangeEvent()
    {
        var directory = TestEnvironment.CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "watched.txt");
            await File.WriteAllTextAsync(path, "one");

            using var watcher = new FileWatchService(TimeSpan.FromMilliseconds(80));
            var changed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            watcher.FileChanged += (_, changedPath) => changed.TrySetResult(changedPath);
            watcher.Track(path);

            await File.AppendAllTextAsync(path, "two");

            var completed = await Task.WhenAny(changed.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.Same(changed.Task, completed);
            Assert.Equal(Path.GetFullPath(path), await changed.Task);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Suppress_IgnoresChangesDuringWindow()
    {
        var directory = TestEnvironment.CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "watched.txt");
            await File.WriteAllTextAsync(path, "one");

            using var watcher = new FileWatchService(TimeSpan.FromMilliseconds(50));
            var changed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            watcher.FileChanged += (_, _) => changed.TrySetResult(true);
            watcher.Track(path);
            watcher.Suppress(path, TimeSpan.FromSeconds(2));

            await File.AppendAllTextAsync(path, "two");
            await Task.Delay(TimeSpan.FromMilliseconds(500));

            Assert.False(changed.Task.IsCompleted);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Untrack_StopsEvents()
    {
        var directory = TestEnvironment.CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "watched.txt");
            await File.WriteAllTextAsync(path, "one");

            using var watcher = new FileWatchService(TimeSpan.FromMilliseconds(50));
            var changed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            watcher.FileChanged += (_, _) => changed.TrySetResult(true);
            watcher.Track(path);
            watcher.Untrack(path);

            await File.AppendAllTextAsync(path, "two");
            await Task.Delay(TimeSpan.FromMilliseconds(500));

            Assert.False(changed.Task.IsCompleted);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
