using ManagementCNCWorkshop.Api.Data;
using ManagementCNCWorkshop.Api.Models;
using ManagementCNCWorkshop.Api.Models.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagementCNCWorkshop.Api.Controllers.Admin;

/// <summary>运营后台-设备点检：点检记录查询与删除</summary>
[ApiController]
[Route("api/admin/equipment-inspections")]
[Authorize(Roles = "Admin")]
[Tags("运营后台-设备点检")]
public class AdminEquipmentInspectionController(AppDbContext db) : ControllerBase
{
    /// <summary>分页查询点检记录</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<EquipmentInspection>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] DateTime? date,
        [FromQuery] int? equipmentId,
        [FromQuery] int? workshopId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var q = db.EquipmentInspections
            .Include(x => x.Equipment)
            .Include(x => x.Inspector)
            .Include(x => x.Items)
            .AsQueryable();

        if (date.HasValue) q = q.Where(x => x.InspectDate == date.Value.Date);
        if (equipmentId.HasValue) q = q.Where(x => x.EquipmentId == equipmentId);
        if (workshopId.HasValue) q = q.Where(x => x.Equipment!.WorkshopId == workshopId);

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(x => x.InspectDate)
            .ThenByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PagedResult<EquipmentInspection> { Total = total, Page = page, PageSize = pageSize, Items = items });
    }

    /// <summary>删除点检记录（纠错用，物理删除）</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var record = await db.EquipmentInspections.FirstOrDefaultAsync(x => x.Id == id);
        if (record is null)
            return NotFound(new { message = "点检记录不存在" });

        db.EquipmentInspections.Remove(record);
        await db.SaveChangesAsync();
        return Ok(new { message = "已删除" });
    }
}
