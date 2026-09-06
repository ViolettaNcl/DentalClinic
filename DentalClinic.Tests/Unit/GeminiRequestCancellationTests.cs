using System.Net;
using DentalClinic.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class GeminiRequestCancellationTests
{
    [Fact]
    public async Task GeminiCall_IsCancelled_WhenAspNetRequestIsAborted()
    {
        using var requestAbort = new CancellationTokenSource();
        var httpContext = new DefaultHttpContext
        {
            RequestAborted = requestAbort.Token
        };
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:ApiKey"] = "test-key"
            })
            .Build();

        var upstream = new BlockingHandler();
        using var handler = new GeminiApiKeyHandler(configuration, accessor)
        {
            InnerHandler = upstream
        };
        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://generativelanguage.googleapis.com/v1beta/models");

        var sendTask = client.SendAsync(request);
        var upstreamToken = await upstream.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(upstreamToken.CanBeCanceled);
        Assert.False(upstreamToken.IsCancellationRequested);

        requestAbort.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await sendTask);
        Assert.True(upstreamToken.IsCancellationRequested);
    }

    private sealed class BlockingHandler : HttpMessageHandler
    {
        public TaskCompletionSource<CancellationToken> Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Started.TrySetResult(cancellationToken);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
