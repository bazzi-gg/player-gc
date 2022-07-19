using Bazzigg.Database.Context;
using Kartrider.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PlayerGC.Options;
using System.Net;

namespace PlayerGC.HostedServices
{
    internal class PlayerGCHostedService : BackgroundService
    {
        private readonly ILogger<PlayerGCHostedService> _logger;
        private readonly IKartriderApi _kartriderApi;
        private readonly IDbContextFactory<AppDbContext> _appDbContextFactory;
        private readonly PlayerGCOptions _options = new PlayerGCOptions();
        public PlayerGCHostedService(ILogger<PlayerGCHostedService> logger,
            IKartriderApi kartriderApi,
            IDbContextFactory<AppDbContext> appDbContextFactory,
            IConfiguration configuration)
        {
            _logger = logger;
            _kartriderApi = kartriderApi;
            _appDbContextFactory = appDbContextFactory;
            configuration.GetSection(PlayerGCOptions.ConfigurationKey).Bind(_options);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // ... Use the service here ...
            while (!stoppingToken.IsCancellationRequested)
            {
                await using var appDbContext = _appDbContextFactory.CreateDbContext();
                foreach (var player in appDbContext.PlayerSummary.ToList())
                {
                    try
                    {
                        await _kartriderApi.User.GetUserByNicknameAsync(player.Nickname);
                        _logger.LogInformation($"{player.Nickname}: OK");
                    }
                    catch (KartriderApiException e) when(e.HttpStatusCode == HttpStatusCode.NotFound)
                    {
                        appDbContext.PlayerSummary.Remove(player);
                        foreach (var playerDetail in appDbContext.PlayerDetail.Where(x => x.Nickname == player.Nickname).ToList())
                        {
                            await appDbContext.Entry(playerDetail).Collection(b => b.Matches).LoadAsync(stoppingToken);
                            await appDbContext.Entry(playerDetail).Collection(b => b.RecentTrackRecords).LoadAsync(stoppingToken);
                            appDbContext.PlayerDetail.Remove(playerDetail);
                        }

                        await appDbContext.SaveChangesAsync(stoppingToken);
                        _logger.LogInformation($"{player.Nickname}: GC");
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(_options.LoopDelay), stoppingToken);
                }
            }
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting up");
            return base.StartAsync(cancellationToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping");
            return base.StopAsync(cancellationToken);
        }
    }
}