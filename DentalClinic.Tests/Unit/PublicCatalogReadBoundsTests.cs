using DentalClinic.Controllers;
using DentalClinic.Data;
using DentalClinic.Models;
using DentalClinic.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class PublicCatalogReadBoundsTests
{
    [Fact]
    public async Task PublicDoctors_AreBoundedAndAdvertiseTruncation()
    {
        await using var db = CreateDb();
        db.Doctors.AddRange(Enumerable.Range(1, 205).Select(index => new Doctor
        {
            FullName = $"Doctor {index:000}",
            IsActive = true
        }));
        await db.SaveChangesAsync();
        var controller = WithHttpContext(new DoctorController(
            db,
            CreateClock(),
            NullLogger<DoctorController>.Instance));

        var result = Assert.IsType<OkObjectResult>(await controller.GetAll(CancellationToken.None));
        var doctors = Assert.IsAssignableFrom<IReadOnlyCollection<PublicDoctorDto>>(result.Value);

        Assert.Equal(200, doctors.Count);
        Assert.Equal("true", controller.Response.Headers["X-Result-Truncated"].ToString());
    }

    [Fact]
    public async Task PublicServices_AreBoundedAndAdvertiseTruncation()
    {
        await using var db = CreateDb();
        db.Services.AddRange(Enumerable.Range(1, 505).Select(index => new Service
        {
            Category = "Bounded",
            Name = $"Service {index:000}",
            PriceFrom = index,
            SortOrder = index,
            IsActive = true
        }));
        await db.SaveChangesAsync();
        var controller = WithHttpContext(new ServiceController(
            db,
            NullLogger<ServiceController>.Instance));

        var result = Assert.IsType<OkObjectResult>(await controller.GetAll(CancellationToken.None));
        var services = Assert.IsAssignableFrom<IReadOnlyCollection<PublicServiceDto>>(result.Value);

        Assert.Equal(500, services.Count);
        Assert.Equal("true", controller.Response.Headers["X-Result-Truncated"].ToString());
    }

    private static T WithHttpContext<T>(T controller) where T : ControllerBase
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return controller;
    }

    private static ClinicClock CreateClock()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Scheduling:TimeZoneId"] = "UTC"
            })
            .Build();
        return new ClinicClock(configuration, TimeProvider.System);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"public-catalog-bounds-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    }
}
