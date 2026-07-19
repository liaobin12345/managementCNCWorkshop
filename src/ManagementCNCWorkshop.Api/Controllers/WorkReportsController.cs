using ManagementCNCWorkshop.Api.Data;
using ManagementCNCWorkshop.Api.Models;
using ManagementCNCWorkshop.Api.Models.Dtos;
using ManagementCNCWorkshop.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagementCNCWorkshop.Api.Controllers;

/// <summary>扫码报工：小程序扫码后提交产量数据</summary>
[ApiController]
[Route("api/work-reports")]
[Tags("扫码报工")]
public class WorkReportsController(AppDbContext db) : ControllerBase
{
    /// <summary>扫码报工</summary>
    /// <remarks>
    /// 小程序扫描产品/设备二维码后，提交本次生产的产量数据。
    /// 数据写入 WorkReports 表，供产量日/周/月统计使用。
    /// </remarks>
    /// <param name="req">报工请求体</param>
    /// <returns>保存后的报工记录（含自动生成的 Id）</returns>
    [HttpPost("scan")]
    [ProducesResponseType(typeof(WorkReport), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Scan([FromBody] ScanWorkReportRequest req)
    {
        // Swagger 可能填 0，0 不是有效外键，当作未选设备
        var equipmentId = req.EquipmentId is > 0 ? req.EquipmentId : null;

        var error = await ReferenceValidator.ValidateWorkReportAsync(
            db, req.ProductId, req.WorkshopId, req.EmployeeId, equipmentId);
        if (error is not null)
            return BadRequest(new { message = error, tip = "可先调用 GET /api/master/demo-info 获取当前数据库中的可用 ID" });

        var report = new WorkReport
        {
            ProductId = req.ProductId,
            WorkshopId = req.WorkshopId,
            EmployeeId = req.EmployeeId,
            EquipmentId = equipmentId,
            ReportDate = req.ReportDate ?? DateTime.UtcNow,
            Quantity = req.Quantity,
            QualifiedQty = req.QualifiedQty,
            DefectQty = req.DefectQty,
            ScanPayload = req.ScanPayload,
            Remark = req.Remark
        };
        db.WorkReports.Add(report);
        await db.SaveChangesAsync();
        return Ok(report);
    }

    /// <summary>查询报工记录列表</summary>
    /// <param name="date">按日期筛选，格式 yyyy-MM-dd，不传则查全部</param>
    /// <param name="workshopId">按车间筛选（可选）</param>
    /// <returns>最近 100 条报工记录，按时间倒序</returns>
    [HttpGet]
    [ProducesResponseType(typeof(List<WorkReport>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] DateTime? date, [FromQuery] int? workshopId)
    {
        var q = db.WorkReports.AsQueryable();
        if (date.HasValue) q = q.Where(x => x.ReportDate.Date == date.Value.Date);
        if (workshopId.HasValue) q = q.Where(x => x.WorkshopId == workshopId);
        return Ok(await q.OrderByDescending(x => x.ReportDate).Take(100).ToListAsync());
    }
}
