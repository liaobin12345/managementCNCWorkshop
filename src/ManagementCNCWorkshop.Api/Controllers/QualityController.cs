using System.Globalization;
using ManagementCNCWorkshop.Api.Data;
using ManagementCNCWorkshop.Api.Models;
using ManagementCNCWorkshop.Api.Models.Dtos;
using ManagementCNCWorkshop.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagementCNCWorkshop.Api.Controllers;

/// <summary>质量追踪：录入质检记录，按日/周/月统计合格率</summary>
[ApiController]
[Route("api/quality")]
[Tags("质量追踪")]
public class QualityController(AppDbContext db) : ControllerBase
{
    /// <summary>录入质量检验记录</summary>
    /// <remarks>质检员提交抽检结果，RecordDate 会自动设为当前时间。</remarks>
    /// <param name="record">质量记录</param>
    /// <returns>保存后的质量记录</returns>
    [HttpPost]
    [ProducesResponseType(typeof(QualityRecord), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] QualityRecord record)
    {
        var error = await ReferenceValidator.ValidateQualityRecordAsync(
            db, record.ProductId, record.WorkshopId, record.InspectorId);
        if (error is not null)
            return BadRequest(new { message = error, tip = "可先调用 GET /api/master/demo-info 获取可用 ID" });

        record.RecordDate = DateTime.UtcNow;
        db.QualityRecords.Add(record);
        await db.SaveChangesAsync();
        return Ok(record);
    }

    /// <summary>质量日统计</summary>
    /// <param name="date">统计日期，不传则默认今天</param>
    /// <param name="workshopId">车间 ID（可选）</param>
    /// <returns>按车间+产品分组的质检汇总，含合格率 PassRate</returns>
    [HttpGet("stats/daily")]
    [ProducesResponseType(typeof(List<QualityStatDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DailyStats([FromQuery] DateTime? date, [FromQuery] int? workshopId)
    {
        var day = (date ?? DateTime.UtcNow).Date;
        var q = db.QualityRecords.Where(x => x.RecordDate.Date == day);
        if (workshopId.HasValue) q = q.Where(x => x.WorkshopId == workshopId);

        var result = await q.GroupBy(x => new { x.WorkshopId, x.ProductId })
            .Select(g => new QualityStatDto
            {
                WorkshopId = g.Key.WorkshopId,
                ProductId = g.Key.ProductId,
                SampleQty = g.Sum(x => x.SampleQty),
                QualifiedQty = g.Sum(x => x.QualifiedQty),
                DefectQty = g.Sum(x => x.DefectQty),
                PassRate = g.Sum(x => x.SampleQty) == 0 ? 0 :
                    g.Sum(x => x.QualifiedQty) / g.Sum(x => x.SampleQty)
            }).ToListAsync();

        return Ok(result);
    }

    /// <summary>质量周统计</summary>
    /// <param name="year">年份</param>
    /// <param name="week">ISO 周数（1~53）</param>
    /// <param name="workshopId">车间 ID（可选）</param>
    /// <returns>按车间+产品分组的周质检汇总</returns>
    [HttpGet("stats/weekly")]
    [ProducesResponseType(typeof(List<QualityStatDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> WeeklyStats([FromQuery] int year, [FromQuery] int week, [FromQuery] int? workshopId)
    {
        var q = db.QualityRecords.AsQueryable();
        if (workshopId.HasValue) q = q.Where(x => x.WorkshopId == workshopId);

        var list = await q.ToListAsync();
        var result = list
            .Where(x => ISOWeek.GetYear(x.RecordDate) == year && ISOWeek.GetWeekOfYear(x.RecordDate) == week)
            .GroupBy(x => new { x.WorkshopId, x.ProductId })
            .Select(g => new QualityStatDto
            {
                WorkshopId = g.Key.WorkshopId,
                ProductId = g.Key.ProductId,
                SampleQty = g.Sum(x => x.SampleQty),
                QualifiedQty = g.Sum(x => x.QualifiedQty),
                DefectQty = g.Sum(x => x.DefectQty),
                PassRate = g.Sum(x => x.SampleQty) == 0 ? 0 :
                    g.Sum(x => x.QualifiedQty) / g.Sum(x => x.SampleQty)
            }).ToList();

        return Ok(result);
    }

    /// <summary>质量月统计</summary>
    /// <param name="year">年份</param>
    /// <param name="month">月份（1~12）</param>
    /// <param name="workshopId">车间 ID（可选）</param>
    /// <returns>按车间+产品分组的月质检汇总</returns>
    [HttpGet("stats/monthly")]
    [ProducesResponseType(typeof(List<QualityStatDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MonthlyStats([FromQuery] int year, [FromQuery] int month, [FromQuery] int? workshopId)
    {
        var q = db.QualityRecords.Where(x => x.RecordDate.Year == year && x.RecordDate.Month == month);
        if (workshopId.HasValue) q = q.Where(x => x.WorkshopId == workshopId);

        var result = await q.GroupBy(x => new { x.WorkshopId, x.ProductId })
            .Select(g => new QualityStatDto
            {
                WorkshopId = g.Key.WorkshopId,
                ProductId = g.Key.ProductId,
                SampleQty = g.Sum(x => x.SampleQty),
                QualifiedQty = g.Sum(x => x.QualifiedQty),
                DefectQty = g.Sum(x => x.DefectQty),
                PassRate = g.Sum(x => x.SampleQty) == 0 ? 0 :
                    g.Sum(x => x.QualifiedQty) / g.Sum(x => x.SampleQty)
            }).ToListAsync();

        return Ok(result);
    }
}
