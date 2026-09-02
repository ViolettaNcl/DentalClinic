using DentalClinic.Models;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class ChatPrivacyTests
{
    [Fact]
    public void ClientIp_DoesNotPersistRawAddress()
    {
        var log = new ChatMessageLog { ClientIp = "203.0.113.42" };

        Assert.NotNull(log.ClientIp);
        Assert.NotEqual("203.0.113.42", log.ClientIp);
        Assert.Equal(64, log.ClientIp!.Length);
        Assert.All(log.ClientIp, c => Assert.True(Uri.IsHexDigit(c)));
    }

    [Fact]
    public void ClientIp_HashIsStableForSameAddress()
    {
        var a = new ChatMessageLog { ClientIp = "2001:db8::1" };
        var b = new ChatMessageLog { ClientIp = "2001:db8::1" };
        Assert.Equal(a.ClientIp, b.ClientIp);
    }

    [Fact]
    public void ClientIp_CanBeClearedByRetentionJob()
    {
        var log = new ChatMessageLog { ClientIp = "203.0.113.42" };
        log.ClientIp = null;
        Assert.Null(log.ClientIp);
    }
}
