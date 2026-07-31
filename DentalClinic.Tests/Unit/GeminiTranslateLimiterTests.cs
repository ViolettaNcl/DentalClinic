using DentalClinic.Services;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class GeminiTranslateLimiterTests
{
    [Fact]
    public async Task RunAsync_ReturnsActionResult()
    {
        var result = await GeminiTranslateLimiter.RunAsync(() => Task.FromResult(42));

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task RunAsync_NeverRunsTwoActionsConcurrently()
    {
        // Лимитер существует именно чтобы не бить по Gemini API параллельно —
        // проверяем, что второй вызов физически не может начаться, пока
        // первый ещё выполняется.
        var running = 0;
        var maxObservedConcurrency = 0;
        var gate = new object();

        async Task<int> Slow()
        {
            lock (gate)
            {
                running++;
                maxObservedConcurrency = Math.Max(maxObservedConcurrency, running);
            }
            await Task.Delay(150);
            lock (gate) { running--; }
            return 1;
        }

        var t1 = GeminiTranslateLimiter.RunAsync(Slow);
        var t2 = GeminiTranslateLimiter.RunAsync(Slow);
        await Task.WhenAll(t1, t2);

        Assert.Equal(1, maxObservedConcurrency);
    }
}
