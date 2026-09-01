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

            // Индекс под модерацию отзывов админом (фильтр "только Pending")
            modelBuilder.Entity<Review>()
                .HasIndex(r => r.Status);

            // Каскадное удаление: если пациент удаляется из БД, его отзывы
            // не должны оставаться "осиротевшими" записями без владельца
            modelBuilder.Entity<Review>()
                .HasOne<Patient>()
                .WithMany()
                .HasForeignKey(r => r.PatientId)
                .OnDelete(DeleteBehavior.Cascade);

            // Составной индекс под самый частый запрос колокольчика уведомлений:
            // "непрочитанные уведомления конкретного пациента"
            modelBuilder.Entity<Notification>()
                .HasIndex(n => new { n.PatientId, n.IsRead });

            modelBuilder.Entity<Notification>()
                .HasOne<Patient>()
                .WithMany()
                .HasForeignKey(n => n.PatientId)
                .OnDelete(DeleteBehavior.Cascade);

            // Ускоряет публичную страницу услуг: выборка активных услуг по категории
            modelBuilder.Entity<Service>()
                .HasIndex(s => new { s.Category, s.IsActive });

            // Индекс нужен не только для календаря врача, но и для диапазонной
            // проверки конфликтов под Serializable-транзакцией. Благодаря ему
            // SQL Server блокирует узкий диапазон слотов конкретного врача.
            modelBuilder.Entity<AppointmentRequest>()
                .HasIndex(a => new { a.DoctorId, a.AppointmentDate, a.Status });

            modelBuilder.Entity<AppointmentRequest>()
                .ToTable(table => table.HasCheckConstraint(
                    "CK_AppointmentRequests_Status",
                    "[Status] IN ('pending', 'confirmed', 'cancelled', 'completed')"));

            // SessionId — быстрый поиск истории конкретного диалога с ботом;
            // CreatedAt — под очистку/выборку логов чата по дате (см. Stalependingcleanupservice)
            modelBuilder.Entity<ChatMessageLog>()
                .HasIndex(c => c.SessionId);

            modelBuilder.Entity<ChatMessageLog>()
                .HasIndex(c => c.CreatedAt);
        }
    }
}
