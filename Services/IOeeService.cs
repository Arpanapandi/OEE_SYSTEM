using OeeSystem.Models;

namespace OeeSystem.Services;

public record OeeResult(
    double Availability,
    double Performance,
    double Quality,
    double Oee);

public interface IOeeService
{
    OeeResult CalculateOee(
        TimeSpan plannedProductionTime,
        TimeSpan operatingTime,
        int totalCount,
        int goodCount,
        double standarCycleTime);

    MachineStatus GetRealTimeStatus(Machine machine, JobRun? activeJobRun, bool hasOpenDowntime);
}


