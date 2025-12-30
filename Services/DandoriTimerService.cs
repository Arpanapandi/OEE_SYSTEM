using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OeeSystem.Data;
using OeeSystem.Hubs;

namespace OeeSystem.Services;

public class DandoriTimerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DandoriTimerService> _logger;

    public DandoriTimerService(IServiceProvider serviceProvider, ILogger<DandoriTimerService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<OeeHub>>();

                // Cari semua job yang sedang Dandori (ada start time tapi belum ada end time)
                var runningDandoriJobs = await dbContext.JobRuns
                    .Where(j => j.DandoriStartTime.HasValue && j.DandoriEndTime == null)
                    .ToListAsync(stoppingToken);

                foreach (var job in runningDandoriJobs)
                {
                    try
                    {
                        // Hitung durasi Dandori
                        var baseSeconds = job.DandoriDurationSeconds ?? 0;
                        var elapsedSeconds = (int)(DateTime.Now - job.DandoriStartTime.Value).TotalSeconds;
                        var totalSeconds = baseSeconds + elapsedSeconds;

                        // Broadcast ke group machine
                        var machineId = int.TryParse(job.MachineId, out var id) ? id : 0;
                        if (machineId > 0)
                        {
                            try
                            {
                                await hubContext.Clients.Group($"machine_{machineId}")
                                    .SendAsync("DandoriDurationUpdated", machineId, totalSeconds, stoppingToken);
                                
                                // Log setiap 10 detik untuk debugging
                                if (totalSeconds % 10 == 0)
                                {
                                    _logger.LogInformation("📡 Broadcasting Dandori duration: Machine {MachineId}, Duration: {Duration}s", machineId, totalSeconds);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error broadcasting Dandori duration for machine {MachineId}", machineId);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error broadcasting Dandori duration for job {JobId}", job.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DandoriTimerService");
            }

            // Update setiap detik
            await Task.Delay(1000, stoppingToken);
        }
    }
}

