using DentalClinic.BackgroundJobs;
using DentalClinic.Data;
using DentalClinic.HealthChecks;
using DentalClinic.Hubs;
using DentalClinic.Services;
using DentalClinic.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ================= DB =================
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ================= Health checks =================
// /health проверяет, что процесс жив И что есть соединение с БД —
// этого достаточно для аптайм-мониторинга (UptimeRobot, healthcheck в Docker/оркестраторе).
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("db");

// ================= Уведомления + фоновые задачи =================
builder.Services.AddScoped<NotificationService>();
builder.Services.AddHostedService<AppointmentReminderService>();
builder.Services.AddHostedService<StalePendingCleanupService>();

// ================= AI-ассистент (Дента): кэш + сервис знаний =================
// ChatKnowledgeService на лету собирает актуальные цены и врачей из БД для
// системного промпта чат-бота — вместо того чтобы хранить их прямо в коде.
builder.Services.AddMemoryCache();
builder.Services.AddScoped<ChatKnowledgeService>();

// ================= SignalR (realtime уведомления) =================
// Заменяет опрос /api/notification каждые 60 секунд живым push-соединением:
// колокольчик обновляется мгновенно, когда админ подтверждает/отменяет запись
// или когда пациент подаёт новую заявку/отзыв (см. NotificationHub).
builder.Services.AddSignalR();

// ================= JWT =================
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key не задан в конфигурации");
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

builder.Services.AddSingleton<JwtTokenService>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero
    };

    // SignalR (WebSocket) не может передать заголовок Authorization при подключении,
    // поэтому клиент шлёт токен как ?access_token=... — читаем его тут только для
    // запросов к хабу уведомлений, остальные API как и раньше требуют заголовок.
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/notifications"))
                context.Token = accessToken;
            return Task.CompletedTask;
        }
    };
});

// ================= CORS =================
// Список разрешённых доменов берём из appsettings.json (секция "AllowedOrigins").
// Для локальной разработки там localhost, для продакшена — впишите туда реальный
// домен вашего сайта (например "https://dentalclinic.ru").
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5000", "https://localhost:5001" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod());
});

// ================= Rate Limiting =================
// Ограничиваем частые запросы (защита от спама/скриптов и накрутки трат на AI):
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("AppointmentCreate", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));

    // Вход/регистрация — не более 8 попыток в минуту с одного IP (защита от перебора паролей)
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 8,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // Чат с AI — не более 15 сообщений в минуту с одного IP (защита от накрутки
    // трат на платный Gemini API — запрос без лимита мог долбить его в цикле)
    options.AddPolicy("chat", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 15,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // Перевод текста отзывов — до 40 запросов в минуту с одного IP.
    // Лимит выше, чем у чата, т.к. при открытии страницы с отзывами
    // может понадобиться перевести сразу несколько карточек одновременно,
    // а результаты дальше кэшируются на сервере.
    options.AddPolicy("translate", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 40,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            "{\"message\":\"Слишком много запросов с вашего IP. Попробуйте через минуту.\"}", token);
    };
});

// ================= Controllers =================
builder.Services.AddHttpClient();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ================= Сжатие ответов =================
// Ускоряет отдачу JSON от API и статических файлов (HTML/CSS/JS) конечным
// пользователям — особенно заметно на мобильном интернете.
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

var app = builder.Build();

// ================= MIDDLEWARE =================

// Глобальный перехват необработанных исключений — вместо страницы ошибки ASP.NET
// клиент получает аккуратный JSON, а сама ошибка попадает в лог.
app.UseExceptionHandler(errApp =>
{
    errApp.Run(async context =>
    {
        var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("GlobalException");
        if (feature?.Error != null)
            logger.LogError(feature.Error, "Необработанное исключение на {Path}", context.Request.Path);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsync("{\"message\":\"Произошла внутренняя ошибка сервера\"}");
    });
});

if (!app.Environment.IsDevelopment())
{
    // HSTS: говорит браузеру всегда обращаться к сайту только по HTTPS
    // (защита от downgrade-атак). Включаем только на проде — на localhost
    // с самоподписанным сертификатом это будет мешать разработке.
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseResponseCompression();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();

app.UseCors("AllowFrontend");

// В Development лимитер отключаем целиком: интеграционные тесты поднимают
// приложение через WebApplicationFactory и держат ОДИН и тот же host на все
// тест-методы класса — а значит и один и тот же счётчик "auth"-лимита (8/мин),
// который реальные пользователи в проде никогда не делят между собой. Заодно
// это удобно и для ручной локальной отладки — не упереться в лимит, дёргая
// эндпоинты один за другим из Swagger/Postman.
if (!app.Environment.IsDevelopment())
{
    app.UseRateLimiter();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");

// Не проверяем полную готовность (миграции/сидинг) — только то, что процесс
// жив и есть соединение с БД. Без [Authorize] и без rate limiting намеренно:
// это должен быть самый дешёвый и всегда доступный запрос для мониторинга.
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthCheckJsonWriter.WriteResponse
});

// ================= Первичное заполнение прайса и врачей =================
// При первом запуске после обновления (пустые таблицы Services/Doctors)
// заполняем их тем же прайсом и врачами, что раньше были зашиты в промпте
// чат-бота — чтобы после миграции сайт и бот не остались без данных.
// Дальше это редактируется через панель администратора, а не через код.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await DentalClinic.Data.DbSeeder.SeedAsync(db);
}

app.Run();

// Топ-level statements генерируют класс Program как internal — этого достаточно
// для запуска приложения, но не для WebApplicationFactory<Program> в тестах,
// которому нужен доступный извне тип. Пустой partial-класс делает Program public,
// не меняя поведение самого приложения.
public partial class Program { }