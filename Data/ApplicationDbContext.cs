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

            // Emails are normalized to lowercase by AuthController. The unique indexes
            // provide the database-level guarantee needed under concurrent registration.
            modelBuilder.Entity<Patient>()
                .HasIndex(p => p.Email)
                .IsUnique();

            modelBuilder.Entity<Admin>()
                .HasIndex(a => a.Email)
                .IsUnique();

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

            // Appointment history should survive deletion of an account/doctor, while
            // avoiding dangling foreign keys. The nullable IDs are cleared instead.
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

            modelBuilder.Entity<Service>()
                .HasIndex(s => new { s.Category, s.IsActive });

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
