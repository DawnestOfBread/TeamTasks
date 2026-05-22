using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TeamTasks.Application.Common;

namespace TeamTasks.Api;

public static class RateLimiterConfiguration
{
    public static IServiceCollection AddCustomRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.ContentType = "application/problem+json";
                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too Many Requests",
                    Detail = "Rate limit exceeded. Please try again later."
                }, cancellationToken: token);
            };
            
            options.AddFixedWindowLimiter("AuthPolicy", opt =>
            {
                opt.PermitLimit = configuration.GetValue<int>("RateLimiting:AuthPermitLimit");
                opt.Window = TimeSpan.FromSeconds(configuration.GetValue<int>("RateLimiting:AuthWindowInSeconds"));
                opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                opt.QueueLimit = 0;
            });
            
            options.AddPolicy("TenantPolicy", httpContext =>
            {
                var tenantProvider = httpContext.RequestServices.GetRequiredService<ITenantProvider>();
                var organizationId = tenantProvider.OrganizationId;

                if (organizationId != null && organizationId != Guid.Empty)
                {
                    return RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: $"tenant_{organizationId}",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = configuration.GetValue<int>("RateLimiting:PermitLimit"),
                            Window = TimeSpan.FromSeconds(configuration.GetValue<int>("RateLimiting:WindowInSeconds")),
                            QueueLimit = configuration.GetValue<int>("RateLimiting:QueueLimit")
                        });
                }
                
                string ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: $"ip_{ipAddress}",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromSeconds(60),
                        QueueLimit = 0
                    });
            });
        });

        return services;
    }
}