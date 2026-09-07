using DentalClinic.Services;

namespace DentalClinic.Middleware;

public sealed class UnsafeRequestOriginMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _environment;

    public UnsafeRequestOriginMiddleware(
        RequestDelegate next,
        IWebHostEnvironment environment)
    {
        _next = next;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var hasAuthCookie = context.Request.Cookies.ContainsKey("dc_auth");
        if (UnsafeRequestOriginPolicy.RequiresValidation(
            context.Request.Method,
            context.Request.Path.Value,
            hasAuthCookie))
        {
            var allowDirectRequests = _environment.IsDevelopment()
                || _environment.IsEnvironment("Testing");
            var allowed = UnsafeRequestOriginPolicy.IsAllowed(
                context.Request.Headers.Origin.ToString(),
                context.Request.Headers.Referer.ToString(),
                context.Request.Headers["Sec-Fetch-Site"].ToString(),
                context.Request.Scheme,
                context.Request.Host.Host,
                context.Request.Host.Port,
                allowDirectRequests);

            if (!allowed)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(
                    "{\"message\":\"Cross-origin state-changing requests are not allowed\"}",
                    context.RequestAborted);
                return;
            }
        }

        await _next(context);
    }
}

public static class UnsafeRequestOriginMiddlewareExtensions
{
    public static IApplicationBuilder UseUnsafeRequestOriginProtection(
        this IApplicationBuilder app)
        => app.UseMiddleware<UnsafeRequestOriginMiddleware>();
}
