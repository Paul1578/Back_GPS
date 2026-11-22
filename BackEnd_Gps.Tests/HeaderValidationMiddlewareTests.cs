using fletflow.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BackEnd_Gps.Tests;

public class HeaderValidationMiddlewareTests
{
    [Fact]
    public async Task PutWithoutBody_DoesNotRequireContentType()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Put;
        var calledNext = false;
        var middleware = new HeaderValidationMiddleware(NullLogger<HeaderValidationMiddleware>.Instance);

        await middleware.InvokeAsync(context, _ =>
        {
            calledNext = true;
            return Task.CompletedTask;
        });

        Assert.True(calledNext);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task PutWithBody_RequiresExactJsonMediaType()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Put;
        context.Request.ContentLength = 2;
        context.Request.ContentType = "application/jsonp";
        var middleware = new HeaderValidationMiddleware(NullLogger<HeaderValidationMiddleware>.Instance);

        await middleware.InvokeAsync(context, _ => Task.CompletedTask);

        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, context.Response.StatusCode);
    }

    [Fact]
    public async Task PutWithJsonBody_Passes()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Put;
        context.Request.ContentLength = 2;
        context.Request.ContentType = "application/json; charset=utf-8";
        var calledNext = false;
        var middleware = new HeaderValidationMiddleware(NullLogger<HeaderValidationMiddleware>.Instance);

        await middleware.InvokeAsync(context, _ =>
        {
            calledNext = true;
            return Task.CompletedTask;
        });

        Assert.True(calledNext);
    }
}
