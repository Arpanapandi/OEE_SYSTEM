using Microsoft.AspNetCore.SignalR;
using OeeSystem.Data;
using OeeSystem.Hubs;
using Microsoft.EntityFrameworkCore;

namespace OeeSystem.Services;

/// <summary>
/// Background service yang berjalan setiap 10 detik untuk menghitung ulang OEE/Durasi 
/// dan mem-broadcast hasilnya ke semua operator via SignalR.
/// Ini memastikan bahwa operator yang sedang membuka halaman OEE Detail 
/// selalu melihat ticking metrics yang akurat dan sinkron satu sama lain.
/// </summary>
public class OeeRealTimeService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OeeRealTimeService> _logger;
    private readonly IHubContext<OeeHub> _hubContext;

    public OeeRealTimeService(
        IServiceScopeFactory scopeFactory,
        ILogger<OeeRealTimeService> logger,
        IHubContext<OeeHub> hubContext)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🚀 OeeRealTimeService is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var oeeService = scope.ServiceProvider.GetRequiredService<IOeeService>();
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    // 1. Ambil daftar mesin yang aktif (status 'Aktif')
                    // Kita fokus pada mesin yang mungkin sedang beroperasi
                    var machines = await context.Machines
                        .AsNoTracking()
                        .ToListAsync(stoppingToken);

                    foreach (var machine in machines)
                    {
                        try
                        {
                            // 2. Hitung metrics terbaru (termasuk ongoing durations)
                            var metrics = await oeeService.GetTimeMetricsAsync(machine.Id);

                            // 3. Broadcast ke SignalR group machine_{machineId}
                            // Event: "ReceiveRealTimeSync"
                            await _hubContext.Clients.Group($"machine_{machine.Id}")
                                .SendAsync("ReceiveRealTimeSync", metrics, stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"❌ Error processing real-time sync for Machine {machine.Id}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in OeeRealTimeService loop");
            }

            // 4. Tunggu 5 detik sebelum iterasi berikutnya (Matching user expectations for real-time updates)
            await Task.Delay(5000, stoppingToken);

        }

        _logger.LogInformation("🛑 OeeRealTimeService is stopping.");
    }
}
