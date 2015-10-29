﻿using System;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Hosting;
using Microsoft.Dnx.Runtime;
using Microsoft.Framework.Configuration;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.Logging;
using StopGuessing.Clients;
using StopGuessing.Controllers;
using StopGuessing.DataStructures;
using StopGuessing.Models;

namespace StopGuessing
{
    public class Startup
    {
        public Startup(IHostingEnvironment env, IApplicationEnvironment appEnv)
        {
            // Setup configuration sources.
            var builder = new ConfigurationBuilder()
                .SetBasePath(appEnv.ApplicationBasePath)
                .AddJsonFile("appsettings.json");

            if (env.IsEnvironment("Development"))
            {
                // This will push telemetry data through Application Insights pipeline faster, allowing you to view results immediately.
                builder.AddApplicationInsightsSettings(developerMode: true);
            }

            builder.AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; set; }

        // This method gets called by a runtime.
        // Use this method to add services to the container
        public void ConfigureServices(IServiceCollection services)
        {
            // Add Application Insights data collection services to the services container.
            services.AddApplicationInsightsTelemetry(Configuration);

            services.AddMvc();
            // Uncomment the following line to add Web API services which makes it easier to port Web API 2 controllers.
            // You will also need to add the Microsoft.AspNet.Mvc.WebApiCompatShim package to the 'dependencies' section of project.json.
            // services.AddWebApiConventions();

            var hosts = new MaxWeightHashing<RemoteHost>("FIXME-uniquekeyfromconfig");
            RemoteHost localHost = new RemoteHost {Uri = new Uri("http://localhost:35358")};

            services.AddSingleton<RemoteHost>(x => localHost);
            hosts.Add("localhost", localHost);

            // Use memory only stable store if none other is available.  FUTURE -- use azure SQL or tables
            services.AddSingleton<IStableStore, MemoryOnlyStableStore>();

            var options = new BlockingAlgorithmOptions();


            services.AddSingleton<BlockingAlgorithmOptions>(x => options);
            services.AddSingleton<BlockingAlgorithmOptions>(x => options);

            services.AddSingleton<MemoryUsageLimiter, MemoryUsageLimiter>();

            services.AddSingleton<IDistributedResponsibilitySet<RemoteHost>>(x => hosts);
            services.AddSingleton<UserAccountClient>();
            services.AddSingleton<LoginAttemptClient>();
            services.AddSingleton<UserAccountController>();
            services.AddSingleton<LoginAttemptController>();
        }

        // Configure is called after ConfigureServices is called.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.MinimumLevel = LogLevel.Information;
            loggerFactory.AddConsole();
            loggerFactory.AddDebug();

            // Add the platform handler to the request pipeline.
            app.UseIISPlatformHandler();

            // Add Application Insights to the request pipeline to track HTTP request telemetry data.
            app.UseApplicationInsightsRequestTelemetry();

            // Track data about exceptions from the application. Should be configured after all error handling middleware in the request pipeline.
            app.UseApplicationInsightsExceptionTelemetry();

            // Configure the HTTP request pipeline.
            app.UseDefaultFiles();
            app.UseStaticFiles();

            // Add MVC to the request pipeline.
            app.UseMvc();
            // Add the following route for porting Web API 2 controllers.
            // routes.MapWebApiRoute("DefaultApi", "api/{controller}/{id?}");

        }
    }
}
