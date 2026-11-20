namespace MessageQueueNET.Models.Authentication;

/// <summary>
/// Hierarchical: Admin includes Write, Write includes Read
/// </summary>
public enum ClientRole
{
    Read = 1,
    Write = 2,
    Admin = 3
}
