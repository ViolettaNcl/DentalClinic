using System.Data;
using DentalClinic.Data;
using DentalClinic.Models;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Services;

public enum AdminAccessError
{
    None,
    Forbidden,
    NotFound,
    DuplicateEmail,
    PatientEmailConflict,
    LastSuperAdmin,
    CannotDeleteSelf
}

public sealed record AdminAccessSummary(
    int Id,
    string Email,
    bool IsSuperAdmin,
    DateTime CreatedAt);

public sealed record AdminAccessOperation(
    AdminAccessError Error,
    AdminAccessSummary? Admin = null)
{
    public bool Succeeded => Error == AdminAccessError.None;
}

/// <summary>
/// Owns administrator-account mutations and the invariant that an installation which
/// already has a super-admin can never lose its final super-admin through the API.
/// Mutations are serialized across Vercel/container instances with a SQL Server
/// application lock; non-relational test stores use an in-process gate.
/// </summary>
public sealed class AdminAccessService
{
    private const string SqlServerLockSql = """
DECLARE @lockResult int;
EXEC @lockResult = sys.sp_getapplock
    @Resource = N'DentalClinic.AdminAccess',
    @LockMode = 'Exclusive',
    @LockOwner = 'Transaction',
    @LockTimeout = 5000;
IF @lockResult < 0
    THROW 51000, 'Unable to acquire administrator access lock', 1;
""";

    private static readonly SemaphoreSlim ProcessGate = new(1, 1);
    private readonly ApplicationDbContext _db;

    public AdminAccessService(ApplicationDbContext db)
    {
        _db = db;
    }

    public Task<bool> IsSuperAdminAsync(int adminId, CancellationToken cancellationToken = default)
        => _db.Admins.AsNoTracking().AnyAsync(
            admin => admin.Id == adminId && admin.IsSuperAdmin,
            cancellationToken);

    public async Task<IReadOnlyList<AdminAccessSummary>> ListAsync(
        CancellationToken cancellationToken = default)
        => await _db.Admins
            .AsNoTracking()
            .OrderByDescending(admin => admin.IsSuperAdmin)
            .ThenBy(admin => admin.CreatedAt)
            .ThenBy(admin => admin.Id)
            .Select(admin => new AdminAccessSummary(
                admin.Id,
                admin.Email,
                admin.IsSuperAdmin,
                admin.CreatedAt))
            .ToListAsync(cancellationToken);

    public Task<AdminAccessOperation> CreateAsync(
        int actorAdminId,
        string email,
        string password,
        bool isSuperAdmin,
        CancellationToken cancellationToken = default)
        => ExecuteSerializedAsync(async () =>
        {
            if (!await ActorIsSuperAdminAsync(actorAdminId, cancellationToken))
                return new AdminAccessOperation(AdminAccessError.Forbidden);

            return await IdentityEmailGuard.ExecuteSerializedAsync(_db, async () =>
            {
                var normalizedEmail = NormalizeEmail(email);
                if (await _db.Admins.AnyAsync(a => a.Email == normalizedEmail, cancellationToken))
                    return new AdminAccessOperation(AdminAccessError.DuplicateEmail);

                if (await _db.Patients.AnyAsync(p => p.Email == normalizedEmail, cancellationToken))
                    return new AdminAccessOperation(AdminAccessError.PatientEmailConflict);

                var admin = new Admin
                {
                    Email = normalizedEmail,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                    IsSuperAdmin = isSuperAdmin
                };

                _db.Admins.Add(admin);
                try
                {
                    await _db.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException)
                {
                    _db.Entry(admin).State = EntityState.Detached;

                    // Confirm the exact invariant which lost a race. Do not turn
                    // an unrelated storage outage or constraint failure into a
                    // misleading duplicate-email response.
                    if (await _db.Admins
                        .AsNoTracking()
                        .AnyAsync(a => a.Email == normalizedEmail, cancellationToken))
                    {
                        return new AdminAccessOperation(AdminAccessError.DuplicateEmail);
                    }

                    if (await _db.Patients
                        .AsNoTracking()
                        .AnyAsync(p => p.Email == normalizedEmail, cancellationToken))
                    {
                        return new AdminAccessOperation(AdminAccessError.PatientEmailConflict);
                    }

                    throw;
                }

                return new AdminAccessOperation(AdminAccessError.None, ToSummary(admin));
            }, cancellationToken);
        }, cancellationToken);

    public Task<AdminAccessOperation> SetSuperAdminAsync(
        int actorAdminId,
        int targetAdminId,
        bool isSuperAdmin,
        CancellationToken cancellationToken = default)
        => ExecuteSerializedAsync(async () =>
        {
            if (!await ActorIsSuperAdminAsync(actorAdminId, cancellationToken))
                return new AdminAccessOperation(AdminAccessError.Forbidden);

            var target = await _db.Admins.SingleOrDefaultAsync(
                admin => admin.Id == targetAdminId,
                cancellationToken);
            if (target == null)
                return new AdminAccessOperation(AdminAccessError.NotFound);

            if (target.IsSuperAdmin == isSuperAdmin)
                return new AdminAccessOperation(AdminAccessError.None, ToSummary(target));

            if (!isSuperAdmin && target.IsSuperAdmin)
            {
                var superAdminCount = await _db.Admins.CountAsync(
                    admin => admin.IsSuperAdmin,
                    cancellationToken);
                if (superAdminCount <= 1)
                    return new AdminAccessOperation(AdminAccessError.LastSuperAdmin);
            }

            target.IsSuperAdmin = isSuperAdmin;
            target.TokenVersion = checked(target.TokenVersion + 1);
            await _db.SaveChangesAsync(cancellationToken);

            return new AdminAccessOperation(AdminAccessError.None, ToSummary(target));
        }, cancellationToken);

    public Task<AdminAccessOperation> ResetPasswordAsync(
        int actorAdminId,
        int targetAdminId,
        string newPassword,
        CancellationToken cancellationToken = default)
        => ExecuteSerializedAsync(async () =>
        {
            if (!await ActorIsSuperAdminAsync(actorAdminId, cancellationToken))
                return new AdminAccessOperation(AdminAccessError.Forbidden);

            var target = await _db.Admins.SingleOrDefaultAsync(
                admin => admin.Id == targetAdminId,
                cancellationToken);
            if (target == null)
                return new AdminAccessOperation(AdminAccessError.NotFound);

            target.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            target.TokenVersion = checked(target.TokenVersion + 1);
            await _db.SaveChangesAsync(cancellationToken);

            return new AdminAccessOperation(AdminAccessError.None, ToSummary(target));
        }, cancellationToken);

    public Task<AdminAccessOperation> DeleteAsync(
        int actorAdminId,
        int targetAdminId,
        CancellationToken cancellationToken = default)
        => ExecuteSerializedAsync(async () =>
        {
            if (!await ActorIsSuperAdminAsync(actorAdminId, cancellationToken))
                return new AdminAccessOperation(AdminAccessError.Forbidden);

            if (actorAdminId == targetAdminId)
                return new AdminAccessOperation(AdminAccessError.CannotDeleteSelf);

            var target = await _db.Admins.SingleOrDefaultAsync(
                admin => admin.Id == targetAdminId,
                cancellationToken);
            if (target == null)
                return new AdminAccessOperation(AdminAccessError.NotFound);

            if (target.IsSuperAdmin)
            {
                var superAdminCount = await _db.Admins.CountAsync(
                    admin => admin.IsSuperAdmin,
                    cancellationToken);
                if (superAdminCount <= 1)
                    return new AdminAccessOperation(AdminAccessError.LastSuperAdmin);
            }

            var summary = ToSummary(target);
            _db.Admins.Remove(target);
            await _db.SaveChangesAsync(cancellationToken);
            return new AdminAccessOperation(AdminAccessError.None, summary);
        }, cancellationToken);

    private Task<bool> ActorIsSuperAdminAsync(int actorAdminId, CancellationToken cancellationToken)
        => _db.Admins.AnyAsync(
            admin => admin.Id == actorAdminId && admin.IsSuperAdmin,
            cancellationToken);

    private async Task<T> ExecuteSerializedAsync<T>(
        Func<Task<T>> action,
        CancellationToken cancellationToken)
    {
        await ProcessGate.WaitAsync(cancellationToken);
        try
        {
            if (_db.Database.IsSqlServer())
            {
                await using var transaction = await _db.Database.BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken);
                await _db.Database.ExecuteSqlRawAsync(SqlServerLockSql, cancellationToken);
                var result = await action();
                await transaction.CommitAsync(cancellationToken);
                return result;
            }

            if (_db.Database.IsRelational())
            {
                await using var transaction = await _db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);
                var result = await action();
                await transaction.CommitAsync(cancellationToken);
                return result;
            }

            return await action();
        }
        finally
        {
            ProcessGate.Release();
        }
    }

    private static string NormalizeEmail(string email)
        => email.Trim().ToLowerInvariant();

    private static AdminAccessSummary ToSummary(Admin admin)
        => new(admin.Id, admin.Email, admin.IsSuperAdmin, admin.CreatedAt);
}
