using DentalClinic.Services;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class ImageUploadValidatorTests
{
    [Theory]
    [InlineData(".jpg")]
    [InlineData(".jpeg")]
    public async Task JpegSignature_MatchesJpegExtensions(string extension)
    {
        await using var stream = new MemoryStream([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10]);

        Assert.True(await ImageUploadValidator.MatchesExtensionAsync(stream, extension));
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public async Task PngSignature_MatchesPngExtension()
    {
        await using var stream = new MemoryStream([
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D
        ]);

        Assert.True(await ImageUploadValidator.MatchesExtensionAsync(stream, ".png"));
    }

    [Fact]
    public async Task WebpSignature_MatchesWebpExtension()
    {
        await using var stream = new MemoryStream([
            (byte)'R', (byte)'I', (byte)'F', (byte)'F', 0, 0, 0, 0,
            (byte)'W', (byte)'E', (byte)'B', (byte)'P'
        ]);

        Assert.True(await ImageUploadValidator.MatchesExtensionAsync(stream, ".webp"));
    }

    [Fact]
    public async Task RenamedHtml_IsRejectedEvenWithAllowedExtension()
    {
        await using var stream = new MemoryStream("<script>alert('x')</script>"u8.ToArray());

        Assert.False(await ImageUploadValidator.MatchesExtensionAsync(stream, ".png"));
    }

    [Fact]
    public async Task RealImageSignature_WithWrongExtension_IsRejected()
    {
        await using var stream = new MemoryStream([0xFF, 0xD8, 0xFF, 0xE0]);

        Assert.False(await ImageUploadValidator.MatchesExtensionAsync(stream, ".png"));
    }
}
