using System.Text.Json;
using DentalClinic.Filters;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class DentaProactiveSafetyFilterTests
{
    [Theory]
    [InlineData("Вижу, вы на странице о лечении каналов. Это звучит страшно, но у нас это абсолютно безболезненно 🤍 Есть вопросы?", "абсолютно безболезненно")]
    [InlineData("I see you're on our root canal treatment page. It may sound scary, but with us it's completely painless 🤍 Any questions?", "completely painless")]
    [InlineData("Je vois que vous êtes sur notre page de traitement de canal. Cela peut sembler effrayant, mais chez nous c'est totalement indolore 🤍 Des questions ?", "totalement indolore")]
    [InlineData("Βλέπω ότι είστε στη σελίδα για τη θεραπεία ρίζας. Ίσως ακούγεται τρομακτικό, αλλά με εμάς είναι εντελώς ανώδυνο 🤍 Έχετε ερωτήσεις;", "εντελώς ανώδυνο")]
    [InlineData("أرى أنك في صفحة علاج قناة الجذر. قد يبدو الأمر مخيفًا، لكنه غير مؤلم تمامًا لدينا 🤍 هل لديك أسئلة؟", "غير مؤلم تمامًا")]
    [InlineData("Изучаете удаление зубов? Расскажу чего ожидать и как проходит процедура без боли и страха.", "без боли и страха")]
    [InlineData("Looking into tooth extraction? I can tell you what to expect and how the procedure goes without pain or fear.", "without pain or fear")]
    [InlineData("Vous vous renseignez sur l'extraction dentaire ? Je peux vous expliquer à quoi vous attendre et comment se déroule la procédure sans douleur ni crainte.", "sans douleur ni crainte")]
    [InlineData("Εξετάζετε την εξαγωγή δοντιού; Μπορώ να σας πω τι να περιμένετε και πώς γίνεται η διαδικασία χωρίς πόνο και φόβο.", "χωρίς πόνο και φόβο")]
    [InlineData("هل تبحث عن خلع الأسنان؟ يمكنني إخبارك بما يمكن توقعه وكيف يتم الإجراء دون ألم أو خوف.", "دون ألم أو خوف")]
    public void Sanitize_RemovesAbsolutePainGuarantees(string legacy, string unsafePhrase)
    {
        var safe = DentaProactiveSafetyFilter.Sanitize(legacy);

        Assert.DoesNotContain(unsafePhrase, safe, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(legacy, safe);
    }

    [Theory]
    [InlineData("ru", "Вижу, вы на странице о лечении каналов. Это звучит страшно, но у нас это абсолютно безболезненно 🤍 Есть вопросы?", "абсолютно безболезненно")]
    [InlineData("el", "Βλέπω ότι είστε στη σελίδα για τη θεραπεία ρίζας. Ίσως ακούγεται τρομακτικό, αλλά με εμάς είναι εντελώς ανώδυνο 🤍 Έχετε ερωτήσεις;", "εντελώς ανώδυνο")]
    [InlineData("ar", "أرى أنك في صفحة علاج قناة الجذر. قد يبدو الأمر مخيفًا، لكنه غير مؤلم تمامًا لدينا 🤍 هل لديك أسئلة؟", "غير مؤلم تمامًا")]
    public void SanitizeSse_DecodesAndRewritesLocalizedDelta(string _, string legacy, string unsafePhrase)
    {
        var json = JsonSerializer.Serialize(new { delta = legacy });
        var sse = $"data: {json}\n\ndata: {JsonSerializer.Serialize(new { done = true })}\n\n";

        var safe = DentaProactiveSafetyFilter.SanitizeSse(sse);

        Assert.DoesNotContain(unsafePhrase, safe, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data: ", safe, StringComparison.Ordinal);
        Assert.Contains("\"done\":true", safe, StringComparison.Ordinal);
    }

    [Fact]
    public void Sanitize_LeavesUnrelatedCopyUntouched()
    {
        const string text = "Interested in cosmetic dentistry? I can tell you about whitening and veneers.";
        Assert.Equal(text, DentaProactiveSafetyFilter.Sanitize(text));
    }
}
