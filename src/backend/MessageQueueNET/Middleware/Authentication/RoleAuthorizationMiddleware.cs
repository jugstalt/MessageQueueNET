using MessageQueueNET.Extensions;
using MessageQueueNET.Models.Authentication;
using Microsoft.AspNetCore.Http;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MessageQueueNET.Middleware.Authentication;

/// <summary>
/// Checks <see cref="RequiredRoleAttribute"/> of the routed endpoint.
/// Must run after UseRouting and after <see cref="BasicAuthenticationMiddleware"/>.
/// </summary>
public class RoleAuthorizationMiddleware
{
    private readonly RequestDelegate _next;

    public RoleAuthorizationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var requiredRole = context.GetEndpoint()?.Metadata.GetMetadata<RequiredRoleAttribute>();

        if (requiredRole is not null)
        {
            var required = IsPatternRequest(context) ? requiredRole.PatternRole : requiredRole.Role;

            if (GetClientRole(context.User) is not ClientRole role || role < required)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
        }

        await _next(context);
    }

    private static bool IsPatternRequest(HttpContext context)
    {
        foreach (var key in new[] { "idPattern", "id" })
        {
            if (context.Request.RouteValues.TryGetValue(key, out var value)
                && value?.ToString() is string id
                && id.IsPattern())
            {
                return true;
            }
        }

        return false;
    }

    private static ClientRole? GetClientRole(ClaimsPrincipal user)
    {
        var roleClaim = user.FindFirst(ClaimTypes.Role)?.Value;

        return Enum.TryParse<ClientRole>(roleClaim, out var role) ? role : null;
    }
}
