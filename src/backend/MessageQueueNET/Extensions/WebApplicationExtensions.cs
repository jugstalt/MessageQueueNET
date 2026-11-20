using MessageQueueNET.Middleware.Authentication;
﻿using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;

namespace MessageQueueNET.Extensions;

static internal class WebApplicationExtensions
{
    public static WebApplication LogStartupInformation(
            this WebApplication app,
            WebApplicationBuilder builder,
            ILogger<Startup> logger
        )
    {
        var urls = builder.Configuration["urls"]?.Split(';', StringSplitOptions.RemoveEmptyEntries) ?? [];

        foreach (var url in urls)
        {
            logger.LogInformation("Listen to {url}", url);
        }

        var authorizationType = builder.Configuration["Authorization:Type"];
        if (!BasicAuthenticationMiddleware.IsEnabled(builder.Configuration))
        {
            if (!String.IsNullOrWhiteSpace(authorizationType))
            {
                logger.LogWarning("Unknown Authorization:Type {authorizationType}. Only 'Basic' is supported.", authorizationType);
            }

            logger.LogWarning("No authentication configured: the message queue API is accessible for everyone. Configure Authorization:Type = Basic and Authorization:Clients for production use.");
        }

        if (!urls.Any(url => url.Trim().StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            logger.LogWarning("The service does not listen on HTTPS. Credentials and messages are transferred unencrypted unless a TLS terminating reverse proxy is used.");
        }

        return app;
    }
}
