import { readFile, writeFile } from 'node:fs/promises';

async function replaceOnce(path, before, after, marker) {
  let source = await readFile(path, 'utf8');
  const count = source.split(before).length - 1;
  if (count === 1) {
    source = source.replace(before, after);
    await writeFile(path, source, 'utf8');
    return;
  }
  if (count === 0 && source.includes(marker)) return;
  throw new Error(`Unexpected source shape in ${path}; old-block occurrences=${count}.`);
}

await replaceOnce(
  'Services/NotificationService.cs',
`    public async Task NotifyAsync(
        int patientId,
        string type,
        string message,
        int? relatedId = null,
        CancellationToken cancellationToken = default)
    {
        var notification = CreateNotification(patientId, type, message, relatedId, idempotencyKey: null);

        // Persistence is the durable source of truth for patient notifications.
        // Realtime delivery is only an optimization for an already-open browser tab.
        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync(cancellationToken);
        await DeliverPatientRealtimeBestEffortAsync(notification, cancellationToken);
    }
`,
`    public async Task NotifyAsync(
        int patientId,
        string type,
        string message,
        int? relatedId = null,
        CancellationToken cancellationToken = default)
    {
        var notification = await PersistPatientNotificationAsync(
            patientId,
            type,
            message,
            relatedId,
            cancellationToken);
        await DeliverPersistedPatientRealtimeBestEffortAsync(notification, cancellationToken);
    }

    /// <summary>
    /// Persists a durable patient notification without emitting realtime delivery.
    /// Transactional callers can save this inside their database transaction, commit
    /// the primary state, and only then publish the already-persisted notification.
    /// </summary>
    public async Task<Notification> PersistPatientNotificationAsync(
        int patientId,
        string type,
        string message,
        int? relatedId = null,
        CancellationToken cancellationToken = default)
    {
        var notification = CreateNotification(patientId, type, message, relatedId, idempotencyKey: null);
        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync(cancellationToken);
        return notification;
    }

    /// <summary>
    /// Best-effort realtime delivery for a notification whose durable database state
    /// has already been persisted (and, for transactional callers, committed).
    /// </summary>
    public Task DeliverPersistedPatientRealtimeBestEffortAsync(
        Notification notification,
        CancellationToken cancellationToken = default)
        => DeliverPatientRealtimeBestEffortAsync(notification, cancellationToken);
`,
  'PersistPatientNotificationAsync');

await replaceOnce(
  'Controllers/ReviewController.cs',
`            // NotificationService uses the same scoped DbContext, so on relational
            // providers both the moderation state and durable patient notification
            // participate in this transaction. Realtime delivery remains best-effort.
            await _notifications.NotifyAsync(
                review.PatientId,
                status == "approved" ? "review_approved" : "review_rejected",
                message,
                review.Id,
                cancellationToken);

            if (transaction != null)
                await transaction.CommitAsync(cancellationToken);
`,
`            // Persist the patient notification in the same database transaction as
            // the moderation decision, but do not emit realtime before commit. A
            // failed commit must never leave the patient with a transient false event.
            var notification = await _notifications.PersistPatientNotificationAsync(
                review.PatientId,
                status == "approved" ? NotificationTypes.ReviewApproved : NotificationTypes.ReviewRejected,
                message,
                review.Id,
                cancellationToken);

            if (transaction != null)
                await transaction.CommitAsync(cancellationToken);

            await _notifications.DeliverPersistedPatientRealtimeBestEffortAsync(
                notification,
                cancellationToken);
`,
  'DeliverPersistedPatientRealtimeBestEffortAsync');

console.log('Review moderation realtime delivery moved after durable transaction commit.');
