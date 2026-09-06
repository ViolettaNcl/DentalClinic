using DentalClinic.Services;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class PaidApiPayloadPolicyTests
{
    [Fact]
    public void UnknownContentLength_IsNotRejectedByPrecheck()
    {
        Assert.False(PaidApiPayloadPolicy.IsKnownLengthTooLarge(null));
    }

    [Theory]
    [InlineData(0L, false)]
    [InlineData(65535L, false)]
    [InlineData(65536L, false)]
    [InlineData(65537L, true)]
    public void KnownContentLength_IsBounded(long contentLength, bool expectedTooLarge)
    {
        Assert.Equal(expectedTooLarge, PaidApiPayloadPolicy.IsKnownLengthTooLarge(contentLength));
    }

    [Fact]
    public void Limit_RemainsLargeEnoughForBoundedChatHistory()
    {
        // ChatController currently caps one message to 800 chars and history to
        // 12 x 800 chars. 64 KiB leaves generous JSON/Unicode overhead while still
        // rejecting bodies that are orders of magnitude beyond normal requests.
        Assert.True(PaidApiPayloadPolicy.MaxRequestBodyBytes >= 32 * 1024);
        Assert.True(PaidApiPayloadPolicy.MaxRequestBodyBytes <= 128 * 1024);
    }
}
