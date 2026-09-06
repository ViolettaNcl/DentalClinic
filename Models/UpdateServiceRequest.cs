namespace DentalClinic.Models;

// Тот же паттерн, что и UpdateDoctorRequest: все поля необязательные (nullable),
// чтобы отличить "клиент не передал это поле" от "клиент явно передал 0/пусто".
// Раньше Update у услуг принимал прямо Service (сущность БД) — и если клиент
// присылал JSON без priceFrom, это поле десериализовалось в 0 по умолчанию
// (decimal не nullable), а проверка "req.PriceFrom >= 0" пропускала 0 как
// валидное значение — цена молча обнулялась. С этим DTO такого не произойдёт:
// не передали PriceFrom — PriceFrom будет null, и мы просто не тронем цену.
public class UpdateServiceRequest
{
    public string? Category { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public decimal? PriceFrom { get; set; }
    public decimal? PriceTo { get; set; }
    // JSON null и отсутствующее nullable-поле неотличимы при обычном model binding,
    // поэтому очистка верхней границы цены задаётся явно.
    public bool? ClearPriceTo { get; set; }
    public string? Unit { get; set; }
    public string? Keywords { get; set; }
    public string? PageUrl { get; set; }
    public int? SortOrder { get; set; }
    public bool? IsActive { get; set; }
}
