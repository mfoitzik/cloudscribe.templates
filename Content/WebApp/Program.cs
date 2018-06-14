﻿using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace WebApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var hostBuilder = CreateWebHostBuilder(args);
            var host = hostBuilder.Build();
            
            using (var scope = host.Services.CreateScope())
            {
                var scopedServices = scope.ServiceProvider; 
                try
                {
                    EnsureDataStorageIsReady(scopedServices);

                }
                catch (Exception ex)
                {
                    var logger = scopedServices.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ex, "An error occurred while migrating the database.");
                }
            }

            #if (Logging)
            var env = host.Services.GetRequiredService<IHostingEnvironment>();
            var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
            var config = host.Services.GetRequiredService<IConfiguration>();
            ConfigureLogging(env, loggerFactory, host.Services, config);
            #endif

            host.Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
            #if (KvpCustomRegistration) 
                .ConfigureAppConfiguration((builderContext, config) =>
                {
                    config.AddJsonFile("app-userproperties.json", optional: true, reloadOnChange: true);
                })
            #endif
                .UseStartup<Startup>();

        private static void EnsureDataStorageIsReady(IServiceProvider scopedServices)
        {
            #if (NoDb)
            CoreNoDbStartup.InitializeDataAsync(scopedServices).Wait();
            #else
            CoreEFStartup.InitializeDatabaseAsync(scopedServices).Wait();
            #endif
            
            #if (SimpleContentConfig != "z")
            #if (!NoDb)
            SimpleContentEFStartup.InitializeDatabaseAsync(scopedServices).Wait();
            #endif
            #endif

            #if (KvpCustomRegistration) 
            #if (!NoDb)
            KvpEFCoreStartup.InitializeDatabaseAsync(scopedServices).Wait();
            #endif
            #endif

            #if (IdentityServer)
            #if (NoDb)
            CloudscribeIdentityServerIntegrationNoDbStorage.InitializeDatabaseAsync(scopedServices).Wait();
            #else
            CloudscribeIdentityServerIntegrationEFCoreStorage.InitializeDatabaseAsync(scopedServices).Wait();
            #endif
            #endif

            #if (Logging)
            #if (!NoDb)
            LoggingEFStartup.InitializeDatabaseAsync(scopedServices).Wait();
            #endif
            #endif
        }

#if (Logging)
        private static void ConfigureLogging(
            IHostingEnvironment env,
            ILoggerFactory loggerFactory,
            IServiceProvider serviceProvider,
            IConfiguration config
            )
        {
            LogLevel minimumLevel;
            string levelConfig;
            if (env.IsProduction())
            {
                levelConfig = config["AppSettings:ProductionLogLevel"];
            }
            else
            {
                levelConfig = config["AppSettings:DevLogLevel"];
            }
            switch(levelConfig)
            {
                case "Debug":
                    minimumLevel = LogLevel.Debug;
                    break;

                case "Information":
                    minimumLevel = LogLevel.Information;
                    break;

                case "Trace":
                    minimumLevel = LogLevel.Trace;
                    break;

                default:
                    minimumLevel = LogLevel.Warning;
                    break;
            }

            // a customizable filter for logging
            // add exclusions to remove noise in the logs
            var excludedLoggers = new List<string>
            {
                "Microsoft.AspNetCore.StaticFiles.StaticFileMiddleware",
                "Microsoft.AspNetCore.Hosting.Internal.WebHost",
            };

            Func<string, LogLevel, bool> logFilter = (string loggerName, LogLevel logLevel) =>
            {
                if (logLevel < minimumLevel)
                {
                    return false;
                }

                if (excludedLoggers.Contains(loggerName))
                {
                    return false;
                }

                return true;
            };

            loggerFactory.AddDbLogger(serviceProvider, logFilter);
        }
#endif

    }


}
