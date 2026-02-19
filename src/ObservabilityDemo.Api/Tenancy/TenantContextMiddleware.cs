using Microsoft.AspNetCore.Mvc;
using Serilog.Context;
using System.Diagnostics;

namespace ObservabilityDemo.Api.Tenancy;

public sealed class TenantContextMiddleware(RequestDelegate next)
{
    private static readonly PathString WorkItemsPath = new("/work-items");
    private const string TenantHeaderName = "X-Tenant-Id";
    public const string TenantLogPropertyName = "tenant_id";

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        if (!context.Request.Path.StartsWithSegments(WorkItemsPath, StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(TenantHeaderName, out var tenantHeaderValues))
        {
            await WriteBadRequestAsync(
                context,
                $"{TenantHeaderName} header is required for tenant-scoped endpoints.");
            return;
        }

        var rawTenantId = tenantHeaderValues.FirstOrDefault();
        if (!Guid.TryParse(rawTenantId, out var tenantId))
        {
            await WriteBadRequestAsync(
                context,
                $"{TenantHeaderName} must be a valid GUID value.");
            return;
        }

        tenantContext.SetTenant(tenantId);
        context.Items[TenantLogPropertyName] = tenantId.ToString();
        Activity.Current?.SetTag("tenant.id", tenantId);
        Activity.Current?.AddBaggage("tenant.id", tenantId.ToString());

        using (LogContext.PushProperty(TenantLogPropertyName, tenantId))
        {
            await next(context);
        }
    }

    private static async Task WriteBadRequestAsync(HttpContext context, string detail)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid tenant header",
                Detail = detail,
            });
    }
}
