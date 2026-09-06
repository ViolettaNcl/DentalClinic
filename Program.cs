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
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("db");

builder.Services.AddScoped<NotificationService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ClinicClock>();
builder.Services.AddScoped<AppointmentSchedulingService>();
builder.Services.AddScoped<AppointmentMaintenanceService>();
builder.Services.AddScoped<AdminAnalyticsService>();
builder.Services.AddScoped<DistributedPaidApiQuotaService>();
var isVercel = Environment.GetEnvironmentVariable("VERCEL") == "1";

if (!isVercel)
{
    builder.Services.AddHostedService<AppointmentReminderService>();
    builder.Services.AddHostedService<StalePendingCleanupService>();
}

if (isVercel)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ChatKnowledgeService>();
builder.Services.AddSignalR();

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key не задан в конфигурации");
if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException("Jwt:Key должен содержать не менее 32 байт энтропии");

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
    options.SaveToken = false;
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

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // Browser sessions authenticate through the HttpOnly cookie for normal
            // API requests and SignalR/WebSocket negotiation alike. Deliberately do
            // not accept JWTs from query strings: URLs are routinely logged, copied
            // into analytics, browser history and proxy traces.
            if (string.IsNullOrEmpty(context.Token)
                && context.Request.Cookies.TryGetValue("dc_auth", out var cookieToken)
                && !string.IsNullOrWhiteSpace(cookieToken))
            {
                context.Token = cookieToken;
            }

            return Task.CompletedTask;
        },
        OnTokenValidated = async context =>
        {
            // JWT signature/expiry validation alone cannot revoke a token that was
            // copied before logout or password change. Match the token's version to
            // the current account row on every authenticated request so those events
            // invalidate all previously issued sessions immediately.
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

            var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
            var isCurrent = string.Equals(role, "Patient", StringComparison.OrdinalIgnoreCase)
                ? await db.Patients.AsNoTracking().AnyAsync(
                    p => p.Id == userId && p.TokenVersion == tokenVersion,
                    context.HttpContext.RequestAborted)
                : string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)
                    && await db.Admins.AsNoTracking().AnyAsync(
                        a => a.Id == userId && a.TokenVersion == tokenVersion,
                        context.HttpContext.RequestAborted);

            if (!isCurrent)
                context.Fail("Authentication session has been revoked.");
        }
    };
});

var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5000", "https://localhost:5001" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

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

    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 8,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.AddPolicy("chat", httpContext =>
    {
        var profile = ChatRateLimitPolicy.Resolve(httpContext.Request.Path.Value);
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"{profile.Bucket}:{ip}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = profile.PermitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });

    options.AddPolicy("translate", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = PaidApiQuotaPolicy.TranslatePermitLimit,
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

builder.Services.AddTransient<GeminiApiKeyHandler>();
builder.Services.AddHttpClient(string.Empty)
    .AddHttpMessageHandler<GeminiApiKeyHandler>();
builder.Services.AddControllers(options =>
    {
        options.Filters.Add<DentaProactiveSafetyFilter>();
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

var app = builder.Build();

if (isVercel)
    app.UseForwardedHeaders();

app.UseSecurityResponseHeaders();

app.UseExceptionHandler(errApp =>
{
    errApp.Run(async context =>
    {
        var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("GlobalException");

        if (feature?.Error is BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge })
        {
            logger.LogWarning("Отклонён слишком большой request body на {Path}", context.Request.Path);
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            await context.Response.WriteAsync("{\"message\":\"Request body is too large\"}");
            return;
        }

        if (feature?.Error != null)
            logger.LogError(feature.Error, "Необработанное исключение на {Path}", context.Request.Path);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsync("{\"message\":\"Произошла внутренняя ошибка сервера\"}");
    });
});

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseResponseCompression();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();

app.Use(async (context, next) =>
{
    if (PaidApiRoutePolicy.RequiresSameOrigin(context.Request.Method, context.Request.Path.Value))
    {
        var maxBodySizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (maxBodySizeFeature is { IsReadOnly: false })
            maxBodySizeFeature.MaxRequestBodySize = PaidApiPayloadPolicy.MaxRequestBodyBytes;

        if (PaidApiPayloadPolicy.IsKnownLengthTooLarge(context.Request.ContentLength))
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"message\":\"Request body is too large\"}");
            return;
        }

        var allowDirectRequests = app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing");
        var allowed = PaidApiOriginPolicy.IsAllowed(
            context.Request.Headers.Origin.ToString(),
            context.Request.Headers["Sec-Fetch-Site"].ToString(),
            context.Request.Scheme,
            context.Request.Host.Host,
            context.Request.Host.Port,
            allowDirectRequests);

        if (!allowed)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"message\":\"Cross-origin AI requests are not allowed\"}");
            return;
        }

        // The built-in ASP.NET limiter below is process-local. In production also
        // reserve the same client budget in SQL so spinning up another Vercel/container
        // instance cannot multiply paid Gemini/ElevenLabs quota.
        if (!app.Environment.IsDevelopment()
            && !app.Environment.IsEnvironment("Testing")
            && PaidApiQuotaPolicy.TryResolve(context.Request.Path.Value, out var quotaProfile))
        {
            var quota = context.RequestServices.GetRequiredService<DistributedPaidApiQuotaService>();
            var clientKey = PaidApiQuotaPolicy.CreateClientKey(
                context.Connection.RemoteIpAddress?.ToString());
            var acquired = await quota.TryAcquireAsync(
                quotaProfile.Bucket,
                clientKey,
                quotaProfile.PermitLimit,
                context.RequestAborted);

            if (!acquired)
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.ContentType = "application/json";
                context.Response.Headers["Retry-After"] = "60";
                await context.Response.WriteAsync(
                    "{\"message\":\"Слишком много платных AI-запросов. Попробуйте через минуту.\"}",
                    context.RequestAborted);
                return;
            }
        }
    }

    await next();
});

app.UseCors("AllowFrontend");

if (!app.Environment.IsDevelopment())
    app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthCheckJsonWriter.WriteResponse
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    if (!isVercel && db.Database.IsRelational())
        await db.Database.MigrateAsync();
    await DentalClinic.Data.DbSeeder.SeedAsync(db);
}

app.Run();

public partial class Program { }
