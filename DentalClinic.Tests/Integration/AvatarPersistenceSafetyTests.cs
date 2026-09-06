using System.Security.Claims;
using DentalClinic.Controllers;
using DentalClinic.Data;
using DentalClinic.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DentalClinic.Tests.Integration;

public sealed class AvatarPersistenceSafetyTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"dental-avatar-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task Upload_WhenAuthenticatedPatientNoLongerExists_DoesNotLeaveOrphanFile()
    {
        Directory.CreateDirectory(_root);
        await using var db = CreateDb();
        var controller = CreateController(db, "Patient", 404);

        var result = await controller.Upload(CreatePngFormFile());

        Assert.IsType<NotFoundResult>(result);
        var uploads = Path.Combine(_root, "uploads", "avatars");
        Assert.False(Directory.Exists(uploads) && Directory.EnumerateFiles(uploads).Any());
    }

    [Fact]
    public async Task Delete_RejectsTraversalStoredInLegacyAvatarUrl()
    {
        Directory.CreateDirectory(_root);
        var sentinel = Path.Combine(_root, "sentinel.txt");
        await File.WriteAllTextAsync(sentinel, "must survive");

        await using var db = CreateDb();
        db.Patients.Add(new Patient
        {
            Id = 7,
            FirstName = "Test",
            Email = "patient@example.test",
            PasswordHash = "hash",
            AvatarUrl = "/uploads/avatars/../../sentinel.txt"
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db, "Patient", 7);
        var result = await controller.Delete();

        Assert.IsType<OkObjectResult>(result);
        Assert.True(File.Exists(sentinel));
        Assert.Null((await db.Patients.FindAsync(7))!.AvatarUrl);
    }

    [Fact]
    public async Task Upload_ValidImage_CommitsNewUrlBeforeRemovingOldFile()
    {
        var uploads = Path.Combine(_root, "uploads", "avatars");
        Directory.CreateDirectory(uploads);
        var oldPath = Path.Combine(uploads, "patient-9-old.png");
        await File.WriteAllBytesAsync(oldPath, ValidPngBytes());

        await using var db = CreateDb();
        db.Patients.Add(new Patient
        {
            Id = 9,
            FirstName = "Test",
            Email = "replace@example.test",
            PasswordHash = "hash",
            AvatarUrl = "/uploads/avatars/patient-9-old.png"
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db, "Patient", 9);
        var result = await controller.Upload(CreatePngFormFile());

        Assert.IsType<OkObjectResult>(result);
        var patient = (await db.Patients.FindAsync(9))!;
        Assert.NotNull(patient.AvatarUrl);
        Assert.StartsWith("/uploads/avatars/patient-9-", patient.AvatarUrl, StringComparison.Ordinal);
        Assert.False(File.Exists(oldPath));

        var newPath = Path.Combine(uploads, patient.AvatarUrl!["/uploads/avatars/".Length..]);
        Assert.True(File.Exists(newPath));
    }

    [Fact]
    public async Task Delete_UnknownAuthenticatedRole_IsForbiddenAndDoesNotTouchPatients()
    {
        Directory.CreateDirectory(_root);
        await using var db = CreateDb();
        db.Patients.Add(new Patient
        {
            Id = 11,
            FirstName = "Test",
            Email = "role@example.test",
            PasswordHash = "hash",
            AvatarUrl = "/uploads/avatars/patient-11-existing.png"
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db, "UnexpectedRole", 11);
        var result = await controller.Delete();

        Assert.IsType<ForbidResult>(result);
        Assert.Equal("/uploads/avatars/patient-11-existing.png", (await db.Patients.FindAsync(11))!.AvatarUrl);
    }

    private ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"avatar-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    }

    private AvatarController CreateController(ApplicationDbContext db, string role, int id)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, id.ToString()),
            new Claim(ClaimTypes.Role, role)
        ], "test");

        return new AvatarController(db, new TestEnvironment(_root), NullLogger<AvatarController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            }
        };
    }

    private static FormFile CreatePngFormFile()
    {
        var bytes = ValidPngBytes();
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", "avatar.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };
    }

    private static byte[] ValidPngBytes() =>
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52
    ];

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Temp cleanup is best effort and must not hide the test assertion.
        }
    }

    private sealed class TestEnvironment(string webRoot) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "DentalClinic.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = webRoot;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = webRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}