using System.Data;
using DentalClinic.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DentalClinic.Services;

/// <summary>
/// Serializes writes that can introduce a Patient/Admin email identity. SQL Server
/// uses one transaction-owned application lock across all instances; non-relational
/// test stores use the same in-process gate. If a caller already owns a transaction
/// (AdminAccessService does), this guard joins it instead of nesting a transaction.
/// </summary>
public static class IdentityEmailGuard
{
    private const string SqlServerLockSql = """
DECLARE @lockResult int;
EXEC @lockResult = sys.sp_getapplock
    @Resource = N'DentalClinic.IdentityEmail',
    @LockMode = 'Exclusive',
    @LockOwner = 'Transaction',
    @LockTimeout = 5000;
IF @lockResult < 0
    THROW 51031, 'Unable to acquire cross-role identity email lock', 1;
""";

    private static readonly SemaphoreSlim ProcessGate = new(1, 1);

    public static async Task<T> ExecuteSerializedAsync<T>(
        ApplicationDbContext db,
        Func<Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        await ProcessGate.WaitAsync(cancellationToken);
        IDbContextTransaction? ownedTransaction = null;
        try
        {
            var hasExistingTransaction = db.Database.CurrentTransaction != null;

            if (!hasExistingTransaction && db.Database.IsSqlServer())
            {
                ownedTransaction = await db.Database.BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken);
            }
            else if (!hasExistingTransaction && db.Database.IsRelational())
            {
                ownedTransaction = await db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);
            }

            if (db.Database.IsSqlServer())
                await db.Database.ExecuteSqlRawAsync(SqlServerLockSql, cancellationToken);

            var result = await action();

            if (ownedTransaction != null)
                await ownedTransaction.CommitAsync(cancellationToken);

            return result;
        }
        catch
        {
            if (ownedTransaction != null)
                await ownedTransaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            if (ownedTransaction != null)
                await ownedTransaction.DisposeAsync();
            ProcessGate.Release();
        }
    }
}
