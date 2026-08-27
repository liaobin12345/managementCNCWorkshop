using System.Globalization;
using System.Security.Claims;
using ManagementCNCWorkshop.Api.Data;
using ManagementCNCWorkshop.Api.Models.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagementCNCWorkshop.Api.Controllers.Worker;

/// <summary>工人端统计：工作台概览、产量/质量统计（小程序原型页面的数据源）</summary>
[ApiController]
[Route("api/worker/stats")]
[Authorize(Roles = "Worker,Inspector,Programmer,Admin")]
[Tags("工人端-统计")]
public class WorkerStatsController(AppDbContext db) : ControllerBase
{
    /// <summary>我的车间 ID（未传 workshopId 时默认按当前员工所属车间统计）</summary>
    private int MyWorkshopId
    {
        get
        {
            var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return db.Employees.AsNoTracking().FirstOrDefault(x => x.Id == id)?.WorkshopId ?? 0;
        }
    }

    /// <summary>工作台概览：今日产量/合格率/不良</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(WorkerSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Summary([FromQuery] int? workshopId)
    {
        var wid = workshopId ?? MyWorkshopId;
        var day = DateTime.UtcNow.Date;
        var list = await db.WorkReports.AsNoTracking().ToListAsync();

        var row = list
            .Where(x => x.WorkshopId == wid && x.ReportDate.Date == day)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Sum(x => x.Quantity),
                Qualified = g.Sum(x => x.QualifiedQty),
                Defect = g.Sum(x => x.DefectQty)
            })
            .FirstOrDefault();

        return Ok(new WorkerSummaryDto
        {
            WorkshopId = wid,
            TotalQty = row?.Total ?? 0,
            QualifiedQty = row?.Qualified ?? 0,
            DefectQty = row?.Defect ?? 0,
            PassRate = row is { Total: > 0 } ? row.Qualified / row.Total : 1m
        });
    }

    /// <summary>产量日统计（按产品分组）</summary>
    [HttpGet("production/daily")]
    [ProducesResponseType(typeof(List<WorkerProductStatDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ProductionDaily([FromQuery] DateTime? date, [FromQuery] int? workshopId)
    {
        var day = (date ?? DateTime.UtcNow).Date;
        var list = await db.WorkReports.Include(x => x.Product).AsNoTracking().ToListAsync();
        if (workshopId.HasValue)
            list = list.Where(x => x.WorkshopId == workshopId).ToList();

        return Ok(GroupProduction(list.Where(x => x.ReportDate.Date == day).ToList()));
    }

    /// <summary>产量周统计（按产品分组）</summary>
    [HttpGet("production/weekly")]
    [ProducesResponseType(typeof(List<WorkerProductStatDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ProductionWeekly([FromQuery] int year, [FromQuery] int week, [FromQuery] int? workshopId)
    {
        var list = await db.WorkReports.Include(x => x.Product).AsNoTracking().ToListAsync();
        if (workshopId.HasValue)
            list = list.Where(x => x.WorkshopId == workshopId).ToList();

        return Ok(GroupProduction(list
            .Where(x => ISOWeek.GetYear(x.ReportDate) == year && ISOWeek.GetWeekOfYear(x.ReportDate) == week)
            .ToList()));
    }

    /// <summary>产量月统计（按产品分组）</summary>
    [HttpGet("production/monthly")]
    [ProducesResponseType(typeof(List<WorkerProductStatDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ProductionMonthly([FromQuery] int year, [FromQuery] int month, [FromQuery] int? workshopId)
    {
        var list = await db.WorkReports.Include(x => x.Product).AsNoTracking().ToListAsync();
        if (workshopId.HasValue)
            list = list.Where(x => x.WorkshopId == workshopId).ToList();

        return Ok(GroupProduction(list
            .Where(x => x.ReportDate.Year == year && x.ReportDate.Month == month)
            .ToList()));
    }

    /// <summary>工序报工统计：产品某道工序完成数量 + 操作工/机台产量排名</summary>
    [HttpGet("production/process")]
    [ProducesResponseType(typeof(WorkerProcessStatsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ProductionProcess(
        [FromQuery] DateTime? date,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? workshopId,
        [FromQuery] int? productId,
        [FromQuery] int? processCardId,
        [FromQuery] int? processStepNo)
    {
        var wid = workshopId ?? MyWorkshopId;
        var start = from?.Date ?? date?.Date;
        var endExclusive = to?.Date.AddDays(1) ?? (date.HasValue ? date.Value.Date.AddDays(1) : (DateTime?)null);

        var q = db.WorkReports
            .Include(x => x.Product)
            .Include(x => x.Employee)
            .Include(x => x.Equipment)
            .Where(x => x.WorkshopId == wid && x.ProcessCardId.HasValue && x.ProcessStepNo.HasValue)
            .AsNoTracking();

        if (start.HasValue) q = q.Where(x => x.ReportDate >= start.Value);
        if (endExclusive.HasValue) q = q.Where(x => x.ReportDate < endExclusive.Value);
        if (productId.HasValue) q = q.Where(x => x.ProductId == productId);
        if (processCardId.HasValue) q = q.Where(x => x.ProcessCardId == processCardId);
        if (processStepNo.HasValue) q = q.Where(x => x.ProcessStepNo == processStepNo);

        var reports = await q.ToListAsync();

        var cardIds = reports.Select(x => x.ProcessCardId!.Value).Distinct().ToList();
        var steps = await db.ProcessCardSteps.AsNoTracking()
            .Where(x => cardIds.Contains(x.ProcessCardId))
            .ToDictionaryAsync(x => (x.ProcessCardId, x.StepNo));

        var processSteps = reports
            .GroupBy(x => new { x.ProductId, x.ProcessCardId, x.ProcessStepNo })
            .Select(g =>
            {
                steps.TryGetValue((g.Key.ProcessCardId!.Value, g.Key.ProcessStepNo!.Value), out var step);
                var first = g.First();
                return new WorkerProcessStepStatDto
                {
                    ProductId = g.Key.ProductId,
                    ProductName = first.Product?.Name,
                    ProductSpec = first.Product?.Specification,
                    ProcessCardId = g.Key.ProcessCardId,
                    CardCode = null,
                    StepNo = g.Key.ProcessStepNo,
                    StepName = step?.StepName ?? first.ProcessStepName,
                    TotalQty = g.Sum(x => x.Quantity),
                    QualifiedQty = g.Sum(x => x.QualifiedQty),
                    DefectQty = g.Sum(x => x.DefectQty),
                    ReportCount = g.Count()
                };
            })
            .OrderByDescending(x => x.TotalQty)
            .ToList();

        var cardCodes = await db.ProcessCards.AsNoTracking()
            .Where(x => cardIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Code);
        foreach (var row in processSteps)
            if (row.ProcessCardId.HasValue && cardCodes.TryGetValue(row.ProcessCardId.Value, out var code)) row.CardCode = code;

        var operatorRankings = reports
            .GroupBy(x => new { x.EmployeeId, x.Employee.Name, x.Employee.EmployeeNo })
            .Select(g => new WorkerOperatorRankingDto
            {
                EmployeeId = g.Key.EmployeeId,
                EmployeeName = g.Key.Name,
                EmployeeNo = g.Key.EmployeeNo,
                TotalQty = g.Sum(x => x.Quantity),
                QualifiedQty = g.Sum(x => x.QualifiedQty),
                DefectQty = g.Sum(x => x.DefectQty),
                MachineCount = g.Where(x => x.EquipmentId.HasValue).Select(x => x.EquipmentId).Distinct().Count(),
                Machines = g.GroupBy(x => new { x.EquipmentId, Code = x.Equipment?.Code, Name = x.Equipment?.Name })
                    .Select(m => new WorkerMachineOutputDto
                    {
                        EquipmentId = m.Key.EquipmentId,
                        EquipmentCode = m.Key.Code,
                        EquipmentName = m.Key.Name,
                        TotalQty = m.Sum(x => x.Quantity),
                        QualifiedQty = m.Sum(x => x.QualifiedQty),
                        DefectQty = m.Sum(x => x.DefectQty),
                        Steps = m.GroupBy(x => new { x.ProcessCardId, x.ProcessStepNo, Name = x.ProcessStepName ?? (steps.TryGetValue((x.ProcessCardId ?? 0, x.ProcessStepNo ?? 0), out var st) ? st.StepName : null) })
                            .Select(sm =>
                            {
                                var code = sm.Key.ProcessCardId is > 0 && cardCodes.TryGetValue(sm.Key.ProcessCardId.Value, out var cd) ? cd : null;
                                return new WorkerMachineStepDto
                                {
                                    ProcessCardId = sm.Key.ProcessCardId,
                                    CardCode = code,
                                    StepNo = sm.Key.ProcessStepNo,
                                    StepName = sm.Key.Name,
                                    TotalQty = sm.Sum(x => x.Quantity),
                                    QualifiedQty = sm.Sum(x => x.QualifiedQty),
                                    DefectQty = sm.Sum(x => x.DefectQty)
                                };
                            })
                            .OrderByDescending(x => x.TotalQty)
                            .ToList()
                    })
                    .OrderByDescending(x => x.TotalQty)
                    .ToList()
            })
            .OrderByDescending(x => x.TotalQty)
            .ToList();

        return Ok(new WorkerProcessStatsDto { ProcessSteps = processSteps, OperatorRankings = operatorRankings });
    }

    /// <summary>产量趋势（最近 N 天，含今天）</summary>
    [HttpGet("production/trend")]
    [ProducesResponseType(typeof(List<WorkerTrendPointDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ProductionTrend([FromQuery] int days = 7, [FromQuery] int? workshopId = null)
    {
        var wid = workshopId ?? MyWorkshopId;
        var end = DateTime.UtcNow.Date;
        var start = end.AddDays(-(days - 1));
        var list = await db.WorkReports.AsNoTracking().ToListAsync();
        list = list.Where(x => x.WorkshopId == wid).ToList();

        var rows = list
            .Where(x => x.ReportDate.Date >= start && x.ReportDate.Date <= end)
            .GroupBy(x => x.ReportDate.Date)
            .ToDictionary(
                g => g.Key,
                g => new { Total = g.Sum(x => x.Quantity), Qualified = g.Sum(x => x.QualifiedQty), Defect = g.Sum(x => x.DefectQty) });

        var result = new List<WorkerTrendPointDto>();
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            rows.TryGetValue(d, out var r);
            result.Add(new WorkerTrendPointDto
            {
                Date = d.ToString("yyyy-MM-dd"),
                TotalQty = r?.Total ?? 0,
                QualifiedQty = r?.Qualified ?? 0,
                DefectQty = r?.Defect ?? 0
            });
        }
        return Ok(result);
    }

    /// <summary>按产品和工序统计质量</summary>
    [HttpGet("quality/process")]
    [ProducesResponseType(typeof(List<QualityProcessStatDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> QualityProcess(
        [FromQuery] DateTime? date,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? workshopId,
        [FromQuery] int? productId,
        [FromQuery] int? processStepNo)
    {
        var wid = workshopId ?? MyWorkshopId;
        var start = from?.Date ?? date?.Date;
        var end = to?.Date.AddDays(1) ?? (date.HasValue ? date.Value.Date.AddDays(1) : (DateTime?)null);
        var q = db.QualityRecords.Include(x => x.Product).Include(x => x.ProcessCard)
            .Where(x => x.WorkshopId == wid && x.ProcessCardId.HasValue && x.ProcessStepNo.HasValue)
            .AsNoTracking();
        if (start.HasValue) q = q.Where(x => x.RecordDate >= start.Value);
        if (end.HasValue) q = q.Where(x => x.RecordDate < end.Value);
        if (productId.HasValue) q = q.Where(x => x.ProductId == productId);
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

    /// <summary>质量趋势（最近 N 天，按日期）</summary>
    [HttpGet("quality/trend")]
    [Authorize(Roles = "Worker,Inspector,Programmer,Admin")]
    [ProducesResponseType(typeof(List<QualityTrendDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> QualityTrend([FromQuery] int days = 7, [FromQuery] int? workshopId = null)
    {
        var wid = workshopId ?? MyWorkshopId;
        var end = DateTime.UtcNow.Date;
        var start = end.AddDays(-(days - 1));
        var list = await db.QualityRecords.AsNoTracking().ToListAsync();
        list = list.Where(x => x.WorkshopId == wid).ToList();

        var rows = list
            .Where(x => x.RecordDate.Date >= start && x.RecordDate.Date <= end)
            .GroupBy(x => x.RecordDate.Date)
            .ToDictionary(
                g => g.Key,
                g => new { Sample = g.Sum(x => x.SampleQty), Qualified = g.Sum(x => x.QualifiedQty), Defect = g.Sum(x => x.DefectQty) });

        var result = new List<QualityTrendDto>();
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            rows.TryGetValue(d, out var r);
            var sample = r?.Sample ?? 0;
            var qualified = r?.Qualified ?? 0;
            result.Add(new QualityTrendDto
            {
                Date = d.ToString("yyyy-MM-dd"),
                SampleQty = sample,
                QualifiedQty = qualified,
                DefectQty = r?.Defect ?? 0,
                PassRate = sample > 0 ? qualified / sample : 1m
            });
        }
        return Ok(result);
    }

    /// <summary>设备产量统计（按设备分组，含当日调试/开机/待机时间）</summary>
    [HttpGet("equipment/daily")]
    [ProducesResponseType(typeof(List<WorkerEquipmentStatDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> EquipmentDaily([FromQuery] DateTime? date, [FromQuery] int? workshopId, [FromQuery] int? equipmentId)
    {
        var wid = workshopId ?? MyWorkshopId;
        var day = (date ?? DateTime.UtcNow).Date;

        var workReports = await db.WorkReports.Include(x => x.Equipment).AsNoTracking()
            .Where(x => x.WorkshopId == wid && x.ReportDate.Date == day)
            .ToListAsync();

        var timeRecords = await db.EquipmentTimeRecords.AsNoTracking()
            .Where(x => x.RecordDate == day)
            .ToListAsync();

        if (equipmentId.HasValue)
        {
            workReports = workReports.Where(x => x.EquipmentId == equipmentId).ToList();
            timeRecords = timeRecords.Where(x => x.EquipmentId == equipmentId).ToList();
        }

        // 参与统计的设备：当天有报工的 + 有时间的
        var equipmentIds = workReports
            .Where(x => x.EquipmentId.HasValue)
            .Select(x => x.EquipmentId!.Value)
            .Concat(timeRecords.Select(x => x.EquipmentId))
            .Distinct()
            .ToList();

        var equipments = await db.Equipments.AsNoTracking()
            .Where(x => equipmentIds.Contains(x.Id))
            .ToListAsync();

        var result = equipments.Select(eq =>
        {
            var reports = workReports.Where(x => x.EquipmentId == eq.Id).ToList();
            var total = reports.Sum(x => x.Quantity);
            var qualified = reports.Sum(x => x.QualifiedQty);
            var time = timeRecords.FirstOrDefault(x => x.EquipmentId == eq.Id);

            return new WorkerEquipmentStatDto
            {
                EquipmentId = eq.Id,
                EquipmentCode = eq.Code,
                EquipmentName = eq.Name,
                TotalQty = total,
                QualifiedQty = qualified,
                DefectQty = reports.Sum(x => x.DefectQty),
                PassRate = total > 0 ? qualified / total : 1m,
                SetupHours = time?.SetupHours ?? 0,
                RunningHours = time?.RunningHours ?? 0,
                IdleHours = time?.IdleHours ?? 0
            };
        }).OrderByDescending(x => x.TotalQty).ToList();

        return Ok(result);
    }

    /// <summary>质量日统计（按产品分组，所有角色可看）</summary>
    [HttpGet("quality/daily")]
    [Authorize(Roles = "Worker,Inspector,Programmer,Admin")]
    [ProducesResponseType(typeof(List<WorkerProductStatDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> QualityDaily([FromQuery] DateTime? date, [FromQuery] int? workshopId)
    {
        var day = (date ?? DateTime.UtcNow).Date;
        var list = await db.QualityRecords.Include(x => x.Product).AsNoTracking().ToListAsync();
        if (workshopId.HasValue)
            list = list.Where(x => x.WorkshopId == workshopId).ToList();

        return Ok(GroupQuality(list.Where(x => x.RecordDate.Date == day).ToList()));
    }

    /// <summary>质量周统计（按产品分组，所有角色可看）</summary>
    [HttpGet("quality/weekly")]
    [Authorize(Roles = "Worker,Inspector,Programmer,Admin")]
    [ProducesResponseType(typeof(List<WorkerProductStatDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> QualityWeekly([FromQuery] int year, [FromQuery] int week, [FromQuery] int? workshopId)
    {
        var list = await db.QualityRecords.Include(x => x.Product).AsNoTracking().ToListAsync();
        if (workshopId.HasValue)
            list = list.Where(x => x.WorkshopId == workshopId).ToList();

        return Ok(GroupQuality(list
            .Where(x => ISOWeek.GetYear(x.RecordDate) == year && ISOWeek.GetWeekOfYear(x.RecordDate) == week)
            .ToList()));
    }

    /// <summary>质量月统计（按产品分组，所有角色可看）</summary>
    [HttpGet("quality/monthly")]
    [Authorize(Roles = "Worker,Inspector,Programmer,Admin")]
    [ProducesResponseType(typeof(List<WorkerProductStatDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> QualityMonthly([FromQuery] int year, [FromQuery] int month, [FromQuery] int? workshopId)
    {
        var list = await db.QualityRecords.Include(x => x.Product).AsNoTracking().ToListAsync();
        if (workshopId.HasValue)
            list = list.Where(x => x.WorkshopId == workshopId).ToList();

        return Ok(GroupQuality(list
            .Where(x => x.RecordDate.Year == year && x.RecordDate.Month == month)
            .ToList()));
    }

    /// <summary>按产品汇总报工数据</summary>
    private static List<WorkerProductStatDto> GroupProduction(List<Models.WorkReport> list) =>
        list.GroupBy(x => x.ProductId)
            .Select(g =>
            {
                var total = g.Sum(x => x.Quantity);
                var qualified = g.Sum(x => x.QualifiedQty);
                return new WorkerProductStatDto
                {
                    ProductId = g.Key,
                    ProductName = g.First().Product?.Name,
                    ProductSpec = g.First().Product?.Specification,
                    TotalQty = total,
                    QualifiedQty = qualified,
                    DefectQty = g.Sum(x => x.DefectQty),
                    PassRate = total > 0 ? qualified / total : 1m
                };
            })
            .OrderByDescending(x => x.TotalQty)
            .ToList();

    /// <summary>按产品汇总质检数据</summary>
    private static List<WorkerProductStatDto> GroupQuality(List<Models.QualityRecord> list) =>
        list.GroupBy(x => x.ProductId)
            .Select(g =>
            {
                var total = g.Sum(x => x.SampleQty);
                var qualified = g.Sum(x => x.QualifiedQty);
                return new WorkerProductStatDto
                {
                    ProductId = g.Key,
                    ProductName = g.First().Product?.Name,
                    ProductSpec = g.First().Product?.Specification,
                    TotalQty = total,
                    QualifiedQty = qualified,
                    DefectQty = g.Sum(x => x.DefectQty),
                    PassRate = total > 0 ? qualified / total : 1m
                };
            })
            .OrderByDescending(x => x.TotalQty)
            .ToList();
}
