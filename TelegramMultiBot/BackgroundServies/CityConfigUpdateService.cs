using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;
using TelegramMultiBot.Database;
using TelegramMultiBot.Database.Interfaces;

namespace TelegramMultiBot.BackgroundServies;

public class CityConfigUpdateService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CityConfigUpdateService> _logger;
    ISqlConfiguationService _sqlConfiguationService;
    CancellationTokenSource _delayCancellationTokenSource;


    public CityConfigUpdateService(
        IServiceProvider serviceProvider,
        ILogger<CityConfigUpdateService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _delayCancellationTokenSource = new CancellationTokenSource();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _serviceProvider.CreateScope();
            _sqlConfiguationService = scope.ServiceProvider.GetRequiredService<ISqlConfiguationService>();
            var delay = 30;

            try
            {
                var dbservice = scope.ServiceProvider.GetRequiredService<IMonitorDataService>();
                await ParseExistingConfigurations(dbservice);

                await DoCleanup(dbservice);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during city config update: {message}", ex.Message);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(delay), _delayCancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Delay cancelled, running the parser again immediately");
            }
            finally
            {
                _delayCancellationTokenSource = new CancellationTokenSource();
            }
        }
    }

    private async Task DoCleanup(IMonitorDataService dbservice)
    {
        var snapshots = await dbservice.RemoveProcessedSnapshots();
        if(snapshots > 0)
        {
            _logger.LogInformation("Cleaned up {count} processed snapshots", snapshots);
        }
    }

    private async Task ParseExistingConfigurations(IMonitorDataService dbservice)
    {
        var snapshots = await dbservice.GetNotProcessedSnapshots();
        foreach (var snapshot in snapshots)
        {
            using var loggerscope = _logger.BeginScope($"Snapshot: {snapshot.Id}");
            try
            {
                Stopwatch sw = Stopwatch.StartNew();

                var config = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(snapshot.ConfigJson);
                if (config == null)
                {
                    _logger.LogWarning("Failed to deserialize config json for snapshot {id}", snapshot.Id);
                    continue;
                }
                var progress = 0.0;
                var total = config.Keys.Count;
                foreach (var city in config.Keys)
                {
                    _logger.LogDebug("Processing city {city} ({progress}/{total})", city, progress + 1, total);
                    progress += 1;

                    var cityStreets = config[city];
                    var dbCity = await dbservice.GetCityByNameAndLocation(snapshot.LocationId, city);
                    if (dbCity == null)
                    {
                        dbCity = new City()
                        {
                            LocationId = snapshot.LocationId,
                            Name = city,
                        };
                        _logger.LogInformation("Created new city {city} in region {region}", city, snapshot.LocationId);
                        await dbservice.Add(dbCity, false);
                    }

                    foreach (var street in cityStreets)
                    {
                        var dbStreet = dbCity.Streets
                            .FirstOrDefault(s => s.Name.Equals(street, StringComparison.OrdinalIgnoreCase));

                        if (dbStreet == null)
                        {
                            dbStreet = new Street()
                            {
                                CityId = dbCity.Id,
                                Name = street
                            };
                            dbCity.Streets.Add(dbStreet);
                            _logger.LogInformation("Added new street {street} to city {city}", street, city);
                        }
                    }
                }

                snapshot.IsProcessed = true;
                await dbservice.ApplyChanges();
                sw.Stop();
                _logger.LogDebug("Updating cities and streets took {time} s", sw.Elapsed.TotalSeconds);
                _logger.LogInformation("Cities and streets updated in database");

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing snapshot {id}: {message}", snapshot.Id, ex.Message);
            }
        }
    }

    internal void CancelDelay()
    {
        _delayCancellationTokenSource.Cancel();
    }
}