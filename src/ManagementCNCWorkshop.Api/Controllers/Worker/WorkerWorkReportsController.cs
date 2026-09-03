using System.Security.Claims;
using ManagementCNCWorkshop.Api.Data;
using ManagementCNCWorkshop.Api.Models;
using ManagementCNCWorkshop.Api.Models.Dtos;
using ManagementCNCWorkshop.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagementCNCWorkshop.Api.Controllers.Worker;

/// <summary>工人端扫码报工：提交产量、查询我的报工记录</summary>
[ApiController]
[Route("api/worker/work-reports")]
[Authorize(Roles = "Worker,Inspector,Programmer,Admin")]
[Tags("工人端-扫码报工")]
public class WorkerWorkReportsController(AppDbContext db, TenantContext tenant) : ControllerBase
{
    /// <summary>扫码报工</summary>
    /// <remarks>
    /// 扫码后提交本次生产的产量数据，写入 WorkReports 表，供运营后台产量统计使用。
    /// 未传 employeeId 时默认取当前登录用户。
    /// </remarks>
    [HttpPost("scan")]
    [ProducesResponseType(typeof(WorkReport), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Scan([FromBody] ScanWorkReportRequest req)
    {
        // 0 或未传时默认当前登录员工
        var employeeId = req.EmployeeId > 0
            ? req.EmployeeId
            : int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // 0 不是有效外键，当作未选设备
        var equipmentId = req.EquipmentId is > 0 ? req.EquipmentId : null;

        // 租户用户强制归属当前车间（防止跨车间写入）；管理员按请求指定
        var workshopId = tenant.WorkshopId ?? req.WorkshopId;

        var error = await ReferenceValidator.ValidateWorkReportAsync(
            db, req.ProductId, workshopId, employeeId, equipmentId);
        if (error is not null)
            return BadRequest(new { message = error });

        // 如果带工序关联，校验流转卡与工序，并冗余工序名
        string? processStepName = null;
        if (req.ProcessCardId is > 0)
        {
            var card = await db.ProcessCards
                .Include(x => x.CardSteps)
                .FirstOrDefaultAsync(x => x.Id == req.ProcessCardId);
            if (card is null)
                return BadRequest(new { message = "工艺流转卡不存在" });
            if (card.ProductId != req.ProductId)
                return BadRequest(new { message = "流转卡与产品不匹配" });
            if (card.Status == "Completed")
                return BadRequest(new { message = "该批次已完工，不能再报工" });

            if (req.ProcessStepNo is > 0)
            {
                var step = card.CardSteps.FirstOrDefault(x => x.StepNo == req.ProcessStepNo);
                if (step is null)
                    return BadRequest(new { message = $"流转卡中没有第 {req.ProcessStepNo} 道工序" });
                processStepName = step.StepName;
            }
        }

        var report = new WorkReport
        {
            ProductId = req.ProductId,
            WorkshopId = workshopId,
            EmployeeId = employeeId,
            EquipmentId = equipmentId,
            ReportDate = req.ReportDate ?? DateTime.UtcNow,
            Quantity = req.Quantity,
            QualifiedQty = req.QualifiedQty,
            DefectQty = req.DefectQty,
            ScanPayload = req.ScanPayload,
            ProcessCardId = req.ProcessCardId,
            ProcessStepNo = req.ProcessStepNo,
            ProcessStepName = processStepName,
            Remark = req.Remark
        };
        db.WorkReports.Add(report);
        await db.SaveChangesAsync();
        return Ok(report);
    }

    /// <summary>查询我的报工记录（分页）</summary>
    [HttpGet("my")]
    [ProducesResponseType(typeof(PagedResult<WorkReport>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MyReports([FromQuery] DateTime? date, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var employeeId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var q = db.WorkReports
            .Include(x => x.Product)
            .Include(x => x.Equipment)
            .Where(x => x.EmployeeId == employeeId);

        if (date.HasValue)
            q = q.Where(x => x.ReportDate.Date == date.Value.Date);

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(x => x.ReportDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new PagedResult<WorkReport> { Total = total, Page = page, PageSize = pageSize, Items = items });
    }
}
