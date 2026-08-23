using ManagementCNCWorkshop.Api.Data;
using ManagementCNCWorkshop.Api.Models;
using ManagementCNCWorkshop.Api.Models.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagementCNCWorkshop.Api.Controllers.Admin;

/// <summary>运营后台设备保养：计划维护、提醒生成与查询</summary>
[ApiController]
[Route("api/admin/maintenance")]
[Authorize(Roles = "Admin")]
[Tags("运营后台-保养")]
public class AdminMaintenanceController(AppDbContext db) : ControllerBase
{
    /// <summary>保养计划列表</summary>
    [HttpGet("plans")]
    [ProducesResponseType(typeof(List<MaintenancePlan>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Plans() =>
        Ok(await db.MaintenancePlans.Include(x => x.Equipment).OrderBy(x => x.NextDueDate).ToListAsync());

    /// <summary>创建保养计划</summary>
    [HttpPost("plans")]
    [ProducesResponseType(typeof(MaintenancePlan), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreatePlan([FromBody] MaintenancePlan plan)
    {
        db.MaintenancePlans.Add(plan);
        await db.SaveChangesAsync();
        return Ok(plan);
    }

    /// <summary>查询待处理保养提醒（Pending / Overdue）</summary>
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

    /// <summary>根据保养计划自动生成提醒</summary>
    [HttpPost("reminders/generate")]
    [ProducesResponseType(typeof(GenerateRemindersResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GenerateReminders()
    {
        var today = DateTime.UtcNow.Date;
        var plans = await db.MaintenancePlans.Include(x => x.Equipment).ToListAsync();
        var created = 0;

        foreach (var plan in plans)
        {
            var remindDate = plan.NextDueDate.AddDays(-plan.RemindDaysBefore).Date;
            if (today < remindDate) continue;

            var exists = await db.MaintenanceReminders.AnyAsync(x =>
                x.PlanId == plan.Id && x.DueDate.Date == plan.NextDueDate.Date);
            if (exists) continue;

            db.MaintenanceReminders.Add(new MaintenanceReminder
            {
                PlanId = plan.Id,
                EquipmentId = plan.EquipmentId,
                DueDate = plan.NextDueDate,
                RemindDate = remindDate,
                Status = plan.NextDueDate.Date < today ? "Overdue" : "Pending"
            });
            created++;
        }

        await db.SaveChangesAsync();
        return Ok(new GenerateRemindersResult { Created = created });
    }
}
