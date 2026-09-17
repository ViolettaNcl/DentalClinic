using DentalClinic.Data;
using DentalClinic.Models;
using DentalClinic.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class DentaClinicRouterTests
{
    [Fact]
    public async Task DoctorsQuestion_ComesDirectlyFromActiveDatabaseDoctors()
    {
        await using var db = CreateContext();
        db.Doctors.AddRange(
            new Doctor { FullName = "Dr Active", Specialization = "импланты, хирургия", ExperienceYears = 8, IsActive = true },
            new Doctor { FullName = "Dr Hidden", Specialization = "терапия", ExperienceYears = 20, IsActive = false });
        await db.SaveChangesAsync();

        var router = new DentaClinicRouter(db, BuildConfig());
        var answer = await router.TryAnswerAsync("Какие у вас врачи?", Array.Empty<DentaTurn>(), "ru", CancellationToken.None);

        Assert.NotNull(answer);
        Assert.Equal("database", answer!.Source);
        Assert.Contains("Dr Active", answer.Reply, StringComparison.Ordinal);
        Assert.DoesNotContain("Dr Hidden", answer.Reply, StringComparison.Ordinal);
        Assert.Contains("/pages/doctors.html", answer.Links.Select(l => l.Url));
    }

    [Fact]
    public async Task DoctorFollowUp_UsesRecentConversationContextAndSpecialization()
    {
        await using var db = CreateContext();
        db.Doctors.AddRange(
            new Doctor { FullName = "Dr Implant", Specialization = "импланты, хирургия", ExperienceYears = 7, IsActive = true },
            new Doctor { FullName = "Dr Therapy", Specialization = "терапия", ExperienceYears = 12, IsActive = true });
        await db.SaveChangesAsync();

        var history = new[]
        {
            new DentaTurn("user", "Какие у вас врачи?"),
            new DentaTurn("assistant", "Сейчас активны Dr Implant и Dr Therapy")
        };
        var router = new DentaClinicRouter(db, BuildConfig());
        var answer = await router.TryAnswerAsync("А кто из них занимается имплантами?", history, "ru", CancellationToken.None);

        Assert.NotNull(answer);
        Assert.Contains("Dr Implant", answer!.Reply, StringComparison.Ordinal);
        Assert.DoesNotContain("Dr Therapy", answer.Reply, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ServicesAndPriceQuestions_UseActiveServiceRows()
    {
        await using var db = CreateContext();
        db.Services.AddRange(
            new Service { Category = "Импланты", Name = "Стандарт", PriceFrom = 35000, Keywords = "имплант", PageUrl = "/pages/services/implants.html", IsActive = true },
            new Service { Category = "Скрыто", Name = "Неактивная услуга", PriceFrom = 1, Keywords = "имплант", IsActive = false });
        await db.SaveChangesAsync();

        var router = new DentaClinicRouter(db, BuildConfig());
        var all = await router.TryAnswerAsync("Все услуги", Array.Empty<DentaTurn>(), "ru", CancellationToken.None);
        var price = await router.TryAnswerAsync("Сколько стоит имплант?", Array.Empty<DentaTurn>(), "ru", CancellationToken.None);

        Assert.NotNull(all);
        Assert.Contains("Стандарт", all!.Reply, StringComparison.Ordinal);
        Assert.DoesNotContain("Неактивная услуга", all.Reply, StringComparison.Ordinal);
        Assert.NotNull(price);
        Assert.Contains("35", price!.Reply, StringComparison.Ordinal);
        Assert.Contains("Стандарт", price.Reply, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SedationQuestion_UsesConfirmedManagedKnowledge()
    {
        await using var db = CreateContext();
        db.ClinicKnowledgeItems.Add(new ClinicKnowledgeItem
        {
            Category = "sedation",
            Title = "Седация",
            Content = "Подтверждённая информация: седация доступна для пациентов с дентофобией.",
            Keywords = "седация дентофобия sedation",
            SortOrder = 10,
            IsActive = true
        });
        await db.SaveChangesAsync();

        var router = new DentaClinicRouter(db, BuildConfig());
        var answer = await router.TryAnswerAsync("Есть ли у вас седация?", Array.Empty<DentaTurn>(), "ru", CancellationToken.None);

        Assert.NotNull(answer);
        Assert.Equal("database", answer!.Source);
        Assert.Contains("седация доступна", answer.Reply, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/pages/about.html", answer.Links.Select(l => l.Url));
    }

    [Fact]
    public async Task ContactsQuestion_UsesConfigurationAndNeverRetiredPlaceholder()
    {
        await using var db = CreateContext();
        var router = new DentaClinicRouter(db, BuildConfig());

        var answer = await router.TryAnswerAsync("Какой у вас телефон и график?", Array.Empty<DentaTurn>(), "ru", CancellationToken.None);

        Assert.NotNull(answer);
        Assert.Equal("configuration", answer!.Source);
        Assert.Contains("+7 (8442) 00-00-00", answer.Reply, StringComparison.Ordinal);
        Assert.Contains("09:00", answer.Reply, StringComparison.Ordinal);
        Assert.DoesNotContain("999-99-99", answer.Reply, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OurDoctorsPhrase_ListsDoctorsInsteadOfTreatingOurAsSpecialty()
    {
        await using var db = CreateContext();
        db.Doctors.AddRange(
            new Doctor { FullName = "Dr One", Specialization = "терапия", ExperienceYears = 5, IsActive = true },
            new Doctor { FullName = "Dr Two", Specialization = "импланты", ExperienceYears = 7, IsActive = true });
        await db.SaveChangesAsync();

        var router = new DentaClinicRouter(db, BuildConfig());
        var answer = await router.TryAnswerAsync("Наши врачи", Array.Empty<DentaTurn>(), "ru", CancellationToken.None);

        Assert.NotNull(answer);
        Assert.Contains("Dr One", answer!.Reply, StringComparison.Ordinal);
        Assert.Contains("Dr Two", answer.Reply, StringComparison.Ordinal);
        Assert.Contains("/pages/doctors.html", answer.Links.Select(l => l.Url));
    }

    [Fact]
    public async Task GreetingAndHowAreYou_AreAnsweredWithoutGemini()
    {
        await using var db = CreateContext();
        var router = new DentaClinicRouter(db, BuildConfig());

        var answer = await router.TryAnswerAsync("Привет, как ваши дела?", Array.Empty<DentaTurn>(), "ru", CancellationToken.None);

        Assert.NotNull(answer);
        Assert.Equal("router", answer!.Source);
        Assert.Contains("Привет", answer.Reply, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(answer.Suggestions, s => s.Contains("Услуги", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ServicesAndPricesShortcut_IncludesPricesAndServicesPageLink()
    {
        await using var db = CreateContext();
        db.Services.Add(new Service
        {
            Category = "Импланты",
            Name = "Стандарт",
            PriceFrom = 35000,
            Keywords = "имплант",
            PageUrl = "/pages/services/implants.html",
            IsActive = true
        });
        await db.SaveChangesAsync();

        var router = new DentaClinicRouter(db, BuildConfig());
        var answer = await router.TryAnswerAsync("Услуги и цены", Array.Empty<DentaTurn>(), "ru", CancellationToken.None);

        Assert.NotNull(answer);
        Assert.Contains("Стандарт", answer!.Reply, StringComparison.Ordinal);
        Assert.Contains("35", answer.Reply, StringComparison.Ordinal);
        Assert.Contains("/pages/services.html", answer.Links.Select(l => l.Url));
    }

    [Fact]
    public async Task MentioningAppointmentWithoutBookingIntent_DoesNotStartBooking()
    {
        await using var db = CreateContext();
        var router = new DentaClinicRouter(db, BuildConfig());

        var answer = await router.TryAnswerAsync("Что происходит на обычном приёме у стоматолога?", Array.Empty<DentaTurn>(), "ru", CancellationToken.None);

        Assert.Null(answer);
    }

    [Fact]
    public async Task BookingIntent_DoesNotNeedGemini()
    {
        await using var db = CreateContext();
        var router = new DentaClinicRouter(db, BuildConfig());

        var answer = await router.TryAnswerAsync("Хочу записаться на приём", Array.Empty<DentaTurn>(), "ru", CancellationToken.None);

        Assert.NotNull(answer);
        Assert.True(answer!.StartBooking);
        Assert.Equal("router", answer.Source);
        Assert.Contains(answer.Suggestions, s => s.Contains("Запис", StringComparison.OrdinalIgnoreCase));
    }

    private static IConfiguration BuildConfig() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Clinic:Phone"] = "+7 (8442) 00-00-00",
            ["Clinic:Email"] = "info@denta-volgograd.example",
            ["Clinic:Address"] = "Волгоград, проспект Ленина, 1",
            ["Clinic:Hours"] = "Пн–Сб 09:00–20:00; Вс — выходной",
            ["Scheduling:WorkingHours:Monday:Open"] = "09:00",
            ["Scheduling:WorkingHours:Monday:Close"] = "20:00",
            ["Scheduling:WorkingHours:Saturday:Open"] = "09:00",
            ["Scheduling:WorkingHours:Sunday:Closed"] = "true"
        })
        .Build();

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"denta-router-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    }
}
