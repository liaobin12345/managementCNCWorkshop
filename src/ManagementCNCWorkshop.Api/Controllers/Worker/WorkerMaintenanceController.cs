using System.Security.Claims;
using ManagementCNCWorkshop.Api.Data;
using ManagementCNCWorkshop.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagementCNCWorkshop.Api.Controllers.Worker;

/// <summary>工人端设备保养：查看待处理提醒、完成保养</summary>
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

    /// <summary>完成保养提醒</summary>
    /// <remarks>
    /// 执行保养后将提醒标记为 Completed，并同步推进对应保养计划的 NextDueDate
    /// （从本次截止日按周期推进，若仍早于今天则继续推进到未来），供下一轮提醒使用。
    /// </remarks>
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
