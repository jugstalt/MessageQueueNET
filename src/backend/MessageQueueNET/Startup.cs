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
using Microsoft.OpenApi;
using System;

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

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v2", new OpenApiInfo { Title = "MessageQueue.NET", Version = "v2" });
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
                              RestorePersistedQueuesService restoreQueues,
                              IMessageQueueApiVersionService appVersion)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            if (env.IsDevelopment() || "true".Equals(Configuration["swaggerUI"], StringComparison.OrdinalIgnoreCase))
            {
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("../swagger/v2/swagger.json", $"MessageQueue.NET v{appVersion.Version}"));
            }

            restoreQueues.Restore().Wait();

            //app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseMiddleware<HashCodeMiddleware>();

            switch (Configuration["Authorization:Type"]?.ToLower())
            {
                case "basic":
                    app.UseMiddleware<BasicAuthenticationMiddleware>();
                    break;
            }

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
