namespace DentalClinic.Services;

/// <summary>
/// Общий "шлагбаум" для запросов перевода к Gemini API.
///
/// Изначально здесь стоял лимит на количество ОДНОВРЕМЕННЫХ запросов, но
/// логи показали, что аккаунт упирается в 429 даже без всякой параллельности —
/// значит, у ключа очень низкая квота запросов В МИНУТУ (не одновременных).
/// Поэтому теперь запросы идут строго по одному (Gate на 1), и вдобавок между
/// концом одного запроса и началом следующего выдерживается минимальная пауза —
/// это не решает проблему, если квота исчерпана полностью на уровне аккаунта/дня,
/// но защищает от лишних 429 из-за слишком частых обращений.
/// </summary>
public static class GeminiTranslateLimiter
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly TimeSpan MinInterval = TimeSpan.FromMilliseconds(1500);
    private static DateTime _lastCallUtc = DateTime.MinValue;
    private static readonly object TimeLock = new();

    public static Task<T> RunAsync<T>(Func<Task<T>> action)
        => RunAsync(action, CancellationToken.None);

    public static async Task<T> RunAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        // A disconnected browser must not keep occupying the global translation gate
        // or wait out the spacing delay only to spend Gemini quota after the caller is
        // already gone. Propagate request cancellation through both waits.
        await Gate.WaitAsync(cancellationToken);
        try
        {
            TimeSpan waitFor;
            lock (TimeLock)
            {
                var earliestAllowed = _lastCallUtc + MinInterval;
                waitFor = earliestAllowed > DateTime.UtcNow ? earliestAllowed - DateTime.UtcNow : TimeSpan.Zero;
            }
            if (waitFor > TimeSpan.Zero)
                await Task.Delay(waitFor, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await action();
            }
            finally
            {
                lock (TimeLock) { _lastCallUtc = DateTime.UtcNow; }
            }
        }
        finally
        {
            Gate.Release();
        }
    }
}