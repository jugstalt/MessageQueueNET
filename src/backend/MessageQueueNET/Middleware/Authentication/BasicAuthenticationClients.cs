using MessageQueueNET.Models.Authentication;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace MessageQueueNET.Middleware.Authentication;

/// <summary>
/// Client credentials, loaded once at startup from Authorization:Clients
/// </summary>
public class BasicAuthenticationClients
{
    private const string Sha256Prefix = "sha256:";

    private readonly IReadOnlyList<Entry> _clients;

    public BasicAuthenticationClients(IConfiguration config)
    {
        var clients = config.GetSection("Authorization:Clients").Get<ApiClient[]>() ?? Array.Empty<ApiClient>();

        _clients = clients
            .Where(c => !String.IsNullOrEmpty(c.Id))
            .Select(c => new Entry(c.Id, ParseSecretHash(c), ParseRole(c)))
            .ToArray();
    }

    public int Count => _clients.Count;

    public bool TryAuthenticate(string id, string password, out string clientId, out ClientRole role)
    {
        var passwordHash = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        Entry? match = null;

        // compare against all clients => timing does not depend on which client matches
        foreach (var client in _clients)
        {
            bool idMatches = id.Equals(client.Id, StringComparison.InvariantCultureIgnoreCase);
            bool secretMatches = CryptographicOperations.FixedTimeEquals(passwordHash, client.SecretHash);

            if (idMatches && secretMatches && match is null)
            {
                match = client;
            }
        }

        clientId = match?.Id ?? "";
        role = match?.Role ?? default;

        return match is not null;
    }

    #region Helper

    private static byte[] ParseSecretHash(ApiClient client)
    {
        if (client.Secret.StartsWith(Sha256Prefix, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var hash = Convert.FromHexString(client.Secret.Substring(Sha256Prefix.Length).Trim());
                if (hash.Length == 32)
                {
                    return hash;
                }
            }
            catch (FormatException) { }

            throw new InvalidOperationException($"Authorization: invalid sha256 secret for client '{client.Id}' (expected sha256:<64 hex characters>)");
        }

        if (String.IsNullOrEmpty(client.Secret))
        {
            throw new InvalidOperationException($"Authorization: empty secret for client '{client.Id}'");
        }

        return SHA256.HashData(Encoding.UTF8.GetBytes(client.Secret));
    }

    private static ClientRole ParseRole(ApiClient client)
    {
        if (client.Roles is null || client.Roles.Length == 0)
        {
            return ClientRole.Admin;
        }

        ClientRole result = ClientRole.Read;
        foreach (var roleName in client.Roles)
        {
            if (!Enum.TryParse<ClientRole>(roleName?.Trim(), ignoreCase: true, out var role)
                || !Enum.IsDefined(role))
            {
                throw new InvalidOperationException($"Authorization: unknown role '{roleName}' for client '{client.Id}' (allowed: read, write, admin)");
            }

            if (role > result)
            {
                result = role;
            }
        }

        return result;
    }

    private record Entry(string Id, byte[] SecretHash, ClientRole Role);

    #endregion
}
