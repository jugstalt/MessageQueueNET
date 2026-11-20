using MessageQueueNET.Client.Extensions.DependencyInjetion;
using MessageQueueNET.Client.Services.Abstraction;
using MessageQueueNET.Extensions.DependencyInjection;
using MessageQueueNET.Middleware;
using MessageQueueNET.Middleware.Authentication;
using MessageQueueNET.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scalar.AspNetCore;
using System;
using System.Threading.Tasks;

namespace MessageQueueNET
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMessageQueueAppVersionService();

            services.AddControllers()
                    .AddJsonOptions(options =>
                    {
                        options.JsonSerializerOptions.DefaultIgnoreCondition =
                            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
                    });

            services.AddOpenApi(options =>
            {
                options.AddDocumentTransformer((document, context, cancellationToken) =>
                {
                    document.Info.Title = "MessageQueue.NET";
                    document.Info.Version = context.ApplicationServices.GetRequiredService<IMessageQueueApiVersionService>().Version.ToString();
                    return Task.CompletedTask;
                });
            });

            services.AddQueuesService();
            switch (Configuration["MessageQueue:Persist:Type"]?.ToLower())
            {
                case "filesystem":
                    services.AddQueuePersitFileSystem(config =>
                        {
                            config.RootPath = Configuration["MessageQueue:Persist:RootPath"] ?? string.Empty;
                        });
                    break;
                default:
                    services.AddQueuePersitNone();
                    break;
            }

            services.AddRestorePersistedQueuesService();

            #region Background Task

            services.AddHostedService<TimedHostedBackgroundService>();

            #endregion
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app,
                              IWebHostEnvironment env,
                              IMessageQueueApiVersionService appVersion)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            bool basicAuthentication = BasicAuthenticationMiddleware.IsEnabled(Configuration);

            // authentication first => nothing (openapi/scalar, long polling, queue access) runs for anonymous requests
            if (basicAuthentication)
            {
                app.UseMiddleware<BasicAuthenticationMiddleware>();
            }


            //app.UseHttpsRedirection();

            app.UseRouting();

            if (basicAuthentication)
            {
                app.UseMiddleware<RoleAuthorizationMiddleware>();
            }

            app.UseMiddleware<HashCodeMiddleware>();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();

                if (env.IsDevelopment() || IsOpenApiUIEnabled())
                {
                    endpoints.MapOpenApi();
                    endpoints.MapScalarApiReference(options => options.WithTitle($"MessageQueue.NET v{appVersion.Version}"));
                }
            });
        }

        // "swaggerUI" is the legacy (v3) name of the "openApiUI" setting
        private bool IsOpenApiUIEnabled()
            => "true".Equals(Configuration["openApiUI"] ?? Configuration["swaggerUI"], StringComparison.OrdinalIgnoreCase);
    }
}
