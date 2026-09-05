namespace DentalClinic.Middleware;

/// <summary>
/// Adds conservative browser hardening headers that do not interfere with the
/// clinic's external fonts, maps, AI integrations or microphone-based features.
/// </summary>
public sealed class SecurityResponseHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityResponseHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "SAMEORIGIN";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["X-Permitted-Cross-Domain-Policies"] = "none";

            // Authenticated API data (CRM, profile, admin analytics, etc.) must
            // not be stored in a shared/proxy/browser HTTP cache. Static assets
            // remain cacheable and public API responses keep their normal policy.
            if (context.User.Identity?.IsAuthenticated == true
                && context.Request.Path.StartsWithSegments("/api"))
            {
                headers["Cache-Control"] = "no-store";
                headers["Pragma"] = "no-cache";
            }

            return Task.CompletedTask;
        });

        await _next(context);
    }
}

public static class SecurityResponseHeadersExtensions
{
    public static IApplicationBuilder UseSecurityResponseHeaders(this IApplicationBuilder app) =>
        app.UseMiddleware<SecurityResponseHeadersMiddleware>();
}
