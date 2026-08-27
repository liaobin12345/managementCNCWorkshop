using ManagementCNCWorkshop.Api.Data;
using ManagementCNCWorkshop.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagementCNCWorkshop.Api.Controllers.Worker;

/// <summary>工人端设备保养：查看待处理提醒</summary>
[ApiController]
[Route("api/worker/maintenance")]
[Authorize(Roles = "Worker,Inspector,Programmer,Admin")]
[Tags("工人端-保养")]
public class WorkerMaintenanceController(AppDbContext db) : ControllerBase
{
    /// <summary>查询待处理保养提醒</summary>
    /// <remarks>返回 Pending（待处理）/ Overdue（已逾期）的提醒，按截止日期升序。</remarks>
    [HttpGet("reminders/pending")]
    [ProducesResponseType(typeof(List<MaintenanceReminder>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PendingReminders([FromQuery] int? workshopId)
    {
        var q = db.MaintenanceReminders
            .Include(x => x.Equipment)
            .Include(x => x.Plan)
            .Where(x => x.Status == "Pending" || x.Status == "Overdue");

        if (workshopId.HasValue)
            q = q.Where(x => x.Equipment!.WorkshopId == workshopId);

        return Ok(await q.OrderBy(x => x.DueDate).ToListAsync());
    }

    /// <summary>查询保养计划（含设备信息，按下次保养日期升序）</summary>
    [HttpGet("plans")]
    [ProducesResponseType(typeof(List<MaintenancePlan>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Plans()
    {
        var list = await db.MaintenancePlans
            .Include(x => x.Equipment)
            .OrderBy(x => x.NextDueDate)
            .ToListAsync();
        return Ok(list);
    }
}
