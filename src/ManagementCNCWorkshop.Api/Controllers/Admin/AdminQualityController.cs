using System.Globalization;
using ManagementCNCWorkshop.Api.Data;
using ManagementCNCWorkshop.Api.Models;
using ManagementCNCWorkshop.Api.Models.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagementCNCWorkshop.Api.Controllers.Admin;

/// <summary>运营后台质量：质检记录查询与统计</summary>
[ApiController]
[Route("api/admin/quality")]
[Authorize(Roles = "Admin")]
[Tags("运营后台-质量")]
public class AdminQualityController(AppDbContext db) : ControllerBase
{
    /// <summary>分页查询质检记录</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<QualityRecord>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] DateTime? date,
        [FromQuery] int? workshopId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var q = db.QualityRecords
            .Include(x => x.Product)
            .Include(x => x.Workshop)
            .Include(x => x.Inspector)
            .Include(x => x.Equipment)
            .AsQueryable();

        if (date.HasValue)
            q = q.Where(x => x.RecordDate.Date == date.Value.Date);
        if (workshopId.HasValue)
            q = q.Where(x => x.WorkshopId == workshopId);

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(x => x.RecordDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PagedResult<QualityRecord> { Total = total, Page = page, PageSize = pageSize, Items = items });
    }

    /// <summary>删除质检记录（纠错用，物理删除）</summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var record = await db.QualityRecords.FirstOrDefaultAsync(x => x.Id == id);
        if (record is null)
            return NotFound(new { message = "质检记录不存在" });

        db.QualityRecords.Remove(record);
        await db.SaveChangesAsync();
        return Ok(new { message = "已删除" });
    }

    /// <summary>质量日统计</summary>
    [HttpGet("stats/daily")]
    [ProducesResponseType(typeof(List<QualityStatDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DailyStats([FromQuery] DateTime? date, [FromQuery] int? workshopId)
    {
        var day = (date ?? DateTime.UtcNow).Date;
        var q = db.QualityRecords.Where(x => x.RecordDate.Date == day);
        if (workshopId.HasValue) q = q.Where(x => x.WorkshopId == workshopId);

        return Ok(await AggregateAsync(q));
    }

    /// <summary>按产品与工序统计质量</summary>
    [HttpGet("stats/process")]
    [ProducesResponseType(typeof(List<QualityProcessStatDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ProcessStats(
        [FromQuery] DateTime? date,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? workshopId,
        [FromQuery] int? productId,
        [FromQuery] int? processCardId,
        [FromQuery] int? processStepNo)
    {
        var q = db.QualityRecords
            .Include(x => x.Product)
            .Include(x => x.ProcessCard)
            .Where(x => x.ProcessCardId.HasValue && x.ProcessStepNo.HasValue)
            .AsNoTracking();
        var start = from?.Date ?? date?.Date;
        var end = to?.Date.AddDays(1) ?? (date.HasValue ? date.Value.Date.AddDays(1) : (DateTime?)null);
        if (start.HasValue) q = q.Where(x => x.RecordDate >= start.Value);
        if (end.HasValue) q = q.Where(x => x.RecordDate < end.Value);
        if (workshopId.HasValue) q = q.Where(x => x.WorkshopId == workshopId);
        if (productId.HasValue) q = q.Where(x => x.ProductId == productId);
        if (processCardId.HasValue) q = q.Where(x => x.ProcessCardId == processCardId);
        if (processStepNo.HasValue) q = q.Where(x => x.ProcessStepNo == processStepNo);

        var records = await q.ToListAsync();
        return Ok(records.GroupBy(x => new { x.ProductId, x.ProcessCardId, x.ProcessStepNo, x.ProcessStepName })
            .Select(g => new QualityProcessStatDto
            {
                ProductId = g.Key.ProductId,
                ProductName = g.First().Product?.Name,
                ProductSpec = g.First().Product?.Specification,
                ProcessCardId = g.Key.ProcessCardId,
                CardCode = g.First().ProcessCard?.Code,
                ProcessStepNo = g.Key.ProcessStepNo,
                ProcessStepName = g.Key.ProcessStepName,
                SampleQty = g.Sum(x => x.SampleQty),
                QualifiedQty = g.Sum(x => x.QualifiedQty),
                DefectQty = g.Sum(x => x.DefectQty)
            }).OrderByDescending(x => x.DefectQty).ToList());
    }

    /// <summary>质量周统计</summary>
    [HttpGet("stats/weekly")]
    [ProducesResponseType(typeof(List<QualityStatDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> WeeklyStats([FromQuery] int year, [FromQuery] int week, [FromQuery] int? workshopId)
    {
        var q = db.QualityRecords.AsQueryable();
        if (workshopId.HasValue) q = q.Where(x => x.WorkshopId == workshopId);

        var list = await q.ToListAsync();
        var filtered = list
            .Where(x => ISOWeek.GetYear(x.RecordDate) == year && ISOWeek.GetWeekOfYear(x.RecordDate) == week);
        return Ok(AggregateInMemory(filtered));
    }

    /// <summary>质量月统计</summary>
    [HttpGet("stats/monthly")]
    [ProducesResponseType(typeof(List<QualityStatDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MonthlyStats([FromQuery] int year, [FromQuery] int month, [FromQuery] int? workshopId)
    {
        var q = db.QualityRecords.Where(x => x.RecordDate.Year == year && x.RecordDate.Month == month);
        if (workshopId.HasValue) q = q.Where(x => x.WorkshopId == workshopId);

        return Ok(await AggregateAsync(q));
    }

    private static async Task<List<QualityStatDto>> AggregateAsync(IQueryable<QualityRecord> q)
    {
        var list = await q.AsNoTracking().ToListAsync();
        return AggregateInMemory(list);
    }

    private static List<QualityStatDto> AggregateInMemory(IEnumerable<QualityRecord> source) =>
        source.GroupBy(x => new { x.WorkshopId, x.ProductId, x.ProcessCardId, x.ProcessStepNo, x.ProcessStepName })
            .Select(g => new QualityStatDto
            {
                WorkshopId = g.Key.WorkshopId,
                ProductId = g.Key.ProductId,
                ProcessCardId = g.Key.ProcessCardId,
                ProcessStepNo = g.Key.ProcessStepNo,
                ProcessStepName = g.Key.ProcessStepName,
                SampleQty = g.Sum(x => x.SampleQty),
                QualifiedQty = g.Sum(x => x.QualifiedQty),
                DefectQty = g.Sum(x => x.DefectQty),
                PassRate = g.Sum(x => x.SampleQty) == 0 ? 0 :
                    g.Sum(x => x.QualifiedQty) / g.Sum(x => x.SampleQty)
            }).ToList();
}
