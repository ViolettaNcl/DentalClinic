using DentalClinic.Data;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Services;

public sealed class ChatRetentionService
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _configuration;

    public ChatRetentionService(ApplicationDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public async Task<ChatRetentionResult> CleanupAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var messageDays = Math.Clamp(_configuration.GetValue<int?>("Chat:MessageRetentionDays") ?? 30, 1, 365);
        var ipHours = Math.Clamp(_configuration.GetValue<int?>("Chat:IpRetentionHours") ?? 24, 1, 168);

        var ipCutoff = now.AddHours(-ipHours);
        var messageCutoff = now.AddDays(-messageDays);

        int ipsCleared;
        int messagesDeleted;

        if (_db.Database.IsRelational())
        {
            ipsCleared = await _db.ChatMessageLogs
                .Where(x => x.CreatedAt < ipCutoff && x.ClientIp != null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ClientIp, (string?)null), cancellationToken);

            messagesDeleted = await _db.ChatMessageLogs
                .Where(x => x.CreatedAt < messageCutoff)
                .ExecuteDeleteAsync(cancellationToken);
        }
        else
        {
            var oldIpRows = await _db.ChatMessageLogs
                .Where(x => x.CreatedAt < ipCutoff && x.ClientIp != null)
                .ToListAsync(cancellationToken);
            foreach (var row in oldIpRows) row.ClientIp = null;
            ipsCleared = oldIpRows.Count;

            var oldMessages = await _db.ChatMessageLogs
                .Where(x => x.CreatedAt < messageCutoff)
                .ToListAsync(cancellationToken);
            _db.ChatMessageLogs.RemoveRange(oldMessages);
            messagesDeleted = oldMessages.Count;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return new ChatRetentionResult(ipsCleared, messagesDeleted, ipHours, messageDays);
    }
}

public sealed record ChatRetentionResult(
    int IpPseudonymsCleared,
    int MessagesDeleted,
    int IpRetentionHours,
    int MessageRetentionDays);
