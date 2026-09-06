using DentalClinic.Services;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class PaidApiRoutePolicyTests
{
    [Theory]
    [InlineData("POST", "/api/chat")]
    [InlineData("POST", "/api/chat/stream")]
    [InlineData("POST", "/api/chat/tts")]
    [InlineData("POST", "/api/translate")]
    [InlineData("POST", "/api/review/translate")]
    [InlineData("post", "/API/CHAT")]
    [InlineData("post", "/API/REVIEW/TRANSLATE")]
    public void PaidProviderPosts_RequireSameOrigin(string method, string path)
    {
        Assert.True(PaidApiRoutePolicy.RequiresSameOrigin(method, path));
    }

    [Theory]
    [InlineData("GET", "/api/chat")]
    [InlineData("GET", "/api/translate")]
    [InlineData("POST", "/api/chat/admin/stats")]
    [InlineData("GET", "/api/review/approved")]
    [InlineData("POST", "/api/appointments")]
    [InlineData(null, "/api/chat")]
    [InlineData("POST", null)]
    public void OtherRoutes_DoNotUsePaidProviderOriginGuard(string? method, string? path)
    {
        Assert.False(PaidApiRoutePolicy.RequiresSameOrigin(method, path));
    }
}
