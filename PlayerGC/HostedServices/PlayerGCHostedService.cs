using Bazzigg.Database.Context;
using Kartrider.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PlayerGC.HostedServices
{
    internal class PlayerGCHostedService : BackgroundService
    {
        private readonly ILogger<PlayerGCHostedService> _logger;
        private readonly IKartriderApi _kartriderApi;
        private readonly IDbContextFactory<AppDbContext> _appDbContextFactory;

        public PlayerGCHostedService(ILogger<PlayerGCHostedService> logger,
            IKartriderApi kartriderApi
            , IDbContextFactory<AppDbContext> appDbContextFactory)
        {
            _logger = logger;
            _kartriderApi = kartriderApi;
            _appDbContextFactory = appDbContextFactory;
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
                    catch (KartriderApiException)
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
#if !DEBUG
                await Task.Delay(3000, stoppingToken);
#endif   
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