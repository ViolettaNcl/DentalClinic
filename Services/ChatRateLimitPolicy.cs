namespace DentalClinic.Services;

public readonly record struct ChatRateLimitProfile(string Bucket, int PermitLimit);

public static class ChatRateLimitPolicy
{
    public const int ChatPermitLimit = 15;
    public const int TtsPermitLimit = 4;

    public static ChatRateLimitProfile Resolve(string? path)
    {
        var isTts = string.Equals(path, "/api/chat/tts", StringComparison.OrdinalIgnoreCase);
        return isTts
            ? new ChatRateLimitProfile("tts", TtsPermitLimit)
            : new ChatRateLimitProfile("chat", ChatPermitLimit);
    }
}
