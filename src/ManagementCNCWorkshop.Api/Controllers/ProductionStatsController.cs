using System.Globalization;
using ManagementCNCWorkshop.Api.Data;
using ManagementCNCWorkshop.Api.Models.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagementCNCWorkshop.Api.Controllers;

/// <summary>产量统计：从报工记录按日/周/月汇总</summary>
[ApiController]
[Route("api/stats/production")]
[Tags("产量统计")]
public class ProductionStatsController(AppDbContext db) : ControllerBase
{
    /// <summary>产量日统计</summary>
    /// <param name="date">统计日期，不传则默认今天</param>
    /// <param name="workshopId">车间 ID（可选，不传则统计全部车间）</param>
    /// <returns>按车间+产品分组的产量汇总</returns>
    [HttpGet("daily")]
    [ProducesResponseType(typeof(List<ProductionStatDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Daily([FromQuery] DateTime? date, [FromQuery] int? workshopId)
    {
        var day = (date ?? DateTime.UtcNow).Date;
        var q = db.WorkReports.Where(x => x.ReportDate.Date == day);
        if (workshopId.HasValue) q = q.Where(x => x.WorkshopId == workshopId);

        var result = await q.GroupBy(x => new { x.WorkshopId, x.ProductId })
            .Select(g => new ProductionStatDto
            {
                WorkshopId = g.Key.WorkshopId,
                ProductId = g.Key.ProductId,
                TotalQty = g.Sum(x => x.Quantity),
                QualifiedQty = g.Sum(x => x.QualifiedQty),
                DefectQty = g.Sum(x => x.DefectQty)
            }).ToListAsync();

        return Ok(result);
    }

    /// <summary>产量周统计</summary>
    /// <param name="year">年份，如 2026</param>
    /// <param name="week">ISO 周数（1~53），如 29</param>
    /// <param name="workshopId">车间 ID（可选）</param>
    /// <returns>按车间+产品分组的周产量汇总</returns>
    [HttpGet("weekly")]
    [ProducesResponseType(typeof(List<ProductionStatDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Weekly([FromQuery] int year, [FromQuery] int week, [FromQuery] int? workshopId)
    {
        var q = db.WorkReports.AsQueryable();
        if (workshopId.HasValue) q = q.Where(x => x.WorkshopId == workshopId);

        var list = await q.ToListAsync();
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
            }).ToList();

        return Ok(result);
    }

    /// <summary>产量月统计</summary>
    /// <param name="year">年份，如 2026</param>
    /// <param name="month">月份（1~12）</param>
    /// <param name="workshopId">车间 ID（可选）</param>
    /// <returns>按车间+产品分组的月产量汇总</returns>
    [HttpGet("monthly")]
    [ProducesResponseType(typeof(List<ProductionStatDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Monthly([FromQuery] int year, [FromQuery] int month, [FromQuery] int? workshopId)
    {
        var q = db.WorkReports.Where(x => x.ReportDate.Year == year && x.ReportDate.Month == month);
        if (workshopId.HasValue) q = q.Where(x => x.WorkshopId == workshopId);

        var result = await q.GroupBy(x => new { x.WorkshopId, x.ProductId })
            .Select(g => new ProductionStatDto
            {
                WorkshopId = g.Key.WorkshopId,
                ProductId = g.Key.ProductId,
                TotalQty = g.Sum(x => x.Quantity),
                QualifiedQty = g.Sum(x => x.QualifiedQty),
                DefectQty = g.Sum(x => x.DefectQty)
            }).ToListAsync();

        return Ok(result);
    }
}
