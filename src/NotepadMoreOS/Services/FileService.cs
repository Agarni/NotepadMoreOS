using System.Runtime.InteropServices;
using System.Text;
using NotepadMoreOS.Models;

namespace NotepadMoreOS.Services;

public sealed record LoadedFile(string Text, TextEncodingKind Encoding, LineEnding LineEnding);

/// <summary>Reading and writing text files with encoding and line-ending awareness.</summary>
public sealed class FileService
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    static FileService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public async Task<LoadedFile> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        var (text, encoding) = Decode(bytes);
        return new LoadedFile(text, encoding, LineEndingCatalog.Detect(text));
    }

    /// <summary>Reads a file using a specific encoding, bypassing auto-detection.</summary>
    public async Task<LoadedFile> LoadAsync(
        string path,
        TextEncodingKind encoding,
        CancellationToken cancellationToken = default)
    {
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        var text = Decode(bytes, encoding);
        return new LoadedFile(text, encoding, LineEndingCatalog.Detect(text));
    }

    public async Task SaveAsync(
        string path,
        string text,
        TextEncodingKind encoding,
        LineEnding lineEnding,
        CancellationToken cancellationToken = default)
    {
        var normalized = LineEndingCatalog.Normalize(text, lineEnding);
        var bytes = Encode(normalized, encoding);

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Write next to the target and swap atomically, so a failed save never truncates the file.
        var temporaryPath = path + ".nmos-" + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, bytes, cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                try { File.Delete(temporaryPath); } catch { /* best effort */ }
            }
        }
    }

    /// <summary>
    /// Moves a file to the platform trash. On macOS/Linux this is implemented
    /// directly; on Windows it uses the shell recycle bin.
    /// </summary>
    public Task MoveToTrashAsync(string path, string? trashDirectoryOverride = null)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The file does not exist.", path);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && trashDirectoryOverride is null)
        {
            RecycleWindows(path);
            return Task.CompletedTask;
        }

        var trashDirectory = trashDirectoryOverride ?? ResolveTrashDirectory();
        Directory.CreateDirectory(trashDirectory);

        var fileName = Path.GetFileName(path);
        var target = MakeUniquePath(trashDirectory, fileName);

        try
        {
            File.Move(path, target);
        }
        catch (IOException)
        {
            // Different volume: copy and remove the source.
            File.Copy(path, target);
            File.Delete(path);
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && trashDirectoryOverride is null)
        {
            WriteLinuxTrashInfo(target, path);
        }

        return Task.CompletedTask;
    }

    private static string ResolveTrashDirectory()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(home))
            {
                home = Environment.GetEnvironmentVariable("HOME") ?? Directory.GetCurrentDirectory();
            }

            return Path.Combine(home, ".Trash");
        }

        var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (string.IsNullOrWhiteSpace(dataHome))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(home))
            {
                home = Environment.GetEnvironmentVariable("HOME") ?? Directory.GetCurrentDirectory();
            }

            dataHome = Path.Combine(home, ".local", "share");
        }

        return Path.Combine(dataHome, "Trash", "files");
    }

    private static string MakeUniquePath(string directory, string fileName)
    {
        var candidate = Path.Combine(directory, fileName);
        if (!File.Exists(candidate))
        {
            return candidate;
        }

        var name = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        for (var i = 1; i < 10_000; i++)
        {
            candidate = Path.Combine(directory, $"{name} ({i}){extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("Could not find a free name in the trash directory.");
    }

    private static void WriteLinuxTrashInfo(string trashedPath, string originalPath)
    {
        try
        {
            var filesDirectory = Path.GetDirectoryName(trashedPath)!;
            var infoDirectory = Path.Combine(Path.GetDirectoryName(filesDirectory)!, "info");
            Directory.CreateDirectory(infoDirectory);
            var infoPath = Path.Combine(
                infoDirectory,
                Path.GetFileName(trashedPath) + ".trashinfo");

            var content = "[Trash Info]\n" +
                          "Path=" + Uri.EscapeDataString(originalPath) + "\n" +
                          "DeletionDate=" + DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss") + "\n";
            File.WriteAllText(infoPath, content);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: the file is already in the trash folder.
        }
    }

    private static void RecycleWindows(string path)
    {
        var operation = new ShFileOpStruct
        {
            wFunc = 3, // FO_DELETE
            pFrom = path + "\0\0",
            fFlags = 0x0040 | 0x0010 | 0x0004 // ALLOWUNDO | NOCONFIRMATION | SILENT
        };

        if (SHFileOperation(ref operation) != 0)
        {
            throw new IOException("Windows could not move the file to the Recycle Bin.");
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileOpStruct
    {
        public IntPtr hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPWStr)] public string pFrom;
        [MarshalAs(UnmanagedType.LPWStr)] public string? pTo;
        public ushort fFlags;
        [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref ShFileOpStruct lpFileOp);

    public static string NormalizeLineEndings(string text, LineEnding lineEnding) =>
        LineEndingCatalog.Normalize(text, lineEnding);

    public static (string Text, TextEncodingKind Encoding) Decode(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return (Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3), TextEncodingKind.Utf8Bom);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return (Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2), TextEncodingKind.Utf16Le);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return (Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2), TextEncodingKind.Utf16Be);
        }

        var bomlessUnicode = DetectBomlessUtf16(bytes);
        if (bomlessUnicode == TextEncodingKind.Utf16Le)
        {
            return (Encoding.Unicode.GetString(bytes), TextEncodingKind.Utf16Le);
        }

        if (bomlessUnicode == TextEncodingKind.Utf16Be)
        {
            return (Encoding.BigEndianUnicode.GetString(bytes), TextEncodingKind.Utf16Be);
        }

        try
        {
            return (StrictUtf8.GetString(bytes), TextEncodingKind.Utf8);
        }
        catch (DecoderFallbackException)
        {
            return (Encoding.GetEncoding(1252).GetString(bytes), TextEncodingKind.Windows1252);
        }
    }

    /// <summary>Decodes bytes with an explicit encoding, skipping a matching BOM when present.</summary>
    public static string Decode(byte[] bytes, TextEncodingKind encoding)
    {
        return encoding switch
        {
            TextEncodingKind.Utf8 => DecodeUtf8(bytes, skipBom: true),
            TextEncodingKind.Utf8Bom => DecodeUtf8(bytes, skipBom: true),
            TextEncodingKind.Utf16Le => Encoding.Unicode.GetString(
                bytes,
                HasPrefix(bytes, 0xFF, 0xFE) ? 2 : 0,
                bytes.Length - (HasPrefix(bytes, 0xFF, 0xFE) ? 2 : 0)),
            TextEncodingKind.Utf16Be => Encoding.BigEndianUnicode.GetString(
                bytes,
                HasPrefix(bytes, 0xFE, 0xFF) ? 2 : 0,
                bytes.Length - (HasPrefix(bytes, 0xFE, 0xFF) ? 2 : 0)),
            TextEncodingKind.Windows1252 => Encoding.GetEncoding(1252).GetString(bytes),
            TextEncodingKind.Iso88591 => Encoding.Latin1.GetString(bytes),
            _ => DecodeUtf8(bytes, skipBom: true)
        };
    }

    private static string DecodeUtf8(byte[] bytes, bool skipBom)
    {
        var offset = skipBom && HasPrefix(bytes, 0xEF, 0xBB, 0xBF) ? 3 : 0;
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false)
            .GetString(bytes, offset, bytes.Length - offset);
    }

    private static bool HasPrefix(byte[] bytes, params byte[] prefix)
    {
        if (bytes.Length < prefix.Length)
        {
            return false;
        }

        for (var i = 0; i < prefix.Length; i++)
        {
            if (bytes[i] != prefix[i])
            {
                return false;
            }
        }

        return true;
    }

    public static byte[] Encode(string text, TextEncodingKind encoding)
    {
        return encoding switch
        {
            TextEncodingKind.Utf8 => Utf8NoBom.GetBytes(text),
            TextEncodingKind.Utf8Bom => Combine(Encoding.UTF8.GetPreamble(), Utf8NoBom.GetBytes(text)),
            TextEncodingKind.Utf16Le => Combine(Encoding.Unicode.GetPreamble(), Encoding.Unicode.GetBytes(text)),
            TextEncodingKind.Utf16Be => Combine(Encoding.BigEndianUnicode.GetPreamble(), Encoding.BigEndianUnicode.GetBytes(text)),
            TextEncodingKind.Windows1252 => Encoding.GetEncoding(1252).GetBytes(text),
            TextEncodingKind.Iso88591 => Encoding.Latin1.GetBytes(text),
            _ => Utf8NoBom.GetBytes(text)
        };
    }

    /// <summary>
    /// A light heuristic used by Find in Files to avoid searching binary files.
    /// UTF-8 and UTF-16 files (with or without BOM) are considered text.
    /// </summary>
    public static bool IsProbablyBinary(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            return false;
        }

        if ((bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) ||
            (bytes.Length >= 2 && ((bytes[0] == 0xFF && bytes[1] == 0xFE) || (bytes[0] == 0xFE && bytes[1] == 0xFF))))
        {
            return false;
        }

        if (DetectBomlessUtf16(bytes) is not null)
        {
            return false;
        }

        var sample = Math.Min(bytes.Length, 8192);
        for (var i = 0; i < sample; i++)
        {
            if (bytes[i] == 0)
            {
                return true;
            }
        }

        return false;
    }

    private static byte[] Combine(byte[] first, byte[] second)
    {
        var result = new byte[first.Length + second.Length];
        Buffer.BlockCopy(first, 0, result, 0, first.Length);
        Buffer.BlockCopy(second, 0, result, first.Length, second.Length);
        return result;
    }

    private static TextEncodingKind? DetectBomlessUtf16(byte[] bytes)
    {
        if (bytes.Length < 4)
        {
            return null;
        }

        var sample = Math.Min(bytes.Length, 4096);
        var zerosAtEven = 0;
        var zerosAtOdd = 0;

        for (var i = 0; i < sample; i++)
        {
            if (bytes[i] != 0)
            {
                continue;
            }

            if (i % 2 == 0)
            {
                zerosAtEven++;
            }
            else
            {
                zerosAtOdd++;
            }
        }

        if (zerosAtOdd > sample / 4 && zerosAtEven == 0)
        {
            return TextEncodingKind.Utf16Le;
        }

        if (zerosAtEven > sample / 4 && zerosAtOdd == 0)
        {
            return TextEncodingKind.Utf16Be;
        }

        return null;
    }
}
