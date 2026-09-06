using System.Net;
using System.Net.Http.Json;
using DentalClinic.Models;
using Xunit;

namespace DentalClinic.Tests.Integration;

public class ReviewControllerValidationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ReviewControllerValidationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Create_OversizedReview_ReturnsBadRequestBeforePersistence()
    {
        var client = _factory.CreateClient();
        var email = $"review-{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            FirstName = "ReviewTester",
            Email = email,
            Password = "password123"
        });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        var response = await client.PostAsJsonAsync("/api/review", new CreateReviewRequest
        {
            Rating = 5,
            Text = new string('x', 1001)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
