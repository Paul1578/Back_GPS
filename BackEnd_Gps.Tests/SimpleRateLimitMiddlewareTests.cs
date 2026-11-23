using fletflow.Api.Middleware;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Threading;

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

    [Fact]
    public async Task Login_IsLimitedPerIpWithoutBlockingOtherEndpoints()
    {
        var middleware = new SimpleRateLimitMiddleware(
            NullLogger<SimpleRateLimitMiddleware>.Instance,
            new TestWebHostEnvironment("Production"));
        var remoteAddress = new IPAddress(Guid.NewGuid().ToByteArray());
        var loginNextCalls = 0;
        DefaultHttpContext? lastLoginContext = null;

        for (var requestNumber = 0; requestNumber < 11; requestNumber++)
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = remoteAddress;
            context.Request.Method = HttpMethods.Post;
            context.Request.Path = "/api/Auth/login";
            await middleware.InvokeAsync(context, _ =>
            {
                loginNextCalls++;
                return Task.CompletedTask;
            });
            lastLoginContext = context;
        }

        Assert.Equal(10, loginNextCalls);
        Assert.NotNull(lastLoginContext);
        Assert.Equal(StatusCodes.Status429TooManyRequests, lastLoginContext.Response.StatusCode);
        Assert.True(int.TryParse(lastLoginContext.Response.Headers["Retry-After"], out var retryAfter));
        Assert.InRange(retryAfter, 1, 60);

        var fleetContext = new DefaultHttpContext();
        fleetContext.Connection.RemoteIpAddress = remoteAddress;
        fleetContext.Request.Method = HttpMethods.Get;
        fleetContext.Request.Path = "/api/Routes";
        await middleware.InvokeAsync(fleetContext, _ =>
        {
            loginNextCalls++;
            return Task.CompletedTask;
        });

        Assert.Equal(StatusCodes.Status200OK, fleetContext.Response.StatusCode);
        Assert.Equal(11, loginNextCalls);
    }

    [Fact]
    public async Task ConcurrentRequests_CannotExceedSixtyAcceptedRequestsPerWindow()
    {
        var middleware = new SimpleRateLimitMiddleware(
            NullLogger<SimpleRateLimitMiddleware>.Instance,
            new TestWebHostEnvironment("Production"));
        var remoteAddress = new IPAddress(Guid.NewGuid().ToByteArray());
        var accepted = 0;
        var rejected = 0;
        var activeRequests = 0;
        var peakActiveRequests = 0;
        var readyRequests = 0;
        var allRequestsReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRequests = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var requests = Enumerable.Range(0, 200).Select(async _ =>
        {
            if (Interlocked.Increment(ref readyRequests) == 200)
                allRequestsReady.TrySetResult();
            await releaseRequests.Task;

            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = remoteAddress;
            await middleware.InvokeAsync(context, async _ =>
            {
                Interlocked.Increment(ref accepted);
                var currentActive = Interlocked.Increment(ref activeRequests);
                UpdateMaximum(ref peakActiveRequests, currentActive);
                try
                {
                    await Task.Yield();
                }
                finally
                {
                    Interlocked.Decrement(ref activeRequests);
                }
            });

            if (context.Response.StatusCode == StatusCodes.Status429TooManyRequests)
                Interlocked.Increment(ref rejected);
        }).ToArray();

        await allRequestsReady.Task.WaitAsync(TimeSpan.FromSeconds(10));
        releaseRequests.SetResult();
        await Task.WhenAll(requests);

        Assert.Equal(60, accepted);
        Assert.Equal(140, rejected);
        Assert.True(peakActiveRequests > 1, "Las solicitudes aceptadas deben solaparse para probar concurrencia real.");
    }

    private static void UpdateMaximum(ref int maximum, int candidate)
    {
        var observed = Volatile.Read(ref maximum);
        while (candidate > observed)
        {
            var prior = Interlocked.CompareExchange(ref maximum, candidate, observed);
            if (prior == observed)
                return;
            observed = prior;
        }
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
