namespace DentalClinic.Services;

public static class ImageUploadValidator
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static async Task<bool> MatchesExtensionAsync(
        Stream stream,
        string extension,
        CancellationToken cancellationToken = default)
    {
        if (!stream.CanRead) return false;

        var originalPosition = stream.CanSeek ? stream.Position : 0;
        var header = new byte[12];
        var read = 0;

        try
        {
            while (read < header.Length)
            {
                var count = await stream.ReadAsync(header.AsMemory(read, header.Length - read), cancellationToken);
                if (count == 0) break;
                read += count;
            }
        }
        finally
        {
            if (stream.CanSeek) stream.Position = originalPosition;
        }

        extension = extension.ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => read >= 3
                && header[0] == 0xFF
                && header[1] == 0xD8
                && header[2] == 0xFF,
            ".png" => read >= PngSignature.Length
                && header.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature),
            ".webp" => read >= 12
                && header.AsSpan(0, 4).SequenceEqual("RIFF"u8)
                && header.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };
    }
}
