using System.Globalization;
using ManagementCNCWorkshop.Api.Data;
using ManagementCNCWorkshop.Api.Models;
using ManagementCNCWorkshop.Api.Models.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagementCNCWorkshop.Api.Controllers.Admin;

/// <summary>运营后台产量统计：按日/周/月汇总</summary>
[ApiController]
[Route("api/admin/stats/production")]
[Authorize(Roles = "Admin")]
[Tags("运营后台-产量统计")]
public class AdminProductionStatsController(AppDbContext db) : ControllerBase
{
    /// <summary>产量日统计</summary>
    [HttpGet("daily")]
    [ProducesResponseType(typeof(List<ProductionStatDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Daily([FromQuery] DateTime? date, [FromQuery] int? workshopId)
    {
        var day = (date ?? DateTime.UtcNow).Date;
        var list = await db.WorkReports.AsNoTracking().ToListAsync();
        if (workshopId.HasValue)
            list = list.Where(x => x.WorkshopId == workshopId).ToList();

        var result = list
            .Where(x => x.ReportDate.Date == day)
            .GroupBy(x => new { x.WorkshopId, x.ProductId })
            .Select(g => new ProductionStatDto
            {
                WorkshopId = g.Key.WorkshopId,
                ProductId = g.Key.ProductId,
                TotalQty = g.Sum(x => x.Quantity),
                QualifiedQty = g.Sum(x => x.QualifiedQty),
                DefectQty = g.Sum(x => x.DefectQty)
            })
            .ToList();

        return Ok(result);
    }

    /// <summary>产量周统计</summary>
    [HttpGet("weekly")]
    [ProducesResponseType(typeof(List<ProductionStatDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Weekly([FromQuery] int year, [FromQuery] int week, [FromQuery] int? workshopId)
    {
        var list = await db.WorkReports.AsNoTracking().ToListAsync();
        if (workshopId.HasValue)
            list = list.Where(x => x.WorkshopId == workshopId).ToList();

        var result = list
            .Where(x => ISOWeek.GetYear(x.ReportDate) == year && ISOWeek.GetWeekOfYear(x.ReportDate) == week)
            .GroupBy(x => new { x.WorkshopId, x.ProductId })
            .Select(g => new ProductionStatDto
            {
                WorkshopId = g.Key.WorkshopId,
                ProductId = g.Key.ProductId,
                TotalQty = g.Sum(x => x.Quantity),
                QualifiedQty = g.Sum(x => x.QualifiedQty),
                DefectQty = g.Sum(x => x.DefectQty)
            })
            .ToList();

        return Ok(result);
    }

    /// <summary>产量月统计</summary>
    [HttpGet("monthly")]
    [ProducesResponseType(typeof(List<ProductionStatDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Monthly([FromQuery] int year, [FromQuery] int month, [FromQuery] int? workshopId)
    {
        var list = await db.WorkReports.AsNoTracking().ToListAsync();
        if (workshopId.HasValue)
            list = list.Where(x => x.WorkshopId == workshopId).ToList();

        var result = list
            .Where(x => x.ReportDate.Year == year && x.ReportDate.Month == month)
            .GroupBy(x => new { x.WorkshopId, x.ProductId })
            .Select(g => new ProductionStatDto
            {
                WorkshopId = g.Key.WorkshopId,
                ProductId = g.Key.ProductId,
                TotalQty = g.Sum(x => x.Quantity),
                QualifiedQty = g.Sum(x => x.QualifiedQty),
                DefectQty = g.Sum(x => x.DefectQty)
            })
            .ToList();

        return Ok(result);
    }
}
