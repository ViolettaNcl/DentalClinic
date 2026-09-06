using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DentalClinic.Models;

// Услуга клиники с ценой — источник данных для прайса на сайте И для
// AI-ассистента (Дента). Раньше цены были зашиты прямо в C#-строку системного
// промпта чат-бота: любое изменение прайса требовало правки кода, пересборки
// и деплоя бэкенда. Теперь это обычные строки в БД, которые правит админ
// через панель — бот подхватывает изменения сам (см. ChatKnowledgeService).
[Table("Services")]
public class Service
{
    [Key]
    public int Id { get; set; }

    // Группа в прайсе, например "Импланты", "Коронки", "Пломбы"
    [Required, StringLength(100)]
    public string Category { get; set; } = null!;

    // Название конкретной позиции, например "Имплант стандарт"
    [Required, StringLength(200)]
    public string Name { get; set; } = null!;

    [StringLength(500)]
    public string? Description { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal PriceFrom { get; set; }

    // Верхняя граница цены, если указывается диапазон (необязательно)
    [Column(TypeName = "decimal(10,2)")]
    public decimal? PriceTo { get; set; }

    // Единица тарификации: "зуб", "канал", "челюсть" и т.п. (необязательно)
    [StringLength(30)]
    public string? Unit { get; set; }

    // Слова, по которым бот ищет эту услугу в вопросе пациента и подставляет
    // ссылку на страницу услуги (аналог простого retrieval для RAG)
    [StringLength(300)]
    public string? Keywords { get; set; }

    [StringLength(300)]
    public string? PageUrl { get; set; }

    public bool IsActive { get; set; } = true;

    // 0 = не привязывать к фиксированной карточке на detail-странице;
    // положительные значения задают стабильный слот карточки и порядок в прайсе.
    [Range(0, int.MaxValue)]
    public int SortOrder { get; set; } = 0;
}