using MessageQueueNET.Models.Authentication;
using System;

namespace MessageQueueNET.Middleware.Authentication;

/// <summary>
/// Minimum client role for an endpoint.
/// If the route parameter (id/idPattern) is a pattern, <see cref="PatternRole"/> is required.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class RequiredRoleAttribute : Attribute
{
    public RequiredRoleAttribute(ClientRole role)
    {
        Role = role;
        PatternRole = role;
    }

    public RequiredRoleAttribute(ClientRole role, ClientRole patternRole)
    {
        Role = role;
        PatternRole = patternRole;
    }

    public ClientRole Role { get; }
    public ClientRole PatternRole { get; }
}
