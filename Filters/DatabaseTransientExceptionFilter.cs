using DentalClinic.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DentalClinic.Filters;

/// <summary>
/// Turns expected remote-database transport failures into a clean 503 response.
/// This keeps transient SQL faults out of the generic 500 path and, importantly for
/// local Visual Studio debugging, handles them inside MVC user code instead of leaving
/// them as user-unhandled controller exceptions.
/// </summary>
public sealed class DatabaseTransientExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var exception = context.Exception;

        if (exception is OperationCanceledException
            && context.HttpContext.RequestAborted.IsCancellationRequested)
        {
            // The browser navigated away or aborted a timed request. There is no useful
            // response to write because the connection may already be gone.
            context.ExceptionHandled = true;
            context.Result = new EmptyResult();
            return;
        }

        if (!DatabaseTransientError.IsTransient(exception))
            return;

        var logger = context.HttpContext.RequestServices
            .GetRequiredService<ILogger<DatabaseTransientExceptionFilter>>();

        logger.LogWarning(
            exception,
            "Temporary database connectivity failure on {Method} {Path}",
            context.HttpContext.Request.Method,
            context.HttpContext.Request.Path);

        context.Result = new ObjectResult(new
        {
            message = "База данных временно отвечает медленно. Повторите запрос через несколько секунд.",
            code = "database_temporarily_unavailable"
        })
        {
            StatusCode = StatusCodes.Status503ServiceUnavailable
        };
        context.ExceptionHandled = true;
    }
}
