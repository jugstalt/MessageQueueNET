using MessageQueueNET.Models.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace MessageQueueNET.Middleware.Authentication
{
    public class BasicAuthenticationMiddleware
    {
        public const string AuthenticationType = "Basic";

        // reachable without authentication (health checks)
        private static readonly PathString[] AnonymousPaths = [new PathString("/health"), new PathString("/alive")];

        private readonly RequestDelegate _next;
        private readonly BasicAuthenticationClients _clients;

        public BasicAuthenticationMiddleware(RequestDelegate next,
                                             IConfiguration config,
                                             ILogger<BasicAuthenticationMiddleware> logger)
        {
            _next = next;
            _clients = new BasicAuthenticationClients(config);

            if (_clients.Count == 0)
            {
                logger.LogWarning("Basic authentication is enabled, but no clients are configured (Authorization:Clients). All requests will be rejected.");
            }
        }

        public static bool IsEnabled(IConfiguration config)
            => AuthenticationType.Equals(config["Authorization:Type"]?.Trim(), StringComparison.OrdinalIgnoreCase);

        public async Task InvokeAsync(HttpContext context)
        {
            if (IsAnonymousPath(context.Request.Path))
            {
                await _next(context);
                return;
            }

            if (TryGetCredentials(context.Request, out var id, out var password)
                && _clients.TryAuthenticate(id, password, out var clientId, out var role))
            {
                context.User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.Name, clientId),
                        new Claim(ClaimTypes.Role, role.ToString())
                    ],
                    AuthenticationType));

                await _next(context);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.Append("WWW-Authenticate", @"Basic realm=""Access to message queue"", charset=""UTF-8""");
        }

        #region Helper

        private static bool IsAnonymousPath(PathString path)
        {
            foreach (var anonymousPath in AnonymousPaths)
            {
                if (path.Equals(anonymousPath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetCredentials(HttpRequest request, out string id, out string password)
        {
            id = password = "";

            var authHeader = request.Headers.Authorization.ToString();
            if (!authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var encoded = authHeader.Substring("Basic ".Length).Trim();
            var buffer = new byte[(encoded.Length * 3 + 3) / 4];
            if (encoded.Length == 0 || !Convert.TryFromBase64String(encoded, buffer, out int bytesWritten))
            {
                return false;
            }

            string authCode;
            try
            {
                authCode = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(buffer, 0, bytesWritten);
            }
            catch (DecoderFallbackException)
            {
                return false;
            }

            var pos = authCode.IndexOf(':');
            if (pos <= 0)
            {
                return false;
            }

            id = authCode.Substring(0, pos);
            password = authCode.Substring(pos + 1);

            return true;
        }

        #endregion
    }
}
