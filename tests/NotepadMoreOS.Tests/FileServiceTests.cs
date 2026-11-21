using System.Text;
using NotepadMoreOS.Models;
using NotepadMoreOS.Services;

namespace NotepadMoreOS.Tests;

public sealed class FileServiceTests
{
    [Fact]
    public void Decode_Utf8Bom_RemovesPreamble()
    {
        var bytes = FileService.Encode("olá", TextEncodingKind.Utf8Bom);
        var (text, encoding) = FileService.Decode(bytes);

        Assert.Equal("olá", text);
        Assert.Equal(TextEncodingKind.Utf8Bom, encoding);
    }

    [Theory]
    [InlineData(TextEncodingKind.Utf8, "Hello world")]
    [InlineData(TextEncodingKind.Utf8Bom, "olá")]
    [InlineData(TextEncodingKind.Utf16Le, "Hello world")]
    [InlineData(TextEncodingKind.Utf16Be, "Hello world")]
    [InlineData(TextEncodingKind.Windows1252, "café")]
    public void EncodeAndDecode_RoundTripsSupportedEncodings(TextEncodingKind encoding, string text)
    {
        var bytes = FileService.Encode(text, encoding);

        var (decoded, detected) = FileService.Decode(bytes);

        Assert.Equal(text, decoded);
        Assert.Equal(encoding, detected);
    }

    [Fact]
    public void Decode_FallsBackToWindows1252_ForInvalidUtf8()
    {
        var bytes = new byte[] { 0x63, 0x61, 0x66, 0xE9 };

        var (text, encoding) = FileService.Decode(bytes);

        Assert.Equal("café", text);
        Assert.Equal(TextEncodingKind.Windows1252, encoding);
    }

    [Fact]
    public void Decode_BomlessUtf16_IsDetected()
    {
        var bytes = Encoding.Unicode.GetBytes("hello");

        var (text, encoding) = FileService.Decode(bytes);

        Assert.Equal("hello", text);
        Assert.Equal(TextEncodingKind.Utf16Le, encoding);
    }

    [Theory]
    [InlineData("a\r\nb\r\nc", LineEnding.CrLf)]
    [InlineData("a\nb\nc", LineEnding.Lf)]
    [InlineData("a\rb\rc", LineEnding.Cr)]
    public void DetectLineEnding_ReturnsDominantStyle(string text, LineEnding expected)
    {
        Assert.Equal(expected, LineEndingCatalog.Detect(text));
    }

    [Fact]
    public void NormalizeLineEndings_ConvertsMixedStyles()
    {
        const string text = "a\r\nb\rc\nd";

        var unix = LineEndingCatalog.Normalize(text, LineEnding.Lf);
        var windows = LineEndingCatalog.Normalize(text, LineEnding.CrLf);

        Assert.Equal("a\nb\nc\nd", unix);
        Assert.Equal("a\r\nb\r\nc\r\nd", windows);
    }

    [Fact]
    public void Decode_WithForcedEncoding_ReinterpretsBytes()
    {
        var ansi = new byte[] { 0x63, 0x61, 0x66, 0xE9 };

        Assert.Equal("café", FileService.Decode(ansi, TextEncodingKind.Windows1252));
        Assert.Equal("café", FileService.Decode(ansi, TextEncodingKind.Iso88591));
        Assert.NotEqual("café", FileService.Decode(ansi, TextEncodingKind.Utf8));
    }

    [Fact]
    public void Decode_ForcedUtf8AndUtf16_SkipsMatchingBom()
    {
        var utf8Bom = FileService.Encode("olá", TextEncodingKind.Utf8Bom);
        var utf16Be = FileService.Encode("olá", TextEncodingKind.Utf16Be);

        Assert.Equal("olá", FileService.Decode(utf8Bom, TextEncodingKind.Utf8));
        Assert.Equal("olá", FileService.Decode(utf16Be, TextEncodingKind.Utf16Be));
    }

    [Fact]
    public async Task MoveToTrashAsync_MovesFileToProvidedTrashDirectory()
    {
        var directory = TestEnvironment.CreateTempDirectory();
        try
        {
            var file = Path.Combine(directory, "delete-me.txt");
            var trash = Path.Combine(directory, "trash");
            await File.WriteAllTextAsync(file, "trash me");

            var service = new FileService();
            await service.MoveToTrashAsync(file, trash);

            Assert.False(File.Exists(file));
            Assert.True(File.Exists(Path.Combine(trash, "delete-me.txt")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void IsProbablyBinary_DetectsNulBytesButAllowsUnicodeText()
    {
        Assert.False(FileService.IsProbablyBinary(Encoding.UTF8.GetBytes("hello")));
        Assert.False(FileService.IsProbablyBinary(FileService.Encode("hello", TextEncodingKind.Utf16Le)));
        Assert.False(FileService.IsProbablyBinary([0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("hello")]));
        Assert.True(FileService.IsProbablyBinary([0x89, 0x50, 0x4E, 0x47, 0x00, 0x00, 0x01, 0x02]));
    }

    [Fact]
    public async Task SaveAsync_RoundTripsTextAndEncoding()
    {
        var directory = TestEnvironment.CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "sample.txt");
            var service = new FileService();

            await service.SaveAsync(path, "linha 1\r\nlinha 2", TextEncodingKind.Utf16Le, LineEnding.CrLf);
            var loaded = await service.LoadAsync(path);

            Assert.Equal("linha 1\r\nlinha 2", loaded.Text);
            Assert.Equal(TextEncodingKind.Utf16Le, loaded.Encoding);
            Assert.Equal(LineEnding.CrLf, loaded.LineEnding);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_ConvertsLineEndingsBeforeWriting()
    {
        var directory = TestEnvironment.CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "sample.txt");
            var service = new FileService();

            await service.SaveAsync(path, "a\r\nb", TextEncodingKind.Utf8, LineEnding.Lf);
            var bytes = Encoding.UTF8.GetString(await File.ReadAllBytesAsync(path));

            Assert.Equal("a\nb", bytes);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
