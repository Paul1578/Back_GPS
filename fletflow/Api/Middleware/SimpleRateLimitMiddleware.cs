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
        private static readonly ConcurrentDictionary<string, (int Count, DateTime WindowStart)> LoginRequests = new();
        private static readonly ConcurrentDictionary<string, (int Count, DateTime WindowStart)> RecoveryRequests = new();
        private static readonly ConcurrentDictionary<string, (int Count, DateTime WindowStart)> RegistrationRequests = new();
        private static int _cleanupCounter;
        private readonly ILogger<SimpleRateLimitMiddleware> _logger;
        private readonly IWebHostEnvironment _environment;
        private const int LIMIT = 60; // Máximo de requests
        private static readonly TimeSpan WINDOW = TimeSpan.FromMinutes(1); // Por minuto
        private const int LOGIN_LIMIT = 10;
        private static readonly TimeSpan LOGIN_WINDOW = TimeSpan.FromMinutes(1);
        private const int RECOVERY_LIMIT = 5;
        private static readonly TimeSpan RECOVERY_WINDOW = TimeSpan.FromMinutes(10);
        private const int REGISTRATION_LIMIT = 5;
        private static readonly TimeSpan REGISTRATION_WINDOW = TimeSpan.FromMinutes(10);

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
                IPAddress.IsLoopback(remoteIp) &&
                !IsForgotPasswordRequest(context) &&
                !IsRegistrationRequest(context))
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

                var staleLoginEntries = LoginRequests
                    .Where(entry => now - entry.Value.WindowStart > LOGIN_WINDOW * 2)
                    .ToArray();
                var loginEntries = (ICollection<KeyValuePair<string, (int Count, DateTime WindowStart)>>)LoginRequests;
                foreach (var stale in staleLoginEntries)
                    loginEntries.Remove(stale);

                var staleRecoveryEntries = RecoveryRequests
                    .Where(entry => now - entry.Value.WindowStart > RECOVERY_WINDOW * 2)
                    .ToArray();
                var recoveryEntries = (ICollection<KeyValuePair<string, (int Count, DateTime WindowStart)>>)RecoveryRequests;
                foreach (var stale in staleRecoveryEntries)
                    recoveryEntries.Remove(stale);

                var staleRegistrationEntries = RegistrationRequests
                    .Where(entry => now - entry.Value.WindowStart > REGISTRATION_WINDOW * 2)
                    .ToArray();
                var registrationEntries = (ICollection<KeyValuePair<string, (int Count, DateTime WindowStart)>>)RegistrationRequests;
                foreach (var stale in staleRegistrationEntries)
                    registrationEntries.Remove(stale);
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

            if (IsLoginRequest(context))
            {
                var loginState = LoginRequests.AddOrUpdate(
                    key,
                    _ => (Count: 1, WindowStart: now),
                    (_, current) => now - current.WindowStart >= LOGIN_WINDOW
                        ? (Count: 1, WindowStart: now)
                        : (Count: Math.Min(current.Count + 1, LOGIN_LIMIT + 1), WindowStart: current.WindowStart));

                if (loginState.Count > LOGIN_LIMIT)
                {
                    var retryAfterSeconds = Math.Max(1,
                        (int)Math.Ceiling((LOGIN_WINDOW - (now - loginState.WindowStart)).TotalSeconds));
                    _logger.LogWarning("[RateLimit] Login IP {IP} superó el límite de {Limit} intentos por minuto", key, LOGIN_LIMIT);
                    context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    context.Response.Headers["Retry-After"] = retryAfterSeconds.ToString();
                    await context.Response.WriteAsync("Demasiados intentos de inicio de sesión. Inténtalo más tarde.");
                    return;
                }
            }

            if (IsForgotPasswordRequest(context))
            {
                var recoveryState = RecoveryRequests.AddOrUpdate(
                    key,
                    _ => (Count: 1, WindowStart: now),
                    (_, current) => now - current.WindowStart >= RECOVERY_WINDOW
                        ? (Count: 1, WindowStart: now)
                        : (Count: Math.Min(current.Count + 1, RECOVERY_LIMIT + 1), WindowStart: current.WindowStart));

                if (recoveryState.Count > RECOVERY_LIMIT)
                {
                    var retryAfterSeconds = Math.Max(1,
                        (int)Math.Ceiling((RECOVERY_WINDOW - (now - recoveryState.WindowStart)).TotalSeconds));
                    _logger.LogWarning("[RateLimit] Recovery IP {IP} superó el límite de {Limit} solicitudes por ventana", key, RECOVERY_LIMIT);
                    context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    context.Response.Headers["Retry-After"] = retryAfterSeconds.ToString();
                    await context.Response.WriteAsync("Demasiadas solicitudes de recuperación. Inténtalo más tarde.");
                    return;
                }
            }

            if (IsRegistrationRequest(context))
            {
                var registrationState = RegistrationRequests.AddOrUpdate(
                    key,
                    _ => (Count: 1, WindowStart: now),
                    (_, current) => now - current.WindowStart >= REGISTRATION_WINDOW
                        ? (Count: 1, WindowStart: now)
                        : (Count: Math.Min(current.Count + 1, REGISTRATION_LIMIT + 1), WindowStart: current.WindowStart));

                if (registrationState.Count > REGISTRATION_LIMIT)
                {
                    var retryAfterSeconds = Math.Max(1,
                        (int)Math.Ceiling((REGISTRATION_WINDOW - (now - registrationState.WindowStart)).TotalSeconds));
                    _logger.LogWarning("[RateLimit] Registration IP {IP} superó el límite de {Limit} solicitudes por ventana", key, REGISTRATION_LIMIT);
                    context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    context.Response.Headers["Retry-After"] = retryAfterSeconds.ToString();
                    await context.Response.WriteAsync("Demasiadas solicitudes de registro. Inténtalo más tarde.");
                    return;
                }
            }

            await next(context);
        }

        private static bool IsLoginRequest(HttpContext context)
            => HttpMethods.IsPost(context.Request.Method) &&
               string.Equals(context.Request.Path.Value?.TrimEnd('/'), "/api/Auth/login", StringComparison.OrdinalIgnoreCase);

        private static bool IsForgotPasswordRequest(HttpContext context)
            => HttpMethods.IsPost(context.Request.Method) &&
               string.Equals(context.Request.Path.Value?.TrimEnd('/'), "/api/Auth/forgot-password", StringComparison.OrdinalIgnoreCase);

        private static bool IsRegistrationRequest(HttpContext context)
            => HttpMethods.IsPost(context.Request.Method) &&
               string.Equals(context.Request.Path.Value?.TrimEnd('/'), "/api/Auth/register", StringComparison.OrdinalIgnoreCase);
    }
}
