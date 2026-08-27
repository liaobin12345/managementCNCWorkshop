using ManagementCNCWorkshop.Api.Data;
using ManagementCNCWorkshop.Api.Models;
using ManagementCNCWorkshop.Api.Models.Dtos;
using ManagementCNCWorkshop.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ManagementCNCWorkshop.Api.Controllers.Worker;

/// <summary>工人端-工艺流转：查看工艺卡、流转工序</summary>
[ApiController]
[Route("api/worker/process")]
[Authorize(Roles = "Worker,Inspector,Programmer,Admin")]
[Tags("工人端-工艺流转")]
public class WorkerProcessController(AppDbContext db) : ControllerBase
{
    /// <summary>查询当前产品的有效工艺路线（按产品 ID 或二维码）</summary>
    [HttpGet("flows")]
    [ProducesResponseType(typeof(List<ProcessFlowDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListFlows([FromQuery] int? productId, [FromQuery] string? qrCode)
    {
        if (qrCode is not null)
        {
            var product = await db.Products.FirstOrDefaultAsync(x => x.QrCode == qrCode);
            if (product is null) return Ok(new List<ProcessFlowDto>());
            productId = product.Id;
        }

        if (productId is null) return Ok(new List<ProcessFlowDto>());

        var flows = await db.ProcessFlows
            .Include(x => x.Product)
            .Include(x => x.Steps)
            .Where(x => x.ProductId == productId && x.Status == "Active")
            .AsNoTracking()
            .OrderByDescending(x => x.Version)
            .ToListAsync();

        return Ok(flows.Select(x => new ProcessFlowDto
        {
            Id = x.Id,
            Code = x.Code,
            Name = x.Name,
            ProductId = x.ProductId,
            ProductName = x.Product?.Name,
            ProductSpec = x.Product?.Specification,
            Version = x.Version,
            Status = x.Status,
            StepCount = x.Steps.Count
        }).ToList());
    }

    /// <summary>查询当前车间/个人的流转卡列表</summary>
    [HttpGet("cards")]
    [ProducesResponseType(typeof(List<ProcessCardDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListCards([FromQuery] string? status, [FromQuery] int? productId)
    {
        var q = db.ProcessCards
            .Include(x => x.Product)
            .Include(x => x.ProcessFlow)
            .Include(x => x.Creator)
            .AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(status)) q = q.Where(x => x.Status == status);
        if (productId.HasValue) q = q.Where(x => x.ProductId == productId);

        var list = await q.OrderByDescending(x => x.Id).Take(50).ToListAsync();
        return Ok(list.Select(x => new ProcessCardDto
        {
            Id = x.Id,
            Code = x.Code,
            ProcessFlowId = x.ProcessFlowId,
            FlowName = x.ProcessFlow?.Name,
            ProductId = x.ProductId,
            ProductName = x.Product?.Name,
            ProductSpec = x.Product?.Specification,
            Quantity = x.Quantity,
            MaterialSpec = x.MaterialSpec,
            SurfaceTreatment = x.SurfaceTreatment,
            Status = x.Status,
            CurrentStepNo = x.CurrentStepNo,
            CreatedById = x.CreatedById,
            CreatorName = x.Creator?.Name,
            CreatedAt = x.CreatedAt,
            StartedAt = x.StartedAt,
            CompletedAt = x.CompletedAt,
            Remark = x.Remark
        }).ToList());
    }

    /// <summary>产品工序进度总览（某产品所有流转卡的工序完成量与完工标识）</summary>
    [HttpGet("progress")]
    [ProducesResponseType(typeof(List<ProcessCardProgressDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Progress([FromQuery] int? productId, [FromQuery] string? status)
    {
        var q = db.ProcessCards
            .Include(x => x.Product)
            .Include(x => x.ProcessFlow)
            .Include(x => x.CardSteps).ThenInclude(s => s.Operator)
            .Include(x => x.CardSteps).ThenInclude(s => s.Inspector)
            .AsNoTracking().AsQueryable();

        if (productId.HasValue) q = q.Where(x => x.ProductId == productId);
        if (!string.IsNullOrEmpty(status)) q = q.Where(x => x.Status == status);

        var cards = await q.OrderByDescending(x => x.Id).Take(50).ToListAsync();
        return Ok(await ProcessCardProgressBuilder.BuildAsync(db, cards));
    }

    /// <summary>流转卡详情（含工序执行记录与报工完成数量）</summary>
    [HttpGet("cards/{id}")]
    [ProducesResponseType(typeof(ProcessCardDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CardDetail(int id)
    {
        var card = await db.ProcessCards
            .Include(x => x.Product)
            .Include(x => x.ProcessFlow)
            .Include(x => x.Creator)
            .Include(x => x.CardSteps).ThenInclude(s => s.Operator)
            .Include(x => x.CardSteps).ThenInclude(s => s.Inspector)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
        if (card is null) return NotFound(new { message = "流转卡不存在" });

        var sums = await db.WorkReports
            .Where(r => r.ProcessCardId == id && r.ProcessStepNo.HasValue)
            .GroupBy(r => r.ProcessStepNo)
            .Select(g => new { StepNo = g.Key, Qty = g.Sum(r => r.Quantity), QtyQ = g.Sum(r => r.QualifiedQty), QtyD = g.Sum(r => r.DefectQty) })
            .ToListAsync();
        var sumMap = sums.ToDictionary(k => k.StepNo ?? 0, v => (v.Qty, v.QtyQ, v.QtyD));

        return Ok(new ProcessCardDetailDto
        {
            Id = card.Id,
            Code = card.Code,
            ProcessFlowId = card.ProcessFlowId,
            FlowName = card.ProcessFlow?.Name,
            ProductId = card.ProductId,
            ProductName = card.Product?.Name,
            ProductSpec = card.Product?.Specification,
            Quantity = card.Quantity,
            MaterialSpec = card.MaterialSpec,
            SurfaceTreatment = card.SurfaceTreatment,
            Status = card.Status,
            CurrentStepNo = card.CurrentStepNo,
            CreatedById = card.CreatedById,
            CreatorName = card.Creator?.Name,
            CreatedAt = card.CreatedAt,
            StartedAt = card.StartedAt,
            CompletedAt = card.CompletedAt,
            Remark = card.Remark,
            CardSteps = card.CardSteps.OrderBy(s => s.StepNo).Select(s => new ProcessCardStepDto
            {
                Id = s.Id,
                ProcessCardId = s.ProcessCardId,
                StepNo = s.StepNo,
                StepName = s.StepName,
                Status = s.Status,
                StartedAt = s.StartedAt,
                CompletedAt = s.CompletedAt,
                OperatorId = s.OperatorId,
                OperatorName = s.Operator?.Name,
                InspectorId = s.InspectorId,
                InspectorName = s.Inspector?.Name,
                MachineNo = s.MachineNo,
                WorkDate = s.WorkDate?.ToString("yyyy-MM-dd"),
                Shift = s.Shift,
                Quantity = s.Quantity,
                Remark = s.Remark,
                CompletedQty = sumMap.TryGetValue(s.StepNo, out var sum) ? sum.Qty : 0,
                QualifiedQty = sumMap.TryGetValue(s.StepNo, out var qq) ? qq.QtyQ : 0,
                DefectQty = sumMap.TryGetValue(s.StepNo, out var qd) ? qd.QtyD : 0
            }).ToList()
        });
    }

    /// <summary>工人开始/完成当前工序（流转到下一步）</summary>
    [HttpPost("cards/{id}/advance")]
    [ProducesResponseType(typeof(ProcessCardDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> AdvanceCard(int id)
    {
        var employeeId = int.Parse(User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)!);

        var card = await db.ProcessCards
            .Include(x => x.CardSteps)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (card is null) return NotFound(new { message = "流转卡不存在" });
        if (card.Status == "Completed")
            return BadRequest(new { message = "流转卡已完成" });

        var totalSteps = card.CardSteps.Count;
        var next = card.CurrentStepNo + 1;

        if (next > totalSteps)
        {
            await CompleteCardAsync(card);
            return await CardDetail(card.Id);
        }

        var step = card.CardSteps.FirstOrDefault(x => x.StepNo == next);
        if (step is null) return BadRequest(new { message = "工序数据异常" });

        var now = DateTime.UtcNow;
        var prev = card.CardSteps.FirstOrDefault(x => x.StepNo == card.CurrentStepNo);
        if (prev is { Status: "Running" })
        {
            prev.Status = "Completed";
            prev.CompletedAt = now;
        }

        step.Status = step.Status == "Pending" ? "Running" : step.Status;
        step.StartedAt ??= now;
        step.OperatorId = employeeId;
        card.CurrentStepNo = next;
        card.StartedAt ??= now;

        if (next >= totalSteps && card.CardSteps.All(x => x.StepNo <= next))
            await CompleteCardAsync(card);

        await db.SaveChangesAsync();
        return await CardDetail(card.Id);
    }

    private async Task CompleteCardAsync(ProcessCard card)
    {
        var now = DateTime.UtcNow;
        card.Status = "Completed";
        card.CompletedAt = now;
        card.CurrentStepNo = card.CardSteps.Count;
        foreach (var step in card.CardSteps.Where(x => x.Status != "Completed"))
        {
            step.Status = "Completed";
            step.CompletedAt ??= now;
        }
        await db.SaveChangesAsync();
    }
}