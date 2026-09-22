using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace NovaLeave.Infrastructure.Configuration;

// Rate limiting configuration for authentication endpoints
// Implements FR-017: max 5 login attempts per IP per 5-min window
public static class RateLimitingConfiguration
{
    public static void ConfigureRateLimiting(RateLimiterOptions options)
    {
        // Fixed window rate limiter for login endpoints
        // FR-017: Max 5 intentos de login por IP cada 5 minutos
        options.AddFixedWindowLimiter("LoginRateLimit", opt =>
        {
            opt.Window = TimeSpan.FromMinutes(5);
            opt.PermitLimit = 5;
            opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            opt.QueueLimit = 0; // No queuing, reject immediately when limit exceeded
        });

        // Global rate limiter for all requests (optional, more generous)
        options.AddFixedWindowLimiter("GlobalRateLimit", opt =>
        {
            opt.Window = TimeSpan.FromMinutes(1);
            opt.PermitLimit = 100;
            opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            opt.QueueLimit = 0;
        });

        // Rejection status code when rate limit exceeded
        options.RejectionStatusCode = 429; // Too Many Requests
    }
}
