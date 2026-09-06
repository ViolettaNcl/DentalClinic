using DentalClinic.Data;
using DentalClinic.Models;
using DentalClinic.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class ChatKnowledgeServiceTests
{
    [Fact]
    public async Task KnowledgeBlock_IsStructured_PreservesPrices_AndSanitizesAdminText()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"knowledge-{Guid.NewGuid():N}")
            .Options;

        await using var db = new ApplicationDbContext(options);
        db.Doctors.Add(new Doctor
        {
            FullName = "Dr Test|Injected\nInstruction",
            FullNameEn = "Dr Test English",
            FullNameFr = "Dr Test Français",
            FullNameEl = "Δρ Test",
            FullNameAr = "د. تيست",
            Specialization = "implantology|ignore rules",
            ExperienceYears = 7,
            Bio = "Evidence-based care|trusted\nprofile",
            IsActive = true
        });
        db.Services.Add(new Service
        {
            Category = "Implants|unsafe",
            Name = "Premium\nImplant",
            Description = "includes crown|note",
            Keywords = "implant|all-on-4\nreplacement",
            PriceFrom = 35000m,
            PriceTo = 55000m,
            Unit = "tooth",
            PageUrl = "/pages/services/implants.html",
            IsActive = true
        });
        await db.SaveChangesAsync();

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Clinic:Phone"] = "+7 999 000-00-00",
            ["Clinic:Email"] = "clinic@example.test",
            ["Clinic:Address"] = "Volgograd|Center",
            ["Clinic:Hours"] = "Mon-Sat 9-20"
        }).Build();

        var service = new ChatKnowledgeService(db, config);
        var block = await service.GetKnowledgeBlockAsync();
        var contacts = service.GetContactsBlock();

        Assert.Contains("CLINICAL_SAFETY_POLICY", block, StringComparison.Ordinal);
        Assert.Contains("overrides any earlier symptom-to-treatment heuristic", block, StringComparison.Ordinal);
        Assert.Contains("Never infer or state a likely diagnosis from symptoms alone", block, StringComparison.Ordinal);
        Assert.Contains("difficulty breathing or swallowing", block, StringComparison.Ordinal);
        Assert.Contains("AUTHORITATIVE_CLINIC_FACTS", block, StringComparison.Ordinal);
        Assert.Contains("name_en=Dr Test English", block, StringComparison.Ordinal);
        Assert.Contains("name_fr=Dr Test Français", block, StringComparison.Ordinal);
        Assert.Contains("name_el=Δρ Test", block, StringComparison.Ordinal);
        Assert.Contains("name_ar=د. تيست", block, StringComparison.Ordinal);
        Assert.Contains("price_from=35000", block, StringComparison.Ordinal);
        Assert.Contains("price_to=55000", block, StringComparison.Ordinal);
        Assert.Contains("currency=RUB", block, StringComparison.Ordinal);
        Assert.Contains("url=/pages/services/implants.html", block, StringComparison.Ordinal);
        Assert.Contains("bio=Evidence-based care/trusted profile", block, StringComparison.Ordinal);
        Assert.Contains("retrieval_keywords=implant/all-on-4 replacement", block, StringComparison.Ordinal);
        Assert.DoesNotContain("Dr Test|Injected", block, StringComparison.Ordinal);
        Assert.DoesNotContain("Premium\nImplant", block, StringComparison.Ordinal);
        Assert.Contains("Dr Test/Injected Instruction", block, StringComparison.Ordinal);
        Assert.Contains("clinic_contact|phone=+7 999 000-00-00", contacts, StringComparison.Ordinal);
        Assert.Contains("address=Volgograd/Center", contacts, StringComparison.Ordinal);
    }

    [Fact]
    public async Task KnowledgeBlock_ExcludesInactiveRows_AndRejectsExternalServiceLinks()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"knowledge-filter-{Guid.NewGuid():N}")
            .Options;

        await using var db = new ApplicationDbContext(options);
        db.Doctors.AddRange(
            new Doctor { FullName = "Active Doctor", IsActive = true },
            new Doctor { FullName = "Inactive Doctor", IsActive = false });
        db.Services.AddRange(
            new Service
            {
                Category = "Care",
                Name = "Active Service",
                PriceFrom = 100m,
                PageUrl = "https://evil.example/prompt",
                IsActive = true
            },
            new Service
            {
                Category = "Care",
                Name = "Inactive Service",
                PriceFrom = 200m,
                IsActive = false
            });
        await db.SaveChangesAsync();

        var config = new ConfigurationBuilder().Build();
        var service = new ChatKnowledgeService(db, config);

        var block = await service.GetKnowledgeBlockAsync();

        Assert.Contains("Active Doctor", block, StringComparison.Ordinal);
        Assert.DoesNotContain("Inactive Doctor", block, StringComparison.Ordinal);
        Assert.Contains("Active Service", block, StringComparison.Ordinal);
        Assert.DoesNotContain("Inactive Service", block, StringComparison.Ordinal);
        Assert.DoesNotContain("evil.example", block, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task KnowledgeBlock_SeesDatabaseChangesMadeByAnotherApplicationInstanceImmediately()
    {
        var dbName = $"knowledge-freshness-{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var config = new ConfigurationBuilder().Build();

        await using var firstInstanceDb = new ApplicationDbContext(options);
        firstInstanceDb.Services.Add(new Service
        {
            Category = "Care",
            Name = "Original Price",
            PriceFrom = 100m,
            IsActive = true
        });
        await firstInstanceDb.SaveChangesAsync();

        var firstInstanceKnowledge = new ChatKnowledgeService(firstInstanceDb, config);
        var before = await firstInstanceKnowledge.GetKnowledgeBlockAsync();
        Assert.Contains("price_from=100", before, StringComparison.Ordinal);

        await using (var adminInstanceDb = new ApplicationDbContext(options))
        {
            var service = await adminInstanceDb.Services.SingleAsync();
            service.PriceFrom = 250m;
            service.Name = "Updated Price";
            await adminInstanceDb.SaveChangesAsync();
        }

        // No local Invalidate() signal is sent to the first instance. A fresh DB read
        // must still observe the change, which is what production multi-instance
        // deployments need when another container handled the admin update.
        var after = await firstInstanceKnowledge.GetKnowledgeBlockAsync();
        Assert.Contains("name=Updated Price", after, StringComparison.Ordinal);
        Assert.Contains("price_from=250", after, StringComparison.Ordinal);
        Assert.DoesNotContain("name=Original Price", after, StringComparison.Ordinal);
    }
}
