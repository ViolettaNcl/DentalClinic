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

    [Fact]
    public void SessionId_PreservesNormalBrowserIdentifier()
    {
        const string sessionId = "8ec19b05-58b9-4bd2-ae62_17fba12c";
        var log = new ChatMessageLog { SessionId = sessionId };

        Assert.Equal(sessionId, log.SessionId);
    }

    [Fact]
    public void SessionId_OversizedValueBecomesStableDatabaseSafeHash()
    {
        var raw = new string('a', 200) + "-client-controlled";
        var first = new ChatMessageLog { SessionId = raw };
        var second = new ChatMessageLog { SessionId = raw };

        Assert.Equal(64, first.SessionId.Length);
        Assert.All(first.SessionId, c => Assert.True(Uri.IsHexDigit(c)));
        Assert.Equal(first.SessionId, second.SessionId);
        Assert.NotEqual(raw, first.SessionId);
    }

    [Fact]
    public void SessionId_DoesNotCollapseDifferentValuesWithSameLongPrefix()
    {
        var prefix = new string('x', 100);
        var first = new ChatMessageLog { SessionId = prefix + "-one" };
        var second = new ChatMessageLog { SessionId = prefix + "-two" };

        Assert.NotEqual(first.SessionId, second.SessionId);
        Assert.Equal(64, first.SessionId.Length);
        Assert.Equal(64, second.SessionId.Length);
    }

    [Fact]
    public void SessionId_UnsafeCharactersArePseudonymized()
    {
        const string raw = "session id/<script>alert(1)</script>";
        var log = new ChatMessageLog { SessionId = raw };

        Assert.Equal(64, log.SessionId.Length);
        Assert.DoesNotContain("script", log.SessionId, StringComparison.OrdinalIgnoreCase);
        Assert.All(log.SessionId, c => Assert.True(Uri.IsHexDigit(c)));
    }

    [Theory]
    [InlineData("RU", "ru")]
    [InlineData(" en ", "en")]
    [InlineData("fr", "fr")]
    [InlineData("EL", "el")]
    [InlineData("ar", "ar")]
    public void Lang_NormalizesSupportedLocaleCodes(string raw, string expected)
    {
        var log = new ChatMessageLog { Lang = raw };
        Assert.Equal(expected, log.Lang);
    }

    [Theory]
    [InlineData("this-is-not-a-language")]
    [InlineData("")]
    [InlineData("de")]
    public void Lang_UnsupportedOrOversizedValueFallsBackToRussian(string raw)
    {
        var log = new ChatMessageLog { Lang = raw };
        Assert.Equal("ru", log.Lang);
        Assert.True(log.Lang.Length <= 5);
    }
}
