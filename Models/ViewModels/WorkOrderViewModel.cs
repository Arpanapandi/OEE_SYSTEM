using OeeSystem.Models;

namespace OeeSystem.Models.ViewModels;

public class WorkOrderViewModel
{
    public WorkOrder WorkOrder { get; set; } = null!;
    public DateTime? PlannedDate { get; set; }
    public string? ShiftName { get; set; }
}

