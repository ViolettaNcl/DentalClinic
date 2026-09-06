using DentalClinic.Services;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class PaidApiRoutePolicyTests
{
    [Theory]
    [InlineData("POST", "/api/chat")]
    [InlineData("POST", "/api/chat/stream")]
    [InlineData("POST", "/api/chat/tts")]
    [InlineData("post", "/API/CHAT")]
    public void PaidChatPosts_RequireSameOrigin(string method, string path)
    {
        Assert.True(PaidApiRoutePolicy.RequiresSameOrigin(method, path));
    }

    [Theory]
    [InlineData("GET", "/api/chat")]
    [InlineData("POST", "/api/chat/admin/stats")]
    [InlineData("POST", "/api/appointments")]
    [InlineData(null, "/api/chat")]
    [InlineData("POST", null)]
    public void OtherRoutes_DoNotUsePaidChatOriginGuard(string? method, string? path)
    {
        Assert.False(PaidApiRoutePolicy.RequiresSameOrigin(method, path));
    }
}
