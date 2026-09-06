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

    [Fact]
    public async Task RunAsync_PreCancelledRequest_DoesNotInvokeProviderAction()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var invoked = false;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            GeminiTranslateLimiter.RunAsync(
                () =>
                {
                    invoked = true;
                    return Task.FromResult(1);
                },
                cts.Token));

        Assert.False(invoked);
    }

    [Fact]
    public async Task RunAsync_CancelledWaiter_DoesNotRunAfterGateIsReleased()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var holder = GeminiTranslateLimiter.RunAsync(async () =>
        {
            entered.SetResult();
            await release.Task;
            return 1;
        });

        await entered.Task;

        using var cts = new CancellationTokenSource();
        var secondInvoked = false;
        var waiter = GeminiTranslateLimiter.RunAsync(
            () =>
            {
                secondInvoked = true;
                return Task.FromResult(2);
            },
            cts.Token);

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiter);

        release.SetResult();
        await holder;

        Assert.False(secondInvoked);
    }
}
