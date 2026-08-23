using ManagementCNCWorkshop.Api.Data;
using ManagementCNCWorkshop.Api.Models;
using ManagementCNCWorkshop.Api.Models.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagementCNCWorkshop.Api.Controllers.Admin;

/// <summary>运营后台报工记录查询（分页）</summary>
[ApiController]
[Route("api/admin/work-reports")]
[Authorize(Roles = "Admin")]
[Tags("运营后台-报工记录")]
public class AdminWorkReportsController(AppDbContext db) : ControllerBase
{
    /// <summary>分页查询报工记录</summary>
    /// <param name="date">按日期筛选（可选）</param>
    /// <param name="workshopId">按车间筛选（可选）</param>
    /// <param name="productId">按产品筛选（可选）</param>
    /// <param name="page">页码，从 1 开始</param>
    /// <param name="pageSize">每页条数</param>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<WorkReport>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] DateTime? date,
        [FromQuery] int? workshopId,
        [FromQuery] int? productId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var q = db.WorkReports
            .Include(x => x.Product)
            .Include(x => x.Workshop)
            .Include(x => x.Employee)
            .Include(x => x.Equipment)
            .AsQueryable();

        if (date.HasValue)
            q = q.Where(x => x.ReportDate.Date == date.Value.Date);
        if (workshopId.HasValue)
            q = q.Where(x => x.WorkshopId == workshopId);
        if (productId.HasValue)
            q = q.Where(x => x.ProductId == productId);

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(x => x.ReportDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PagedResult<WorkReport> { Total = total, Page = page, PageSize = pageSize, Items = items });
    }
}
