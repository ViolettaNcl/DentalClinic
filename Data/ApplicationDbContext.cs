using Microsoft.EntityFrameworkCore;
using DentalClinic.Models;

namespace DentalClinic.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Patient> Patients { get; set; }
        public DbSet<AppointmentRequest> AppointmentRequests { get; set; }
        public DbSet<Doctor> Doctors { get; set; }
        public DbSet<Admin> Admins { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<Service> Services { get; set; }
        public DbSet<ChatMessageLog> ChatMessageLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Email is normalized to lower-case by AuthController. Keep the DB as the final
            // authority too, so two concurrent registrations cannot create duplicates.
            modelBuilder.Entity<Patient>()
                .Property(p => p.Email)
                .HasMaxLength(320);
            modelBuilder.Entity<Patient>()
                .HasIndex(p => p.Email)
                .IsUnique();

            modelBuilder.Entity<Admin>()
                .Property(a => a.Email)
                .HasMaxLength(320);
            modelBuilder.Entity<Admin>()
                .HasIndex(a => a.Email)
                .IsUnique();

            // Optional links from appointments stay valid when a patient account or doctor
            // is removed: the historical appointment remains, but the reference becomes null.
            modelBuilder.Entity<AppointmentRequest>()
                .HasOne<Patient>()
                .WithMany()
                .HasForeignKey(a => a.PatientId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<AppointmentRequest>()
                .HasOne<Doctor>()
                .WithMany()
                .HasForeignKey(a => a.DoctorId)
                .OnDelete(DeleteBehavior.SetNull);

            // Review/notification belong to the patient's account and are removed with it.
            modelBuilder.Entity<Review>()
                .HasIndex(r => r.Status);
            modelBuilder.Entity<Review>()
                .HasOne<Patient>()
                .WithMany()
                .HasForeignKey(r => r.PatientId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Notification>()
                .HasIndex(n => new { n.PatientId, n.IsRead });
            modelBuilder.Entity<Notification>()
                .HasOne<Patient>()
                .WithMany()
                .HasForeignKey(n => n.PatientId)
                .OnDelete(DeleteBehavior.Cascade);

            // Chat logs may outlive account deletion for the configured retention period,
            // but must not keep a dangling PatientId.
            modelBuilder.Entity<ChatMessageLog>()
                .HasOne<Patient>()
                .WithMany()
                .HasForeignKey(c => c.PatientId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Service>()
                .HasIndex(s => new { s.Category, s.IsActive });

            // Supports range locking used by the Serializable scheduling transaction.
            modelBuilder.Entity<AppointmentRequest>()
                .HasIndex(a => new { a.DoctorId, a.AppointmentDate, a.Status });

            modelBuilder.Entity<AppointmentRequest>()
                .ToTable(table => table.HasCheckConstraint(
                    "CK_AppointmentRequests_Status",
                    "[Status] IN ('pending', 'confirmed', 'cancelled', 'completed')"));

            modelBuilder.Entity<ChatMessageLog>()
                .HasIndex(c => c.SessionId);
            modelBuilder.Entity<ChatMessageLog>()
                .HasIndex(c => c.CreatedAt);
        }
    }
}
