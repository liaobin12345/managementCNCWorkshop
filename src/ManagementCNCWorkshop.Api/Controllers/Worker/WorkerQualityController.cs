using System.Security.Claims;
using ManagementCNCWorkshop.Api.Data;
using ManagementCNCWorkshop.Api.Models;
using ManagementCNCWorkshop.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ManagementCNCWorkshop.Api.Controllers.Worker;

/// <summary>工人端质检录入：质检员提交抽检结果</summary>
[ApiController]
[Route("api/worker/quality")]
[Authorize(Roles = "Inspector")]
[Tags("工人端-质检")]
public class WorkerQualityController(AppDbContext db) : ControllerBase
{
    /// <summary>录入质检记录</summary>
    /// <remarks>未传 inspectorId 时默认取当前登录质检员。</remarks>
    [HttpPost]
    [ProducesResponseType(typeof(QualityRecord), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] QualityRecord record)
    {
        var inspectorId = record.InspectorId > 0
            ? record.InspectorId
            : int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var error = await ReferenceValidator.ValidateQualityRecordAsync(
            db, record.ProductId, record.WorkshopId, inspectorId);
        if (error is not null)
            return BadRequest(new { message = error });

        record.InspectorId = inspectorId;
        record.RecordDate = DateTime.UtcNow;
        db.QualityRecords.Add(record);
        await db.SaveChangesAsync();
        return Ok(record);
    }
}
