using System.Reflection;
using DentalClinic.Controllers;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class DentaSourceSafetyTests
{
    [Fact]
    public void ProactiveMessages_DoNotContainLegacyPainGuarantees()
    {
        var field = typeof(ChatController).GetField(
            "ProactiveMessages",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(field);
        var messages = Assert.IsType<Dictionary<string, Dictionary<string, string>>>(field!.GetValue(null));
        var allText = string.Join("\n", messages.Values.SelectMany(x => x.Values));

        var banned = new[]
        {
            "абсолютно безболезненно",
            "без боли и страха",
            "completely painless",
            "without pain or fear",
            "totalement indolore",
            "sans douleur ni crainte",
            "εντελώς ανώδυνο",
            "χωρίς πόνο και φόβο",
            "غير مؤلم تمامًا",
            "دون ألم أو خوف"
        };

        foreach (var phrase in banned)
            Assert.DoesNotContain(phrase, allText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BaseSymptomPrompt_DoesNotMapSymptomsToSpecificProcedures()
    {
        var field = typeof(ChatController).GetField(
            "SymptomSafetyPrompt",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(field);
        var prompt = Assert.IsType<string>(field!.GetRawConstantValue());

        Assert.DoesNotContain("боль при жевании → вероятно кариес/пломба", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("боль от холодного → каналы", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Не связывай симптом с конкретным диагнозом или процедурой", prompt, StringComparison.Ordinal);
        Assert.Contains("Не назначай лекарства и дозировки", prompt, StringComparison.Ordinal);
        Assert.Contains("затруднённом дыхании или глотании", prompt, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Хочу узнать об имплантах", "ru", "/pages/services/implants.html", "Подробнее об имплантах →")]
    [InlineData("I may need a root canal", "en", "/pages/services/root-canal.html", "Root canal treatment →")]
    [InlineData("Je voudrais une couronne", "fr", "/pages/services/crowns.html", "Couronnes →")]
    [InlineData("Χρειάζομαι εξαγωγή δοντιού", "el", "/pages/services/extractions.html", "Εξαγωγή δοντιού →")]
    [InlineData("أريد حجز موعد", "ar", "/pages/contact.html", "احجز موعدًا ←")]
    public void AutoLinks_MatchesAndLabelsFallbackInAllSupportedLanguages(
        string text,
        string lang,
        string expectedUrl,
        string expectedLabel)
    {
        var method = typeof(ChatController).GetMethod(
            "AutoLinks",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        var links = Assert.IsType<List<Dictionary<string, string>>>(method!.Invoke(null, new object[] { text, lang }));

        var link = Assert.Single(links);
        Assert.Equal(expectedUrl, link["url"]);
        Assert.Equal(expectedLabel, link["text"]);
        Assert.StartsWith("/pages/", link["url"], StringComparison.Ordinal);
    }
}
