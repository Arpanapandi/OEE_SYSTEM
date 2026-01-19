using Microsoft.AspNetCore.SignalR;

namespace OeeSystem.Hubs;

/// <summary>
/// SignalR Hub untuk OEE real-time updates (Event-Based Architecture)
/// 
/// PRINSIP:
/// - Hanya broadcast START/STOP events, BUKAN update durasi tiap detik
/// - Client menghitung durasi dari timestamp UTC
/// - Server hanya menyimpan timestamp, bukan ticking timer
/// </summary>
public class OeeHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        Console.WriteLine($"✅ SignalR Client Connected: {Context.ConnectionId}");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        Console.WriteLine($"❌ SignalR Client Disconnected: {Context.ConnectionId}");
        if (exception != null)
        {
            Console.WriteLine($"   Error: {exception.Message}");
        }
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Join group untuk machine tertentu (untuk broadcast selektif)
    /// Group format: machine_{machineId}
    /// </summary>
    public async Task JoinMachineGroup(string machineId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"machine_{machineId}");
        Console.WriteLine($"✅ Client {Context.ConnectionId} joined machine_{machineId}");
    }

    /// <summary>
    /// Leave group untuk machine tertentu
    /// </summary>
    public async Task LeaveMachineGroup(string machineId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"machine_{machineId}");
        Console.WriteLine($"❌ Client {Context.ConnectionId} left machine_{machineId}");
    }

    // ========== EVENT-BASED METHODS (Hanya untuk internal use dari Controller) ==========
    // Methods ini dipanggil dari Controller via IHubContext, bukan dari client
    
    /// <summary>
    /// Broadcast DandoriStarted event ke group machine
    /// Event ini dipanggil saat Dandori dimulai
    /// </summary>
    public async Task BroadcastDandoriStarted(int machineId, DateTime startTimeUtc)
    {
        await Clients.Group($"machine_{machineId}").SendAsync("DandoriStarted", machineId, startTimeUtc);
        Console.WriteLine($"📡 Broadcasted DandoriStarted: machine_{machineId}, startTimeUtc: {startTimeUtc:O}");
    }

    /// <summary>
    /// Broadcast DandoriStopped event ke group machine
    /// Event ini dipanggil saat Dandori di-stop
    /// </summary>
    public async Task BroadcastDandoriStopped(int machineId, DateTime endTimeUtc, int totalSeconds)
    {
        await Clients.Group($"machine_{machineId}").SendAsync("DandoriStopped", machineId, endTimeUtc, totalSeconds);
        Console.WriteLine($"📡 Broadcasted DandoriStopped: machine_{machineId}, endTimeUtc: {endTimeUtc:O}, totalSeconds: {totalSeconds}");
    }

    /// <summary>
    /// Broadcast RunningStarted event ke group machine
    /// Event ini dipanggil saat Machine Running dimulai
    /// </summary>
    public async Task BroadcastRunningStarted(int machineId, DateTime startTimeUtc)
    {
        await Clients.Group($"machine_{machineId}").SendAsync("RunningStarted", machineId, startTimeUtc);
        Console.WriteLine($"📡 Broadcasted RunningStarted: machine_{machineId}, startTimeUtc: {startTimeUtc:O}");
    }

    /// <summary>
    /// Broadcast RunningStopped event ke group machine
    /// Event ini dipanggil saat Machine Running di-stop
    /// </summary>
    public async Task BroadcastRunningStopped(int machineId, DateTime endTimeUtc, int totalSeconds)
    {
        await Clients.Group($"machine_{machineId}").SendAsync("RunningStopped", machineId, endTimeUtc, totalSeconds);
        Console.WriteLine($"📡 Broadcasted RunningStopped: machine_{machineId}, endTimeUtc: {endTimeUtc:O}, totalSeconds: {totalSeconds}");
    }

    /// <summary>
    /// Broadcast real-time duration updates every 10 seconds.
    /// This ensures all clients are synchronized with the server's time calculation.
    /// </summary>
    public async Task BroadcastRealTimeSync(string machineId, object syncData)
    {
        await Clients.Group($"machine_{machineId}").SendAsync("ReceiveRealTimeSync", syncData);
        // Console.WriteLine($"📡 Broadcasted ReceiveRealTimeSync: machine_{machineId}");
    }
}

