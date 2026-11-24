using System;
using System.Threading;
using System.Threading.Tasks;
using GMAO.Infrastructure.Cdc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GMAO.API.Workers;

/// <summary>
/// Background Worker indépendant des contrôleurs HTTP consommant le flux WAL PostgreSQL (pgoutput)
/// avec reconnexion résiliente et surveillance périodique du Replication Slot.
/// </summary>
public class PostgresCdcBackgroundService : BackgroundService
{
    private readonly PostgresCdcIngestionService _ingestionService;
    private readonly IOptions<PostgresCdcOptions> _options;
    private readonly ILogger<PostgresCdcBackgroundService> _logger;

    public PostgresCdcBackgroundService(
        PostgresCdcIngestionService ingestionService,
        IOptions<PostgresCdcOptions> options,
        ILogger<PostgresCdcBackgroundService> logger)
    {
        _ingestionService = ingestionService;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Laisser l'application HTTP et EF Core terminer le démarrage initial
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var opts = _options.Value;
            try
            {
                var state = await _ingestionService.ProbeCapabilitiesAsync(stoppingToken);

                if (state.Status is "Ready" or "ActiveStreaming")
                {
                    await _ingestionService.RunStreamingSessionAsync(stoppingToken);
                }
                else
                {
                    _logger.LogInformation("CDC Worker en veille ({Status}) : {Reason}", state.Status, state.StatusReason);
                    await Task.Delay(TimeSpan.FromSeconds(Math.Max(15, opts.ProbeIntervalSeconds)), stoppingToken);
                    continue;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Interruption de la session de réplication WAL CDC. Reconnexion dans {Delay}s.", opts.ReconnectDelaySeconds);
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, opts.ReconnectDelaySeconds)), stoppingToken);
        }
    }
}
