using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OeeSystem.Data;
using System.Linq;
using System.Threading.Tasks;

namespace OeeSystem.Controllers
{
    public class DebugController : Controller
    {
        private readonly ApplicationDbContext _context;
        public DebugController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var machines = await _context.Machines.Select(m => new { m.Id, m.Name, m.PlantId }).ToListAsync();
            var jobRuns = await _context.JobRuns
                .OrderByDescending(j => j.Id)
                .Take(50)
                .Select(j => new { 
                    j.Id, 
                    j.MachineId, 
                    MachineName = j.Machine.Name, 
                    j.WorkOrderId, 
                    WorkOrderNumber = j.WorkOrder.OrderNumber,
                    j.StartTime, 
                    j.EndTime 
                })
                .ToListAsync();
            
            return Json(new { 
                ServerTime = System.DateTime.Now,
                Machines = machines, 
                RecentJobRuns = jobRuns 
            });
        }
    }
}
