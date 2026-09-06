using System.Net;
using System.Net.Http.Json;
using DentalClinic.Data;
using DentalClinic.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DentalClinic.Tests.Integration;

public class AdminAppointmentUpdateIntegrityTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AdminAppointmentUpdateIntegrityTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Update_AssigningDoctorWithoutDate_ReturnsBadRequestAndDoesNotPartiallyMutate()
    {
        int appointmentId;
        int doctorId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var doctor = new Doctor
            {
                FullName = $"CRM integrity doctor {Guid.NewGuid():N}",
                IsActive = true
            };
            var appointment = new AppointmentRequest
            {
                FirstName = "CRM test",
                Phone = UniquePhone(),
                Comment = "original comment",
                Status = AppointmentStatuses.Pending,
                AppointmentDate = null,
                DoctorId = null
            };

            db.Doctors.Add(doctor);
            db.AppointmentRequests.Add(appointment);
            await db.SaveChangesAsync();
            doctorId = doctor.Id;
            appointmentId = appointment.Id;
        }

        var client = await CreateAuthenticatedAdminClientAsync();
        var response = await client.PutAsJsonAsync($"/api/AppointmentRequest/{appointmentId}", new
        {
            doctorId,
            comment = "must not be persisted"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var verificationScope = _factory.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await verificationDb.AppointmentRequests.FindAsync(appointmentId);

        Assert.NotNull(saved);
        Assert.Null(saved!.DoctorId);
        Assert.Null(saved.AppointmentDate);
        Assert.Equal("original comment", saved.Comment);
        Assert.Equal(AppointmentStatuses.Pending, saved.Status);
    }

    [Fact]
    public async Task Update_AssigningDoctorToValidDatedPendingRequest_Succeeds()
    {
        int appointmentId;
        int doctorId;
        var appointmentDate = NextOpenDayAt(15, 0);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var doctor = new Doctor
            {
                FullName = $"CRM scheduled doctor {Guid.NewGuid():N}",
                IsActive = true
            };
            var appointment = new AppointmentRequest
            {
                FirstName = "CRM scheduled test",
                Phone = UniquePhone(),
                Status = AppointmentStatuses.Pending,
                AppointmentDate = appointmentDate,
                DoctorId = null
            };

            db.Doctors.Add(doctor);
            db.AppointmentRequests.Add(appointment);
            await db.SaveChangesAsync();
            doctorId = doctor.Id;
            appointmentId = appointment.Id;
        }

        var client = await CreateAuthenticatedAdminClientAsync();
        var response = await client.PutAsJsonAsync($"/api/AppointmentRequest/{appointmentId}", new
        {
            doctorId
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var verificationScope = _factory.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await verificationDb.AppointmentRequests.FindAsync(appointmentId);

        Assert.NotNull(saved);
        Assert.Equal(doctorId, saved!.DoctorId);
        Assert.Equal(appointmentDate, saved.AppointmentDate);
        Assert.Equal(AppointmentStatuses.Pending, saved.Status);
    }

    [Fact]
    public async Task Update_ExplicitNulls_ClearPendingScheduleAndComment()
    {
        int appointmentId;
        var appointmentDate = NextOpenDayAt(15, 0);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var doctor = new Doctor
            {
                FullName = $"CRM clear doctor {Guid.NewGuid():N}",
                IsActive = true
            };
            db.Doctors.Add(doctor);
            await db.SaveChangesAsync();

            var appointment = new AppointmentRequest
            {
                FirstName = "CRM clear test",
                Phone = UniquePhone(),
                Comment = "remove me",
                Status = AppointmentStatuses.Pending,
                AppointmentDate = appointmentDate,
                DoctorId = doctor.Id,
                ReminderSent = true
            };

            db.AppointmentRequests.Add(appointment);
            await db.SaveChangesAsync();
            appointmentId = appointment.Id;
        }

        var client = await CreateAuthenticatedAdminClientAsync();
        var response = await client.PutAsJsonAsync($"/api/AppointmentRequest/{appointmentId}", new
        {
            doctorId = (int?)null,
            appointmentDate = (DateTime?)null,
            comment = (string?)null
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var verificationScope = _factory.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await verificationDb.AppointmentRequests.FindAsync(appointmentId);

        Assert.NotNull(saved);
        Assert.Null(saved!.DoctorId);
        Assert.Null(saved.AppointmentDate);
        Assert.Null(saved.Comment);
        Assert.False(saved.ReminderSent);
        Assert.Equal(AppointmentStatuses.Pending, saved.Status);
    }

    [Fact]
    public async Task Update_OmittedNullableFields_PreserveExistingValues()
    {
        int appointmentId;
        int doctorId;
        var appointmentDate = NextOpenDayAt(15, 0);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var doctor = new Doctor
            {
                FullName = $"CRM preserve doctor {Guid.NewGuid():N}",
                IsActive = true
            };
            db.Doctors.Add(doctor);
            await db.SaveChangesAsync();
            doctorId = doctor.Id;

            var appointment = new AppointmentRequest
            {
                FirstName = "CRM preserve test",
                Phone = UniquePhone(),
                Comment = "keep me",
                Status = AppointmentStatuses.Pending,
                AppointmentDate = appointmentDate,
                DoctorId = doctorId
            };

            db.AppointmentRequests.Add(appointment);
            await db.SaveChangesAsync();
            appointmentId = appointment.Id;
        }

        var client = await CreateAuthenticatedAdminClientAsync();
        var response = await client.PutAsJsonAsync($"/api/AppointmentRequest/{appointmentId}", new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var verificationScope = _factory.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await verificationDb.AppointmentRequests.FindAsync(appointmentId);

        Assert.NotNull(saved);
        Assert.Equal(doctorId, saved!.DoctorId);
        Assert.Equal(appointmentDate, saved.AppointmentDate);
        Assert.Equal("keep me", saved.Comment);
    }

    [Fact]
    public async Task Update_ClearingDateWhileDoctorRemains_ReturnsBadRequestAndPreservesSchedule()
    {
        int appointmentId;
        int doctorId;
        var appointmentDate = NextOpenDayAt(15, 0);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var doctor = new Doctor
            {
                FullName = $"CRM invalid clear doctor {Guid.NewGuid():N}",
                IsActive = true
            };
            db.Doctors.Add(doctor);
            await db.SaveChangesAsync();
            doctorId = doctor.Id;

            var appointment = new AppointmentRequest
            {
                FirstName = "CRM invalid clear test",
                Phone = UniquePhone(),
                Status = AppointmentStatuses.Pending,
                AppointmentDate = appointmentDate,
                DoctorId = doctorId
            };

            db.AppointmentRequests.Add(appointment);
            await db.SaveChangesAsync();
            appointmentId = appointment.Id;
        }

        var client = await CreateAuthenticatedAdminClientAsync();
        var response = await client.PutAsJsonAsync($"/api/AppointmentRequest/{appointmentId}", new
        {
            appointmentDate = (DateTime?)null
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var verificationScope = _factory.Services.CreateScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await verificationDb.AppointmentRequests.FindAsync(appointmentId);

        Assert.NotNull(saved);
        Assert.Equal(doctorId, saved!.DoctorId);
        Assert.Equal(appointmentDate, saved.AppointmentDate);
    }

    private async Task<HttpClient> CreateAuthenticatedAdminClientAsync()
    {
        var email = $"admin-crm-{Guid.NewGuid():N}@example.com";
        const string password = "admin-test-password";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Admins.Add(new Admin
            {
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
            });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/admin/login", new LoginRequest
        {
            Email = email,
            Password = password
        });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return client;
    }

    private static string UniquePhone() => $"+7997{Random.Shared.Next(1000000, 9999999)}";

    private static DateTime NextOpenDayAt(int hour, int minute)
    {
        var date = DateTime.UtcNow.Date.AddDays(3);
        while (date.DayOfWeek == DayOfWeek.Sunday)
            date = date.AddDays(1);

        return DateTime.SpecifyKind(date.AddHours(hour).AddMinutes(minute), DateTimeKind.Unspecified);
    }
}
