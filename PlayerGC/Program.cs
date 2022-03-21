﻿// See https://aka.ms/new-console-template for more information
using Bazzigg.Database.Context;
using Kartrider.Api.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PlayerGC.HostedServices;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Sentry;

var configuration = new ConfigurationBuilder()
        .AddEnvironmentVariables("APP_")
        .AddCommandLine(args)
        .AddJsonFile("appsettings.json",true,true)
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")}.json",true,true)
        .Build();
using (SentrySdk.Init(o =>
 {
     o.Dsn = configuration["Sentry:Dsn"];
     o.TracesSampleRate = 1.0;
 }))
{
    CreateHostBuilder(args, configuration["Sentry:Dsn"]).Build().Run();
}

static IHostBuilder CreateHostBuilder(string[] args,string sentryDsn) =>
    Host.CreateDefaultBuilder(args)
        .ConfigureServices((hostContext, services) =>
        {
            services.AddDbContextFactory<AppDbContext>(
                dbContextOptions => dbContextOptions.UseMySql(hostContext.Configuration.GetSection("ConnectionStrings")["App"], ServerVersion.Parse("10.5.6-MariaDB", ServerType.MariaDb))
#if DEBUG
                .EnableSensitiveDataLogging() // <-- These two calls are optional but help
                .EnableDetailedErrors()       // <-- with debugging (remove for production).
#endif
        );
            services.AddLogging();
            services.AddHostedService<PlayerGCHostedService>();
            services.AddKartriderApi(hostContext.Configuration["KartriderApiKey"]);
        })
        .ConfigureAppConfiguration((hostingContext, config) =>
        {
            config.AddEnvironmentVariables("APP_");
        })
        .ConfigureLogging((hostingContext, logging) =>
        {
            logging.AddSentry(sentryDsn);
        });
