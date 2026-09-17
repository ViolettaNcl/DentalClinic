using DentalClinic.BackgroundJobs;
using DentalClinic.Data;
using DentalClinic.Filters;
using DentalClinic.HealthChecks;
using DentalClinic.Hubs;
using DentalClinic.Middleware;
using DentalClinic.Services;
using DentalClinic.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Data.SqlClient;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

var databaseCommandTimeoutSeconds = Math.Clamp(
    builder.Configuration.GetValue<int?>("Database:CommandTimeoutSeconds") ?? 20,
    5,
    60);
var databaseConnectTimeoutSeconds = Math.Clamp(
    builder.Configuration.GetValue<int?>("Database:ConnectTimeoutSeconds") ?? 15,
    5,
    30);
var databaseMaxPoolSize = Math.Clamp(
    builder.Configuration.GetValue<int?>("Database:MaxPoolSize") ?? 12,
    5,
    50);

var configuredConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection не задан");
var connectionStringBuilder = new SqlConnectionStringBuilder(configuredConnectionString)
{
    ConnectTimeout = databaseConnectTimeoutSeconds,
    ConnectRetryCount = 2,
    ConnectRetryInterval = 1,
    Pooling = true,
    MaxPoolSize = databaseMaxPoolSize
};

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        connectionStringBuilder.ConnectionString,
        sqlOptions =>
        {
            // The developer build currently talks to a remote SQL Server. Bound
            // individual SQL commands so one stalled request cannot occupy the
            // dashboard for several minutes. Do not enable EF retry execution
            // strategies globally because scheduling/admin flows use explicit SQL
            // transactions and must keep their existing transaction semantics. GET
            // requests instead use clean 503 semantics plus a single client retry.
            sqlOptions.CommandTimeout(databaseCommandTimeoutSeconds);
        }));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("db");

builder.Services.AddScoped<NotificationService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ClinicClock>();
builder.Services.AddScoped<AppointmentSchedulingService>();
builder.Services.AddScoped<AppointmentMaintenanceService>();
builder.Services.AddScoped<AdminAnalyticsService>();
builder.Services.AddScoped<AdminAccessService>();
builder.Services.AddScoped<DistributedRequestQuotaService>();

var isVercel = Environment.GetEnvironmentVariable("VERCEL") == "1";
var enableHostedBackgroundJobs =
    builder.Configuration.GetValue<bool?>("BackgroundJobs:EnableHostedServices")
    ?? !builder.Environment.IsDevelopment();

if (!isVercel && enableHostedBackgroundJobs)
{
    builder.Services.AddHostedService<AppointmentReminderService>();
    builder.Services.AddHostedService<StalePendingCleanupService>();
}

if (isVercel)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders =
            ForwardedHeaders.XForwardedFor |
            ForwardedHeaders.XForwardedProto;

        options.ForwardLimit = 1;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<TokenVersionCache>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ChatKnowledgeService>();
builder.Services.AddSingleton<DentaSiteKnowledgeService>();
builder.Services.AddScoped<DentaClinicRouter>();
builder.Services.AddScoped<DentaAiService>();
builder.Services.AddScoped<DentaAssistantService>();
builder.Services.AddSignalR();

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException(
        "Jwt:Key не задан в конфигурации");

if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key должен содержать не менее 32 байт энтропии");
}

var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

builder.Services.AddSingleton<JwtTokenService>();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            JwtBearerDefaults.AuthenticationScheme;

        options.DefaultChallengeScheme =
            JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.SaveToken = false;

        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtKey)),

                ClockSkew = TimeSpan.Zero
            };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // Browser sessions authenticate through the HttpOnly cookie
                // for normal API requests and SignalR/WebSocket negotiation.
                // JWTs are deliberately not accepted from query strings.

                if (string.IsNullOrEmpty(context.Token)
                    && context.Request.Cookies.TryGetValue(
                        "dc_auth",
                        out var cookieToken)
                    && !string.IsNullOrWhiteSpace(cookieToken))
                {
                    context.Token = cookieToken;
                }

                return Task.CompletedTask;
            },

            OnTokenValidated = async context =>
            {
                // Keep immediate session revocation without doing an extra remote-SQL
                // round trip for every single dashboard request. Login/logout/password
                // flows update TokenVersionCache explicitly; a short expiry covers
                // out-of-process database changes.
                var idText = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                var role = context.Principal?.FindFirstValue(ClaimTypes.Role);
                var versionText = context.Principal?.FindFirstValue(JwtTokenService.TokenVersionClaim);

                if (!int.TryParse(idText, out var userId)
                    || !int.TryParse(versionText, out var tokenVersion)
                    || string.IsNullOrWhiteSpace(role))
                {
                    context.Fail("Authentication session metadata is invalid.");
                    return;
                }

                if (!string.Equals(role, "Patient", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
                {
                    context.Fail("Authentication role is invalid.");
                    return;
                }

                var versionCache = context.HttpContext.RequestServices
                    .GetRequiredService<TokenVersionCache>();

                if (versionCache.TryGet(role, userId, out var cachedVersion))
                {
                    if (cachedVersion != tokenVersion)
                        context.Fail("Authentication session has been revoked.");
                    return;
                }

                try
                {
                    if (context.HttpContext.RequestAborted.IsCancellationRequested)
                        return;

                    using var validationTimeout = CancellationTokenSource.CreateLinkedTokenSource(
                        context.HttpContext.RequestAborted);
                    validationTimeout.CancelAfter(TimeSpan.FromSeconds(15));

                    var db = context.HttpContext.RequestServices
                        .GetRequiredService<ApplicationDbContext>();

                    int? currentVersion = string.Equals(
                        role,
                        "Patient",
                        StringComparison.OrdinalIgnoreCase)
                        ? await db.Patients
                            .AsNoTracking()
                            .Where(p => p.Id == userId)
                            .Select(p => (int?)p.TokenVersion)
                            .SingleOrDefaultAsync(validationTimeout.Token)
                        : await db.Admins
                            .AsNoTracking()
                            .Where(a => a.Id == userId)
                            .Select(a => (int?)a.TokenVersion)
                            .SingleOrDefaultAsync(validationTimeout.Token);

                    if (!currentVersion.HasValue || currentVersion.Value != tokenVersion)
                    {
                        context.Fail("Authentication session has been revoked.");
                        return;
                    }

                    versionCache.Set(role, userId, currentVersion.Value);
                }
                catch (OperationCanceledException)
                    when (context.HttpContext.RequestAborted.IsCancellationRequested)
                {
                    // Browser navigation/refresh can cancel a request while SQL is in
                    // flight. That is normal and must not break the Visual Studio
                    // debugger or surface as an application error.
                    return;
                }
                catch (OperationCanceledException)
                {
                    if (builder.Environment.IsDevelopment())
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("JwtValidation");
                        logger.LogWarning(
                            "TokenVersion lookup timed out in Development for {Role} id={UserId}; accepting the already cryptographically validated short-lived JWT for this request.",
                            role,
                            userId);
                        return;
                    }

                    context.Fail("Authentication validation timed out.");
                }
                catch (Exception ex) when (DatabaseTransientError.IsTransient(ex))
                {
                    if (builder.Environment.IsDevelopment())
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("JwtValidation");
                        logger.LogWarning(
                            ex,
                            "TokenVersion lookup hit a transient database failure in Development for {Role} id={UserId}; accepting the already cryptographically validated short-lived JWT for this request.",
                            role,
                            userId);
                        return;
                    }

                    context.Fail("Authentication validation database check failed.");
                }
            }
        };
    });

var allowedOrigins =
    builder.Configuration
        .GetSection("AllowedOrigins")
        .Get<string[]>()
    ?? new[]
    {
        "http://localhost:5000",
        "https://localhost:5001"
    };

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "AllowFrontend",
        policy =>
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials());
});

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy(
        "AppointmentCreate",
        httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey:
                    httpContext.Connection.RemoteIpAddress
                        ?.ToString()
                    ?? "unknown",

                factory: _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit =
                            GeneralRateLimitPolicy
                                .AppointmentCreatePermitLimit,

                        Window =
                            TimeSpan.FromMinutes(1),

                        QueueLimit = 0,

                        QueueProcessingOrder =
                            QueueProcessingOrder.OldestFirst
                    }));

    options.AddPolicy(
        "auth",
        httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey:
                    httpContext.Connection.RemoteIpAddress
                        ?.ToString()
                    ?? "unknown",

                factory: _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit =
                            GeneralRateLimitPolicy
                                .AuthPermitLimit,

                        Window =
                            TimeSpan.FromMinutes(1),

                        QueueLimit = 0
                    }));

    options.AddPolicy(
        "chat",
        httpContext =>
        {
            var profile =
                ChatRateLimitPolicy.Resolve(
                    httpContext.Request.Path.Value);

            var ip =
                httpContext.Connection.RemoteIpAddress
                    ?.ToString()
                ?? "unknown";

            return RateLimitPartition
                .GetFixedWindowLimiter(
                    partitionKey:
                        $"{profile.Bucket}:{ip}",

                    factory: _ =>
                        new FixedWindowRateLimiterOptions
                        {
                            PermitLimit =
                                profile.PermitLimit,

                            Window =
                                TimeSpan.FromMinutes(1),

                            QueueLimit = 0
                        });
        });

    options.AddPolicy(
        "translate",
        httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey:
                    httpContext.Connection.RemoteIpAddress
                        ?.ToString()
                    ?? "unknown",

                factory: _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit =
                            PaidApiQuotaPolicy
                                .TranslatePermitLimit,

                        Window =
                            TimeSpan.FromMinutes(1),

                        QueueLimit = 0
                    }));

    options.OnRejected =
        async (context, token) =>
        {
            context.HttpContext.Response.StatusCode =
                StatusCodes.Status429TooManyRequests;

            context.HttpContext.Response.ContentType =
                "application/json";

            await context.HttpContext.Response.WriteAsync(
                "{\"message\":\"Слишком много запросов с вашего IP. Попробуйте через минуту.\"}",
                token);
        };
});

builder.Services.AddTransient<GeminiApiKeyHandler>();

builder.Services
    .AddHttpClient(string.Empty)
    .AddHttpMessageHandler<GeminiApiKeyHandler>();

builder.Services
    .AddControllers(options =>
    {
        options.Filters.Add<DentaProactiveSafetyFilter>();
        options.Filters.Add<DatabaseTransientExceptionFilter>();
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions
            .PropertyNameCaseInsensitive = true;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

var app = builder.Build();

if (isVercel)
{
    app.UseForwardedHeaders();
}

app.UseSecurityResponseHeaders();

app.UseExceptionHandler(errApp =>
{
    errApp.Run(async context =>
    {
        var feature =
            context.Features.Get<
                Microsoft.AspNetCore.Diagnostics
                    .IExceptionHandlerFeature>();

        var logger =
            context.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("GlobalException");

        if (feature?.Error is OperationCanceledException
            && context.RequestAborted.IsCancellationRequested)
        {
            // A browser tab change, refresh or aborted fetch is not a server fault.
            // Do not turn it into a 500 or pause the debugger on a normal request
            // cancellation path.
            return;
        }

        if (feature?.Error != null
            && DatabaseTransientError.IsTransient(feature.Error))
        {
            logger.LogWarning(
                feature.Error,
                "Temporary database connectivity failure on {Path}",
                context.Request.Path);

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsync(
                "{\"message\":\"База данных временно отвечает медленно. Повторите запрос через несколько секунд.\",\"code\":\"database_temporarily_unavailable\"}");
            return;
        }

        if (feature?.Error is BadHttpRequestException
            {
                StatusCode:
                    StatusCodes.Status413PayloadTooLarge
            })
        {
            logger.LogWarning(
                "Отклонён слишком большой request body на {Path}",
                context.Request.Path);

            context.Response.ContentType =
                "application/json";

            context.Response.StatusCode =
                StatusCodes.Status413PayloadTooLarge;

            await context.Response.WriteAsync(
                "{\"message\":\"Request body is too large\"}");

            return;
        }

        if (feature?.Error != null)
        {
            logger.LogError(
                feature.Error,
                "Необработанное исключение на {Path}",
                context.Request.Path);
        }

        context.Response.ContentType =
            "application/json";

        context.Response.StatusCode =
            StatusCodes.Status500InternalServerError;

        await context.Response.WriteAsync(
            "{\"message\":\"Произошла внутренняя ошибка сервера\"}");
    });
});

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

// В Development compression отключена.
// Это предотвращает ERR_CONTENT_DECODING_FAILED
// при локальной загрузке HTML/CSS/JS.
if (!app.Environment.IsDevelopment())
{
    app.UseResponseCompression();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();

app.UseUnsafeRequestOriginProtection();

app.Use(async (context, next) =>
{
    if (PaidApiRoutePolicy.RequiresSameOrigin(
        context.Request.Method,
        context.Request.Path.Value))
    {
        var maxBodySizeFeature =
            context.Features.Get<
                IHttpMaxRequestBodySizeFeature>();

        if (maxBodySizeFeature is
            {
                IsReadOnly: false
            })
        {
            maxBodySizeFeature.MaxRequestBodySize =
                PaidApiPayloadPolicy.MaxRequestBodyBytes;
        }

        if (PaidApiPayloadPolicy
            .IsKnownLengthTooLarge(
                context.Request.ContentLength))
        {
            context.Response.StatusCode =
                StatusCodes.Status413PayloadTooLarge;

            context.Response.ContentType =
                "application/json";

            await context.Response.WriteAsync(
                "{\"message\":\"Request body is too large\"}");

            return;
        }

        var allowDirectRequests =
            app.Environment.IsDevelopment()
            || app.Environment.IsEnvironment("Testing");

        var allowed =
            PaidApiOriginPolicy.IsAllowed(
                context.Request.Headers.Origin.ToString(),
                context.Request.Headers["Sec-Fetch-Site"]
                    .ToString(),
                context.Request.Scheme,
                context.Request.Host.Host,
                context.Request.Host.Port,
                allowDirectRequests);

        if (!allowed)
        {
            context.Response.StatusCode =
                StatusCodes.Status403Forbidden;

            context.Response.ContentType =
                "application/json";

            await context.Response.WriteAsync(
                "{\"message\":\"Cross-origin AI requests are not allowed\"}");

            return;
        }

        if (!app.Environment.IsDevelopment()
            && !app.Environment.IsEnvironment("Testing")
            && PaidApiQuotaPolicy.TryResolve(
                context.Request.Path.Value,
                out var quotaProfile))
        {
            var quota =
                context.RequestServices
                    .GetRequiredService<
                        DistributedRequestQuotaService>();

            var clientKey =
                RateLimitClientKey.Create(
                    context.Connection
                        .RemoteIpAddress
                        ?.ToString());

            var acquired =
                await quota.TryAcquireAsync(
                    quotaProfile.Bucket,
                    clientKey,
                    quotaProfile.PermitLimit,
                    context.RequestAborted);

            if (!acquired)
            {
                context.Response.StatusCode =
                    StatusCodes.Status429TooManyRequests;

                context.Response.ContentType =
                    "application/json";

                context.Response.Headers["Retry-After"] =
                    "60";

                await context.Response.WriteAsync(
                    "{\"message\":\"Слишком много платных AI-запросов. Попробуйте через минуту.\"}",
                    context.RequestAborted);

                return;
            }
        }
    }

    if (!app.Environment.IsDevelopment()
        && !app.Environment.IsEnvironment("Testing")
        && GeneralRateLimitPolicy.TryResolve(
            context.Request.Method,
            context.Request.Path.Value,
            out var generalQuotaProfile))
    {
        var quota =
            context.RequestServices
                .GetRequiredService<
                    DistributedRequestQuotaService>();

        var clientKey =
            RateLimitClientKey.Create(
                context.Connection
                    .RemoteIpAddress
                    ?.ToString());

        var acquired =
            await quota.TryAcquireAsync(
                generalQuotaProfile.Bucket,
                clientKey,
                generalQuotaProfile.PermitLimit,
                context.RequestAborted);

        if (!acquired)
        {
            context.Response.StatusCode =
                StatusCodes.Status429TooManyRequests;

            context.Response.ContentType =
                "application/json";

            context.Response.Headers["Retry-After"] =
                "60";

            await context.Response.WriteAsync(
                "{\"message\":\"Слишком много запросов с вашего IP. Попробуйте через минуту.\"}",
                context.RequestAborted);

            return;
        }
    }

    await next();
});

app.UseCors("AllowFrontend");

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

app.MapHub<NotificationHub>(
    "/hubs/notifications");

app.MapHealthChecks(
    "/health",
    new HealthCheckOptions
    {
        ResponseWriter =
            HealthCheckJsonWriter.WriteResponse
    });

using (var scope = app.Services.CreateScope())
{
    var db =
        scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

    // Bring an existing SQL database schema up to date
    // before the application starts serving requests.
    if (db.Database.IsRelational())
    {
        await db.Database.MigrateAsync();
    }

    await DentalClinic.Data.DbSeeder
        .SeedAsync(db);
}

app.Run();

public partial class Program
{
}
