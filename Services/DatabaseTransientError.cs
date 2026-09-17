using Microsoft.Data.SqlClient;

namespace DentalClinic.Services;

/// <summary>
/// Shared classification for short-lived SQL connectivity failures. The remote
/// development SQL host can occasionally time out during the pre-login handshake;
/// those failures should be reported as temporary availability problems rather than
/// opaque HTTP 500 responses.
/// </summary>
public static class DatabaseTransientError
{
    private static readonly HashSet<int> TransientSqlNumbers = new()
    {
        -2,     // timeout
        20,     // instance does not support encryption / transport level
        53,     // network path / server not found
        64,     // network name no longer available
        233,    // no process on other end of pipe
        4060,   // cannot open database (can be transient during startup)
        10053,
        10054,
        10060,
        10928,
        10929,
        40197,
        40501,
        40613,
        49918,
        49919,
        49920
    };

    public static bool IsTransient(Exception? exception)
    {
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (current is TimeoutException)
                return true;

            if (current is SqlException sqlException)
            {
                foreach (SqlError error in sqlException.Errors)
                {
                    if (TransientSqlNumbers.Contains(error.Number))
                        return true;
                }

                var sqlMessage = sqlException.Message;
                if (ContainsTransientText(sqlMessage))
                    return true;
            }

            if (current is InvalidOperationException
                && ContainsTransientText(current.Message))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsTransientText(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;

        return message.Contains("transient failure", StringComparison.OrdinalIgnoreCase)
            || message.Contains("pre-login handshake", StringComparison.OrdinalIgnoreCase)
            || message.Contains("wait operation timed out", StringComparison.OrdinalIgnoreCase)
            || message.Contains("timeout", StringComparison.OrdinalIgnoreCase)
            || message.Contains("temporarily unavailable", StringComparison.OrdinalIgnoreCase)
            || message.Contains("transport-level error", StringComparison.OrdinalIgnoreCase);
    }
}
