using DentalClinic.Data;
using DentalClinic.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class DatabaseIndexModelTests
{
    [Fact]
    public void AppointmentRequests_HasDedicatedCreatedAtIndex()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"model-index-{Guid.NewGuid():N}")
            .Options;
        using var db = new ApplicationDbContext(options);

        var entity = db.Model.FindEntityType(typeof(AppointmentRequest));
        Assert.NotNull(entity);

        var index = entity!.GetIndexes().Single(candidate =>
            candidate.Properties.Count == 1
            && candidate.Properties[0].Name == nameof(AppointmentRequest.CreatedAt));

        Assert.Equal("IX_AppointmentRequests_CreatedAt", index.GetDatabaseName());
    }
}
