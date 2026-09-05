using DentalClinic.Data;
using DentalClinic.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class DatabaseModelInvariantTests
{
    [Fact]
    public void IdentityEmails_AreBoundedAndUnique()
    {
        using var db = CreateContext();

        AssertUniqueBoundedEmail(db.Model.FindEntityType(typeof(Patient))!);
        AssertUniqueBoundedEmail(db.Model.FindEntityType(typeof(Admin))!);
    }

    [Fact]
    public void AppointmentRelationships_PreserveHistoryWhenPatientOrDoctorIsRemoved()
    {
        using var db = CreateContext();
        var appointment = db.Model.FindEntityType(typeof(AppointmentRequest))!;

        var patientFk = FindForeignKey(appointment, nameof(AppointmentRequest.PatientId));
        var doctorFk = FindForeignKey(appointment, nameof(AppointmentRequest.DoctorId));

        Assert.Equal(DeleteBehavior.SetNull, patientFk.DeleteBehavior);
        Assert.Equal(DeleteBehavior.SetNull, doctorFk.DeleteBehavior);
    }

    [Fact]
    public void PatientOwnedReviewsAndNotifications_AreDeletedWithPatient()
    {
        using var db = CreateContext();

        var review = db.Model.FindEntityType(typeof(Review))!;
        var notification = db.Model.FindEntityType(typeof(Notification))!;

        Assert.Equal(DeleteBehavior.Cascade, FindForeignKey(review, nameof(Review.PatientId)).DeleteBehavior);
        Assert.Equal(DeleteBehavior.Cascade, FindForeignKey(notification, nameof(Notification.PatientId)).DeleteBehavior);
    }

    [Fact]
    public void ChatLogs_PreserveAnonymousHistoryWhenPatientIsRemoved()
    {
        using var db = CreateContext();
        var chat = db.Model.FindEntityType(typeof(ChatMessageLog))!;

        Assert.Equal(DeleteBehavior.SetNull, FindForeignKey(chat, nameof(ChatMessageLog.PatientId)).DeleteBehavior);
    }

    [Fact]
    public void OperationalQueries_HaveExpectedIndexes()
    {
        using var db = CreateContext();

        AssertIndex(
            db.Model.FindEntityType(typeof(AppointmentRequest))!,
            nameof(AppointmentRequest.DoctorId),
            nameof(AppointmentRequest.AppointmentDate),
            nameof(AppointmentRequest.Status));

        AssertIndex(
            db.Model.FindEntityType(typeof(Notification))!,
            nameof(Notification.PatientId),
            nameof(Notification.IsRead));

        AssertIndex(
            db.Model.FindEntityType(typeof(Service))!,
            nameof(Service.Category),
            nameof(Service.IsActive));

        AssertIndex(db.Model.FindEntityType(typeof(ChatMessageLog))!, nameof(ChatMessageLog.SessionId));
        AssertIndex(db.Model.FindEntityType(typeof(ChatMessageLog))!, nameof(ChatMessageLog.CreatedAt));
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"model-invariants-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    }

    private static void AssertUniqueBoundedEmail(IEntityType entity)
    {
        var email = entity.FindProperty("Email");
        Assert.NotNull(email);
        Assert.Equal(320, email!.GetMaxLength());
        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique
            && index.Properties.Count == 1
            && index.Properties[0].Name == "Email");
    }

    private static IForeignKey FindForeignKey(IEntityType entity, string propertyName) =>
        Assert.Single(entity.GetForeignKeys().Where(fk =>
            fk.Properties.Count == 1 && fk.Properties[0].Name == propertyName));

    private static void AssertIndex(IEntityType entity, params string[] properties)
    {
        Assert.Contains(entity.GetIndexes(), index =>
            index.Properties.Select(p => p.Name).SequenceEqual(properties));
    }
}
