using System;
using System.Diagnostics;
using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Ext;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Correlation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;


namespace SampleApp
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add framework services.
            services.AddMvc();
            services.Configure<CorrelationConfigurationOptions>(Configuration.GetSection("Correlation"));
            services.AddSingleton(new HttpClient());
        }

        private void SubsribeToSpanEvents(ILoggerFactory loggerFactory, IApplicationLifetime applicationLifetime)
        {
            var subscription = DiagnosticListener.AllListeners.Subscribe(delegate (DiagnosticListener listener)
            {
                if (listener.Name == "SpanDiagnosticListener")
                    listener.Subscribe(new SpanObserver(loggerFactory));
            });

            applicationLifetime.ApplicationStopped.Register(() => subscription?.Dispose());
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory, IApplicationLifetime applicationLifetime)
        {
            loggerFactory.WithFilter(new FilterLoggerSettings
                {
                    {"Microsoft", LogLevel.Warning},
                })
                .AddConsole(Configuration.GetSection("Logging"))
                .AddDebug()
                .AddElasicSearch();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseBrowserLink();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }
            app.UseStaticFiles();

            SubsribeToSpanEvents(loggerFactory, applicationLifetime);

            app.UseCorrelationInstrumentation();
            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
