using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using System.Net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;

namespace fletflow.Api.Middleware
{
    public class SimpleRateLimitMiddleware : IMiddleware
    {
        private static readonly ConcurrentDictionary<string, (int Count, DateTime WindowStart)> Requests = new();
        private static int _cleanupCounter;
        private readonly ILogger<SimpleRateLimitMiddleware> _logger;
        private readonly IWebHostEnvironment _environment;
        private const int LIMIT = 60; // Máximo de requests
        private static readonly TimeSpan WINDOW = TimeSpan.FromMinutes(1); // Por minuto

        public SimpleRateLimitMiddleware(
            ILogger<SimpleRateLimitMiddleware> logger,
            IWebHostEnvironment environment)
        {
            _logger = logger;
            _environment = environment;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            if (_environment.IsDevelopment() &&
                context.Connection.RemoteIpAddress is { } remoteIp &&
                IPAddress.IsLoopback(remoteIp))
            {
                await next(context);
                return;
            }

            var key = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var now = DateTime.UtcNow;
            if ((Interlocked.Increment(ref _cleanupCounter) & 1023) == 0)
            {
                var staleEntries = Requests
                    .Where(entry => now - entry.Value.WindowStart > WINDOW * 2)
                    .ToArray();
                var entries = (ICollection<KeyValuePair<string, (int Count, DateTime WindowStart)>>)Requests;
                foreach (var stale in staleEntries)
                    entries.Remove(stale);
            }
            var requestState = Requests.AddOrUpdate(
                key,
                _ => (Count: 1, WindowStart: now),
                (_, current) => now - current.WindowStart >= WINDOW
                    ? (Count: 1, WindowStart: now)
                    : (Count: Math.Min(current.Count + 1, LIMIT + 1), WindowStart: current.WindowStart));

            if (requestState.Count > LIMIT)
            {
                _logger.LogWarning("[RateLimit] IP {IP} superó el límite de {Limit} req/min", key, LIMIT);
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.Response.WriteAsync($"Rate limit exceeded: {LIMIT} requests per minute");
                return;
            }
            await next(context);
        }
    }
}
