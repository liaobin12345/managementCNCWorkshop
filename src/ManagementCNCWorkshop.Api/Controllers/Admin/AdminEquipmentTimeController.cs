using ManagementCNCWorkshop.Api.Data;
using ManagementCNCWorkshop.Api.Models;
using ManagementCNCWorkshop.Api.Models.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagementCNCWorkshop.Api.Controllers.Admin;

/// <summary>运营后台设备时间管理：查看/录入/编辑设备每日的调试、开机、待机时间</summary>
[ApiController]
[Route("api/admin/equipment-time")]
[Authorize(Roles = "Admin")]
[Tags("运营后台-设备时间")]
public class AdminEquipmentTimeController(AppDbContext db) : ControllerBase
{
    /// <summary>分页查询设备时间记录</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<EquipmentTimeRecordDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int? equipmentId,
        [FromQuery] string? startDate,
        [FromQuery] string? endDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var q = db.EquipmentTimeRecords
            .Include(x => x.Equipment)
            .Include(x => x.Employee)
            .AsQueryable();

        if (equipmentId.HasValue)
            q = q.Where(x => x.EquipmentId == equipmentId);

        if (DateTime.TryParse(startDate, out var start))
            q = q.Where(x => x.RecordDate >= start);
        if (DateTime.TryParse(endDate, out var end))
            q = q.Where(x => x.RecordDate <= end);

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(x => x.RecordDate).ThenBy(x => x.EquipmentId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PagedResult<EquipmentTimeRecordDto>
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Items = items.Select(ToDto).ToList()
        });
    }

    /// <summary>设备时间汇总（Dashboard 看板用）</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(List<EquipmentTimeSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Summary(
        [FromQuery] string? startDate,
        [FromQuery] string? endDate,
        [FromQuery] int? workshopId)
    {
        var start = DateTime.TryParse(startDate, out var s) ? s : DateTime.UtcNow.Date;
        var end = DateTime.TryParse(endDate, out var e) ? e : start;

        // 设备时间
        var timeRecords = await db.EquipmentTimeRecords
            .Include(x => x.Equipment)
            .Where(x => x.RecordDate >= start && x.RecordDate <= end)
            .ToListAsync();

        // 产量（同一时间段）
        var workReports = await db.WorkReports
            .Where(x => x.ReportDate >= start && x.ReportDate <= end)
            .ToListAsync();

        // 按设备分组时间
        var summary = timeRecords
            .GroupBy(x => x.EquipmentId)
            .Select(g => new
            {
                EquipmentId = g.Key,
                SetupHours = g.Sum(x => x.SetupHours),
                RunningHours = g.Sum(x => x.RunningHours),
                IdleHours = g.Sum(x => x.IdleHours),
                Equipment = g.First().Equipment
            })
            .ToList();

        // 如果传了设备时间但不够，补全所有有报工或绑定的设备
        var allEquipment = await db.Equipments
            .Where(x => !workshopId.HasValue || x.WorkshopId == workshopId)
            .ToListAsync();

        var result = allEquipment.Select(eq =>
        {
            var s = summary.FirstOrDefault(x => x.EquipmentId == eq.Id);

            // 产量
            var eqReports = workReports.Where(x => x.EquipmentId == eq.Id).ToList();
            var totalQty = eqReports.Sum(x => x.Quantity);
            var qualifiedQty = eqReports.Sum(x => x.QualifiedQty);
            var defectQty = eqReports.Sum(x => x.DefectQty);
            var passRate = totalQty > 0 ? qualifiedQty / totalQty : 1m;

            return new EquipmentTimeSummaryDto
            {
                EquipmentId = eq.Id,
                EquipmentCode = eq.Code,
                EquipmentName = eq.Name,
                SetupHours = s?.SetupHours ?? 0,
                RunningHours = s?.RunningHours ?? 0,
                IdleHours = s?.IdleHours ?? 0,
                TotalQty = totalQty,
                QualifiedQty = qualifiedQty,
                DefectQty = defectQty,
                PassRate = passRate
            };
        }).ToList();

        return Ok(result);
    }

    /// <summary>录入/更新设备时间记录（同一设备同一天只保留一条）</summary>
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
                Remark = req.Remark
            };
            db.EquipmentTimeRecords.Add(record);
        }
        else
        {
            record.SetupHours = req.SetupHours;
            record.RunningHours = req.RunningHours;
            record.IdleHours = req.IdleHours;
            record.Remark = req.Remark;
        }

        await db.SaveChangesAsync();

        await db.Entry(record).Reference(x => x.Equipment).LoadAsync();
        await db.Entry(record).Reference(x => x.Employee).LoadAsync();
        return Ok(ToDto(record));
    }

    /// <summary>删除设备时间记录</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var record = await db.EquipmentTimeRecords.FirstOrDefaultAsync(x => x.Id == id);
        if (record is null)
            return NotFound(new { message = "记录不存在" });

        db.EquipmentTimeRecords.Remove(record);
        await db.SaveChangesAsync();
        return Ok(new { message = "已删除" });
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