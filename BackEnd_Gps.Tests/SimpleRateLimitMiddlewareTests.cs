using fletflow.Api.Middleware;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;

namespace BackEnd_Gps.Tests;

public class SimpleRateLimitMiddlewareTests
{
    [Fact]
    public async Task DevelopmentLoopback_AllowsMoreThanProductionLimit()
    {
        var middleware = new SimpleRateLimitMiddleware(
            NullLogger<SimpleRateLimitMiddleware>.Instance,
            new TestWebHostEnvironment("Development"));
        var nextCalls = 0;

        for (var requestNumber = 0; requestNumber < 65; requestNumber++)
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IPAddress.Loopback;
            await middleware.InvokeAsync(context, _ =>
            {
                nextCalls++;
                return Task.CompletedTask;
            });
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        }

        Assert.Equal(65, nextCalls);
    }

    [Fact]
    public async Task NonDevelopmentClient_IsLimitedAfterSixtyRequestsPerWindow()
    {
        var middleware = new SimpleRateLimitMiddleware(
            NullLogger<SimpleRateLimitMiddleware>.Instance,
            new TestWebHostEnvironment("Production"));
        var remoteAddress = new IPAddress(Guid.NewGuid().ToByteArray());
        var nextCalls = 0;
        DefaultHttpContext? lastContext = null;

        for (var requestNumber = 0; requestNumber < 61; requestNumber++)
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = remoteAddress;
            await middleware.InvokeAsync(context, _ =>
            {
                nextCalls++;
                return Task.CompletedTask;
            });
            lastContext = context;
        }

        Assert.Equal(60, nextCalls);
        Assert.NotNull(lastContext);
        Assert.Equal(StatusCodes.Status429TooManyRequests, lastContext.Response.StatusCode);
    }

    private sealed class TestWebHostEnvironment(string environmentName) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "BackEnd_Gps.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = environmentName;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
