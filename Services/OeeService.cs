using OeeSystem.Models;

namespace OeeSystem.Services;

public class OeeService : IOeeService
{
    public OeeResult CalculateOee(
        TimeSpan plannedProductionTime,
        TimeSpan operatingTime,
        int totalCount,
        int goodCount,
        double standarCycleTime)
    {
        double plannedSeconds = plannedProductionTime.TotalSeconds;
        double operatingSeconds = operatingTime.TotalSeconds;

        double availability = plannedSeconds <= 0
            ? 0
            : operatingSeconds / plannedSeconds * 100.0;

        double performance = (operatingSeconds <= 0 || standarCycleTime <= 0)
            ? 0
            : totalCount / (operatingSeconds / standarCycleTime) * 100.0;

        double quality = totalCount <= 0
            ? 0
            : (double)goodCount / totalCount * 100.0;

        double oee = (availability / 100.0) * (performance / 100.0) * (quality / 100.0) * 100.0;

        return new OeeResult(
            Math.Round(availability, 2),
            Math.Round(performance, 2),
            Math.Round(quality, 2),
            Math.Round(oee, 2));
    }

    public MachineStatus GetRealTimeStatus(Machine machine, JobRun? activeJobRun, bool hasOpenDowntime)
    {
        if (hasOpenDowntime)
        {
            return MachineStatus.TidakAktif;
        }

        if (activeJobRun != null && activeJobRun.EndTime == null)
        {
            return MachineStatus.Aktif;
        }

        return MachineStatus.TidakAktif; // Idle menjadi Tidak Aktif
    }
}


