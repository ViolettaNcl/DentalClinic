using DentalClinic.Services;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class ChatRateLimitPolicyTests
{
    [Theory]
    [InlineData("/api/chat/tts")]
    [InlineData("/API/CHAT/TTS")]
    public void Resolve_TtsRoute_UsesDedicatedPaidQuotaBucket(string path)
    {
        var profile = ChatRateLimitPolicy.Resolve(path);

        Assert.Equal("tts", profile.Bucket);
        Assert.Equal(ChatRateLimitPolicy.TtsPermitLimit, profile.PermitLimit);
        Assert.Equal(4, profile.PermitLimit);
    }

    [Theory]
    [InlineData("/api/chat")]
    [InlineData("/api/chat/stream")]
    [InlineData(null)]
    public void Resolve_OtherChatRoutes_KeepNormalChatBudget(string? path)
    {
        var profile = ChatRateLimitPolicy.Resolve(path);

        Assert.Equal("chat", profile.Bucket);
        Assert.Equal(ChatRateLimitPolicy.ChatPermitLimit, profile.PermitLimit);
        Assert.Equal(15, profile.PermitLimit);
    }

    [Fact]
    public void PaidTtsBudget_IsStricterThanNormalChatBudget()
    {
        Assert.True(ChatRateLimitPolicy.TtsPermitLimit < ChatRateLimitPolicy.ChatPermitLimit);
    }
}
