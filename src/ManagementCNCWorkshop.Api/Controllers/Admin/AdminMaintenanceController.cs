using System.Security.Claims;
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

    /// <summary>查询保养提醒</summary>
    /// <remarks>不传 status 时返回 Pending（待处理）/ Overdue（已逾期）；传 status（如 Completed）时按指定状态筛选。</remarks>
    [HttpGet("reminders/pending")]
    [ProducesResponseType(typeof(List<MaintenanceReminder>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PendingReminders([FromQuery] int? workshopId, [FromQuery] string? status)
    {
        var q = db.MaintenanceReminders
            .Include(x => x.Equipment)
            .Include(x => x.Plan)
            .Include(x => x.CompletedBy)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
            q = q.Where(x => x.Status == status);
        else
            q = q.Where(x => x.Status == "Pending" || x.Status == "Overdue");

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

    /// <summary>完成保养提醒（标记完成并推进下次保养日期）</summary>
    [HttpPost("reminders/{id}/complete")]
    [ProducesResponseType(typeof(MaintenanceReminder), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CompleteReminder(int id)
    {
        var reminder = await db.MaintenanceReminders
            .Include(x => x.Equipment)
            .Include(x => x.Plan)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (reminder is null)
            return NotFound(new { message = "保养提醒不存在" });
        if (reminder.Status == "Completed")
            return BadRequest(new { message = "该保养提醒已完成，请勿重复操作" });

        reminder.Status = "Completed";
        reminder.CompletedAt = DateTime.UtcNow;
        reminder.CompletedById = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // 推进下次保养日期：从本次截止日往后推一个周期；若仍不晚于今天则继续推进（积压多期的情况）
        if (reminder.Plan is { CycleDays: > 0 })
        {
            var today = DateTime.UtcNow.Date;
            var next = reminder.DueDate.Date.AddDays(reminder.Plan.CycleDays);
            while (next <= today)
                next = next.AddDays(reminder.Plan.CycleDays);
            reminder.Plan.NextDueDate = next;
        }

        await db.SaveChangesAsync();

        await db.Entry(reminder).Reference(x => x.CompletedBy).LoadAsync();
        return Ok(reminder);
    }
}
