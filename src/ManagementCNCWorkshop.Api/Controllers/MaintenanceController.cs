using ManagementCNCWorkshop.Api.Data;
using ManagementCNCWorkshop.Api.Models;
using ManagementCNCWorkshop.Api.Models.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagementCNCWorkshop.Api.Controllers;

/// <summary>设备保养：保养计划、到期提醒</summary>
[ApiController]
[Route("api/maintenance")]
[Tags("设备保养")]
public class MaintenanceController(AppDbContext db) : ControllerBase
{
    /// <summary>创建保养计划</summary>
    /// <param name="plan">保养计划（EquipmentId、PlanName、CycleDays、NextDueDate 必填）</param>
    /// <returns>保存后的保养计划</returns>
    [HttpPost("plans")]
    [ProducesResponseType(typeof(MaintenancePlan), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreatePlan([FromBody] MaintenancePlan plan)
    {
        db.MaintenancePlans.Add(plan);
        await db.SaveChangesAsync();
        return Ok(plan);
    }

    /// <summary>查询待处理保养提醒</summary>
    /// <remarks>返回状态为 Pending（待处理）或 Overdue（已逾期）的提醒，按截止日期升序排列。</remarks>
    /// <param name="workshopId">按车间筛选（可选）</param>
    /// <returns>保养提醒列表（含关联的设备和计划信息）</returns>
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
    /// <remarks>
    /// 扫描所有保养计划，若当前日期已到提醒日（NextDueDate - RemindDaysBefore），
    /// 且该计划对应提醒尚未生成，则创建一条 MaintenanceReminder。
    /// 建议后续用定时任务每天调用一次；小程序也可手动触发。
    /// </remarks>
    /// <returns>本次新生成的提醒条数</returns>
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
