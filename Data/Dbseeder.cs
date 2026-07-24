using DentalClinic.Models;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(ApplicationDbContext db)
        {
            if (!await db.Doctors.AnyAsync())
            {
                db.Doctors.AddRange(
                    new Doctor { FullName = "Раис Наджиб", Specialization = "терапия, импланты", ExperienceYears = 2, IsActive = true },
                    new Doctor { FullName = "Лилит Рейнер", Specialization = "импланты, хирургия (100+ операций)", ExperienceYears = 5, IsActive = true }
                );
            }

            if (!await db.Services.AnyAsync())
            {
                db.Services.AddRange(
                    // Косметика
                    new Service { Category = "Косметика", Name = "Отбеливание ZOOM", PriceFrom = 9000, PageUrl = "/pages/services/cosmetic-treatments.html", Keywords = "отбелив,zoom", SortOrder = 1 },
                    new Service { Category = "Косметика", Name = "Виниры", PriceFrom = 20000, Unit = "зуб", PageUrl = "/pages/services/cosmetic-treatments.html", Keywords = "винир", SortOrder = 2 },
                    new Service { Category = "Косметика", Name = "Реставрация", PriceFrom = 6000, PageUrl = "/pages/services/cosmetic-treatments.html", Keywords = "реставрац", SortOrder = 3 },
                    new Service { Category = "Косметика", Name = "Голливудская улыбка", PriceFrom = 180000, PageUrl = "/pages/services/cosmetic-treatments.html", Keywords = "голливуд,улыбк", SortOrder = 4 },

                    // Пломбы
                    new Service { Category = "Пломбы", Name = "Пломба", PriceFrom = 2500, PriceTo = 5000, Description = "гарантия до 3 лет", PageUrl = "/pages/services/fillings.html", Keywords = "пломб", SortOrder = 1 },

                    // Коронки
                    new Service { Category = "Коронки", Name = "Металлокерамика", PriceFrom = 12000, PageUrl = "/pages/services/crowns.html", Keywords = "коронк,металлокерамик", SortOrder = 1 },
                    new Service { Category = "Коронки", Name = "Цирконий", PriceFrom = 18000, PageUrl = "/pages/services/crowns.html", Keywords = "коронк,цирконий", SortOrder = 2 },
                    new Service { Category = "Коронки", Name = "Золото", PriceFrom = 25000, PageUrl = "/pages/services/crowns.html", Keywords = "коронк,золот", SortOrder = 3 },

                    // Импланты
                    new Service { Category = "Импланты", Name = "Стандарт", PriceFrom = 35000, PageUrl = "/pages/services/implants.html", Keywords = "имплант", SortOrder = 1 },
                    new Service { Category = "Импланты", Name = "Премиум + коронка", PriceFrom = 55000, PageUrl = "/pages/services/implants.html", Keywords = "имплант,премиум", SortOrder = 2 },
                    new Service { Category = "Импланты", Name = "All-on-4/6", PriceFrom = 250000, PageUrl = "/pages/services/implants.html", Keywords = "all-on,имплант", SortOrder = 3 },

                    // Каналы
                    new Service { Category = "Каналы", Name = "Стандарт", PriceFrom = 4000, Unit = "канал", PageUrl = "/pages/services/root-canal.html", Keywords = "канал,нерв,эндодонт", SortOrder = 1 },
                    new Service { Category = "Каналы", Name = "Микроскоп", PriceFrom = 7000, PageUrl = "/pages/services/root-canal.html", Keywords = "канал,микроскоп", SortOrder = 2 },
                    new Service { Category = "Каналы", Name = "Перелечивание", PriceFrom = 9000, PageUrl = "/pages/services/root-canal.html", Keywords = "перелечив,канал", SortOrder = 3 },

                    // Мосты
                    new Service { Category = "Мосты", Name = "Металлокерамика", PriceFrom = 45000, PageUrl = "/pages/services/bridges.html", Keywords = "мост", SortOrder = 1 },
                    new Service { Category = "Мосты", Name = "Керамика", PriceFrom = 65000, PageUrl = "/pages/services/bridges.html", Keywords = "мост,керамик", SortOrder = 2 },
                    new Service { Category = "Мосты", Name = "На имплантах", PriceFrom = 120000, PageUrl = "/pages/services/bridges.html", Keywords = "мост,имплант", SortOrder = 3 },

                    // Удаление
                    new Service { Category = "Удаление", Name = "Простое", PriceFrom = 7500, PageUrl = "/pages/services/extractions.html", Keywords = "удален,экстракц", SortOrder = 1 },
                    new Service { Category = "Удаление", Name = "Сложное", PriceFrom = 14000, PageUrl = "/pages/services/extractions.html", Keywords = "удален,сложн", SortOrder = 2 },
                    new Service { Category = "Удаление", Name = "Детское", PriceFrom = 5500, Description = "подарок от зубной феи!", PageUrl = "/pages/services/extractions.html", Keywords = "удален,детск", SortOrder = 3 },

                    // Протезы
                    new Service { Category = "Протезы", Name = "Акрил", PriceFrom = 25000, PageUrl = "/pages/services/prosthetics.html", Keywords = "протез,акрил", SortOrder = 1 },
                    new Service { Category = "Протезы", Name = "Нейлон", PriceFrom = 40000, PageUrl = "/pages/services/prosthetics.html", Keywords = "протез,нейлон", SortOrder = 2 },
                    new Service { Category = "Протезы", Name = "Бюгельные", PriceFrom = 55000, PageUrl = "/pages/services/prosthetics.html", Keywords = "протез,бюгель", SortOrder = 3 }
                );
            }

            await db.SaveChangesAsync();
        }
    }
}