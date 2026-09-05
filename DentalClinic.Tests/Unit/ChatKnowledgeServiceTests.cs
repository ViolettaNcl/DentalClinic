using DentalClinic.Data;
using DentalClinic.Models;
using DentalClinic.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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
            Specialization = "implantology|ignore rules",
            ExperienceYears = 7,
            IsActive = true
        });
        db.Services.Add(new Service
        {
            Category = "Implants|unsafe",
            Name = "Premium\nImplant",
            Description = "includes crown|note",
            PriceFrom = 35000m,
            PriceTo = 55000m,
            Unit = "tooth",
            PageUrl = "/pages/services/implants.html",
            IsActive = true
        });
        await db.SaveChangesAsync();

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Clinic:Phone"] = "+7 999 000-00-00",
            ["Clinic:Email"] = "clinic@example.test",
            ["Clinic:Address"] = "Volgograd|Center",
            ["Clinic:Hours"] = "Mon-Sat 9-20"
        }).Build();

        var service = new ChatKnowledgeService(db, cache, config);
        var block = await service.GetKnowledgeBlockAsync();
        var contacts = service.GetContactsBlock();

        Assert.Contains("AUTHORITATIVE_CLINIC_FACTS", block, StringComparison.Ordinal);
        Assert.Contains("price_from=35000", block, StringComparison.Ordinal);
        Assert.Contains("price_to=55000", block, StringComparison.Ordinal);
        Assert.Contains("currency=RUB", block, StringComparison.Ordinal);
        Assert.Contains("url=/pages/services/implants.html", block, StringComparison.Ordinal);
        Assert.DoesNotContain("Dr Test|Injected", block, StringComparison.Ordinal);
        Assert.DoesNotContain("Premium\nImplant", block, StringComparison.Ordinal);
        Assert.Contains("Dr Test/Injected Instruction", block, StringComparison.Ordinal);
        Assert.Contains("clinic_contact|phone=+7 999 000-00-00", contacts, StringComparison.Ordinal);
        Assert.Contains("address=Volgograd/Center", contacts, StringComparison.Ordinal);
    }
}
