using DentalClinic.Controllers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DentalClinic.Tests.Unit;

public class TranslateOriginSecurityTests
{
    [Fact]
    public async Task ProductionRequest_WithoutOriginOrBrowserMetadata_IsRejected()
    {
        var controller = CreateController("Production");
        controller.ControllerContext = new ControllerContext { HttpContext = NewHttpsContext() };

        var result = await controller.Translate(new TranslateController.TranslateRequest
        {
            Text = "hello",
            TargetLang = "fr"
        });

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, status.StatusCode);
    }

    [Fact]
    public async Task ProductionRequest_WithSameOrigin_IsAllowed()
    {
        var controller = CreateController("Production");
        var context = NewHttpsContext();
        context.Request.Headers.Origin = "https://dental-clinic-vn.vercel.app";
        controller.ControllerContext = new ControllerContext { HttpContext = context };

        var result = await controller.Translate(new TranslateController.TranslateRequest
        {
            Text = "hello",
            TargetLang = "fr"
        });

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task ProductionRequest_WithSpoofableSameOriginFetchMetadataButNoOrigin_IsRejected()
    {
        var controller = CreateController("Production");
        var context = NewHttpsContext();
        context.Request.Headers["Sec-Fetch-Site"] = "same-origin";
        controller.ControllerContext = new ControllerContext { HttpContext = context };

        var result = await controller.Translate(new TranslateController.TranslateRequest
        {
            Text = "hello",
            TargetLang = "fr"
        });

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, status.StatusCode);
    }

    [Fact]
    public async Task TestingRequest_WithoutOrigin_RemainsAllowedForDirectTestTools()
    {
        var controller = CreateController("Testing");
        controller.ControllerContext = new ControllerContext { HttpContext = NewHttpsContext() };

        var result = await controller.Translate(new TranslateController.TranslateRequest
        {
            Text = "hello",
            TargetLang = "fr"
        });

        Assert.IsType<OkObjectResult>(result);
    }

    private static TranslateController CreateController(string environmentName)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        return new TranslateController(
            new StubHttpClientFactory(),
            config,
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<TranslateController>.Instance,
            new StubEnvironment { EnvironmentName = environmentName });
    }

    private static DefaultHttpContext NewHttpsContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("dental-clinic-vn.vercel.app", 443);
        return context;
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    private sealed class StubEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "DentalClinic.Tests";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
