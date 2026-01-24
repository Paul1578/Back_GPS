using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace fletflow.Api.Middleware
{
    public class SimpleRateLimitMiddleware : IMiddleware
    {
        private static readonly ConcurrentDictionary<string, (int Count, DateTime WindowStart)> Requests = new();
        private readonly ILogger<SimpleRateLimitMiddleware> _logger;
        private const int LIMIT = 60; // Máximo de requests
        private static readonly TimeSpan WINDOW = TimeSpan.FromMinutes(1); // Por minuto

        public SimpleRateLimitMiddleware(ILogger<SimpleRateLimitMiddleware> logger)
        {
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            var key = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var now = DateTime.UtcNow;
            var (count, windowStart) = Requests.GetOrAdd(key, _ => (0, now));

            if (now - windowStart > WINDOW)
            {
                Requests[key] = (1, now);
            }
            else
            {
                if (count >= LIMIT)
                {
                    _logger.LogWarning("[RateLimit] IP {IP} superó el límite de {Limit} req/min", key, LIMIT);
                    context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    await context.Response.WriteAsync($"Rate limit exceeded: {LIMIT} requests per minute");
                    return;
                }
                Requests[key] = (count + 1, windowStart);
            }
            await next(context);
        }
    }
}
