using System.Security.Claims;
using ManagementCNCWorkshop.Api.Data;
using ManagementCNCWorkshop.Api.Models;
using ManagementCNCWorkshop.Api.Models.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagementCNCWorkshop.Api.Controllers.Worker;

/// <summary>工人端设备时间：录入/查询设备当天的调试、开机、待机时间（编程技术员、操作工使用）</summary>
[ApiController]
[Route("api/worker/equipment-time")]
[Authorize(Roles = "Worker,Inspector,Programmer,Admin")]
[Tags("工人端-设备时间")]
public class WorkerEquipmentTimeController(AppDbContext db) : ControllerBase
{
    /// <summary>查询某设备某天的时间记录（未传 date 默认今天）</summary>
    [HttpGet]
    [ProducesResponseType(typeof(EquipmentTimeRecordDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get([FromQuery] int equipmentId, [FromQuery] DateTime? date)
    {
        var day = (date ?? DateTime.UtcNow).Date;
        var record = await db.EquipmentTimeRecords
            .Include(x => x.Equipment)
            .Include(x => x.Employee)
            .FirstOrDefaultAsync(x => x.EquipmentId == equipmentId && x.RecordDate == day);

        if (record is null)
            return NotFound();

        return Ok(ToDto(record));
    }

    /// <summary>查询某设备最近 N 天的时间记录</summary>
    [HttpGet("recent")]
    [ProducesResponseType(typeof(List<EquipmentTimeRecordDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Recent([FromQuery] int equipmentId, [FromQuery] int days = 7)
    {
        var start = DateTime.UtcNow.Date.AddDays(-(days - 1));
        var list = await db.EquipmentTimeRecords
            .Include(x => x.Equipment)
            .Include(x => x.Employee)
            .Where(x => x.EquipmentId == equipmentId && x.RecordDate >= start)
            .OrderByDescending(x => x.RecordDate)
            .ToListAsync();

        return Ok(list.Select(ToDto));
    }

    /// <summary>录入/更新某设备某天的时间记录（同一设备同一天只保留一条，重复提交为更新）</summary>
    [HttpPost]
    [ProducesResponseType(typeof(EquipmentTimeRecordDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upsert([FromBody] EquipmentTimeRequest req)
    {
        if (req.SetupHours < 0 || req.RunningHours < 0 || req.IdleHours < 0)
            return BadRequest(new { message = "时间不能为负数" });

        var equipment = await db.Equipments.FirstOrDefaultAsync(x => x.Id == req.EquipmentId);
        if (equipment is null)
            return BadRequest(new { message = "设备不存在" });

        var employeeId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var day = (req.RecordDate ?? DateTime.UtcNow).Date;

        var record = await db.EquipmentTimeRecords
            .FirstOrDefaultAsync(x => x.EquipmentId == req.EquipmentId && x.RecordDate == day);
        if (record is null)
        {
            record = new EquipmentTimeRecord
            {
                EquipmentId = req.EquipmentId,
                WorkshopId = equipment.WorkshopId,
                RecordDate = day,
                SetupHours = req.SetupHours,
                RunningHours = req.RunningHours,
                IdleHours = req.IdleHours,
                Remark = req.Remark,
                EmployeeId = employeeId
            };
            db.EquipmentTimeRecords.Add(record);
        }
        else
        {
            record.SetupHours = req.SetupHours;
            record.RunningHours = req.RunningHours;
            record.IdleHours = req.IdleHours;
            record.Remark = req.Remark;
            record.EmployeeId = employeeId;
        }

        await db.SaveChangesAsync();

        await db.Entry(record).Reference(x => x.Equipment).LoadAsync();
        await db.Entry(record).Reference(x => x.Employee).LoadAsync();
        return Ok(ToDto(record));
    }

    private static EquipmentTimeRecordDto ToDto(EquipmentTimeRecord r) => new()
    {
        Id = r.Id,
        EquipmentId = r.EquipmentId,
        EquipmentCode = r.Equipment?.Code,
        EquipmentName = r.Equipment?.Name,
        RecordDate = r.RecordDate.ToString("yyyy-MM-dd"),
        SetupHours = r.SetupHours,
        RunningHours = r.RunningHours,
        IdleHours = r.IdleHours,
        EmployeeId = r.EmployeeId,
        EmployeeName = r.Employee?.Name,
        Remark = r.Remark
    };
}
