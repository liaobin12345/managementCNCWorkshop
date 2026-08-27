using ManagementCNCWorkshop.Api.Data;
using ManagementCNCWorkshop.Api.Models;
using ManagementCNCWorkshop.Api.Models.Dtos;
using ManagementCNCWorkshop.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagementCNCWorkshop.Api.Controllers.Admin;

/// <summary>运营后台-工艺管理：工艺路线设计与工艺流转卡</summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin,Programmer")]
[Tags("运营后台-工艺管理")]
public class AdminProcessController(AppDbContext db) : ControllerBase
{
    // ─────────────── 工艺路线 ───────────────

    /// <summary>工艺路线列表</summary>
    [HttpGet("process-flows")]
    [ProducesResponseType(typeof(List<ProcessFlowDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListFlows([FromQuery] int? productId, [FromQuery] string? status)
    {
        var q = db.ProcessFlows
            .Include(x => x.Product)
            .Include(x => x.Creator)
            .Include(x => x.Steps)
            .AsNoTracking().AsQueryable();

        if (productId.HasValue) q = q.Where(x => x.ProductId == productId);
        if (!string.IsNullOrEmpty(status)) q = q.Where(x => x.Status == status);

        var list = await q.OrderByDescending(x => x.Id).ToListAsync();
        return Ok(list.Select(x => ToFlowDto(x)).ToList());
    }

    /// <summary>工艺路线详情（含工序）</summary>
    [HttpGet("process-flows/{id}")]
    [ProducesResponseType(typeof(ProcessFlowDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> FlowDetail(int id)
    {
        var flow = await db.ProcessFlows
            .Include(x => x.Product)
            .Include(x => x.Creator)
            .Include(x => x.Steps).ThenInclude(s => s.Equipment)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
        if (flow is null) return NotFound(new { message = "工艺路线不存在" });

        return Ok(ToFlowDetailDto(flow));
    }

    /// <summary>创建工艺路线（含工序）</summary>
    [HttpPost("process-flows")]
    [ProducesResponseType(typeof(ProcessFlowDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateFlow([FromBody] CreateProcessFlowRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name) || req.ProductId <= 0)
            return BadRequest(new { message = "请填写工艺名称并选择产品" });
        if (req.Steps.Count == 0)
            return BadRequest(new { message = "至少需要一道工序" });

        if (await db.ProcessFlows.AnyAsync(x => x.Code == req.Code))
            return BadRequest(new { message = $"工艺编号 {req.Code} 已存在" });

        var flow = new ProcessFlow
        {
            Code = string.IsNullOrWhiteSpace(req.Code) ? await NextFlowCodeAsync(req.ProductId) : req.Code.Trim(),
            Name = req.Name.Trim(),
            ProductId = req.ProductId,
            Description = req.Description,
            CreatedById = req.CreatedById,
            Status = "Draft"
        };

        // 同产品已有工艺则版本号 +1
        var maxVersion = await db.ProcessFlows
            .Where(x => x.ProductId == req.ProductId)
            .Select(x => (int?)x.Version)
            .MaxAsync() ?? 0;
        flow.Version = maxVersion + 1;

        foreach (var s in req.Steps.OrderBy(x => x.StepNo))
        {
            flow.Steps.Add(ToStep(s));
        }

        db.ProcessFlows.Add(flow);
        await db.SaveChangesAsync();

        return await FlowDetail(flow.Id);
    }

    /// <summary>更新工艺路线（工序全量替换，仅草稿可改）</summary>
    [HttpPut("process-flows/{id}")]
    [ProducesResponseType(typeof(ProcessFlowDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateFlow(int id, [FromBody] UpdateProcessFlowRequest req)
    {
        var flow = await db.ProcessFlows
            .Include(x => x.Steps)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (flow is null) return NotFound(new { message = "工艺路线不存在" });

        var usedByCards = await db.ProcessCards.AnyAsync(x => x.ProcessFlowId == id);
        if (flow.Status == "Active" && usedByCards)
            return BadRequest(new { message = "已发布的工艺路线已用于流转卡，不能修改；请创建新版工艺" });

        if (string.IsNullOrWhiteSpace(req.Name) || req.ProductId <= 0)
            return BadRequest(new { message = "请填写工艺名称并选择产品" });
        if (req.Steps.Count == 0)
            return BadRequest(new { message = "至少需要一道工序" });

        var codeExists = await db.ProcessFlows.AnyAsync(x => x.Code == req.Code && x.Id != id);
        if (codeExists)
            return BadRequest(new { message = $"工艺编号 {req.Code} 已存在" });

        flow.Code = string.IsNullOrWhiteSpace(req.Code) ? flow.Code : req.Code.Trim();
        flow.Name = req.Name.Trim();
        flow.ProductId = req.ProductId;
        flow.Description = req.Description;

        // 全量替换工序
        db.ProcessSteps.RemoveRange(flow.Steps);
        foreach (var s in req.Steps.OrderBy(x => x.StepNo))
            flow.Steps.Add(ToStep(s));

        await db.SaveChangesAsync();
        return await FlowDetail(flow.Id);
    }

    /// <summary>删除工艺路线（仅草稿）</summary>
    [HttpDelete("process-flows/{id}")]
    public async Task<IActionResult> DeleteFlow(int id)
    {
        var flow = await db.ProcessFlows.FindAsync(id);
        if (flow is null) return NotFound(new { message = "工艺路线不存在" });
        if (flow.Status != "Draft")
            return BadRequest(new { message = "只能删除草稿状态的工艺路线" });

        db.ProcessFlows.Remove(flow);
        await db.SaveChangesAsync();
        return Ok(new { message = "已删除" });
    }

    /// <summary>发布工艺路线</summary>
    [HttpPost("process-flows/{id}/activate")]
    public async Task<IActionResult> ActivateFlow(int id)
    {
        var flow = await db.ProcessFlows.Include(x => x.Steps).FirstOrDefaultAsync(x => x.Id == id);
        if (flow is null) return NotFound(new { message = "工艺路线不存在" });
        if (flow.Steps.Count == 0)
            return BadRequest(new { message = "请先添加工序再发布" });

        if (flow.Status != "Active")
        {
            // 同产品其他已发布工艺自动停用（同一产品同时只保留一版有效工艺）
            var others = await db.ProcessFlows
                .Where(x => x.ProductId == flow.ProductId && x.Status == "Active" && x.Id != id)
                .ToListAsync();
            foreach (var o in others) o.Status = "Inactive";

            flow.Status = "Active";
            await db.SaveChangesAsync();
        }
        return Ok(new { message = "工艺已发布" });
    }

    /// <summary>停用工艺路线</summary>
    [HttpPost("process-flows/{id}/deactivate")]
    public async Task<IActionResult> DeactivateFlow(int id)
    {
        var flow = await db.ProcessFlows.FindAsync(id);
        if (flow is null) return NotFound(new { message = "工艺路线不存在" });
        var usedByCards = await db.ProcessCards.AnyAsync(x => x.ProcessFlowId == id);
        if (usedByCards)
            return BadRequest(new { message = "该工艺已用于流转卡，不能停用" });

        flow.Status = "Inactive";
        await db.SaveChangesAsync();
        return Ok(new { message = "工艺已停用" });
    }

    // ─────────────── 工艺流转卡 ───────────────

    /// <summary>流转卡列表</summary>
    [HttpGet("process-cards")]
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

        var list = await q.OrderByDescending(x => x.Id).ToListAsync();
        return Ok(list.Select(ToCardDto).ToList());
    }

    /// <summary>产品工序进度总览（每个流转卡的每道工序完成量与完工标识）</summary>
    /// <param name="productId">按产品筛选（可选）</param>
    /// <param name="status">按流转卡状态筛选 InProgress/Completed（可选）</param>
    /// <param name="keyword">按流转卡编号或产品名模糊搜索（可选）</param>
    [HttpGet("process-cards/progress")]
    [ProducesResponseType(typeof(List<ProcessCardProgressDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CardProgress([FromQuery] int? productId, [FromQuery] string? status, [FromQuery] string? keyword)
    {
        var q = db.ProcessCards
            .Include(x => x.Product)
            .Include(x => x.ProcessFlow)
            .Include(x => x.CardSteps).ThenInclude(s => s.Operator)
            .Include(x => x.CardSteps).ThenInclude(s => s.Inspector)
            .AsNoTracking().AsQueryable();

        if (productId.HasValue) q = q.Where(x => x.ProductId == productId);
        if (!string.IsNullOrEmpty(status)) q = q.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(keyword))
            q = q.Where(x => x.Code.Contains(keyword) || (x.Product != null && x.Product.Name.Contains(keyword)));

        var cards = await q.OrderByDescending(x => x.Id).Take(200).ToListAsync();
        return Ok(await ProcessCardProgressBuilder.BuildAsync(db, cards));
    }

    /// <summary>流转卡详情（含工序执行记录与报工完成数量）</summary>
    [HttpGet("process-cards/{id}")]
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

        // 查报工累计
        var sums = await db.WorkReports
            .Where(r => r.ProcessCardId == id && r.ProcessStepNo.HasValue)
            .GroupBy(r => r.ProcessStepNo)
            .Select(g => new { StepNo = g.Key, Qty = g.Sum(r => r.Quantity), QtyQ = g.Sum(r => r.QualifiedQty), QtyD = g.Sum(r => r.DefectQty) })
            .ToListAsync();
        var sumMap = sums.ToDictionary(k => k.StepNo ?? 0, v => (v.Qty, v.QtyQ, v.QtyD));

        var dto = ToCardDetailDto(card);
        foreach (var step in dto.CardSteps)
        {
            if (sumMap.TryGetValue(step.StepNo, out var s))
            {
                step.CompletedQty = s.Qty;
                step.QualifiedQty = s.QtyQ;
                step.DefectQty = s.QtyD;
            }
        }
        return Ok(dto);
    }

    /// <summary>创建流转卡（按已发布工艺路线生成）</summary>
    [HttpPost("process-cards")]
    [ProducesResponseType(typeof(ProcessCardDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateCard([FromBody] CreateProcessCardRequest req)
    {
        var flow = await db.ProcessFlows
            .Include(x => x.Steps)
            .FirstOrDefaultAsync(x => x.Id == req.ProcessFlowId);
        if (flow is null) return NotFound(new { message = "工艺路线不存在" });
        if (flow.Status != "Active")
            return BadRequest(new { message = "只能使用已发布的工艺路线创建流转卡" });
        if (flow.Steps.Count == 0)
            return BadRequest(new { message = "工艺路线还没有工序" });
        if (req.Quantity <= 0)
            return BadRequest(new { message = "请填写批次数量" });

        var card = new ProcessCard
        {
            Code = await NextCardCodeAsync(),
            ProcessFlowId = flow.Id,
            ProductId = flow.ProductId,
            Quantity = req.Quantity,
            MaterialSpec = req.MaterialSpec,
            SurfaceTreatment = req.SurfaceTreatment,
            Status = "InProgress",
            CurrentStepNo = 0,
            CreatedById = req.CreatedById,
            CreatedAt = DateTime.UtcNow,
            Remark = req.Remark
        };

        // 复制工序为执行记录，保留纸质卡空白字段，流转时再填写
        foreach (var step in flow.Steps.OrderBy(x => x.StepNo))
        {
            card.CardSteps.Add(new ProcessCardStep
            {
                StepNo = step.StepNo,
                StepName = step.Name,
                Status = "Pending"
            });
        }

        db.ProcessCards.Add(card);
        await db.SaveChangesAsync();

        return await CardDetail(card.Id);
    }

    /// <summary>开始/流转到下一道工序（完成当前工序，进入下一道）</summary>
    [HttpPost("process-cards/{id}/advance")]
    [ProducesResponseType(typeof(ProcessCardDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> AdvanceCard(int id, [FromBody] AdvanceCardRequest? req)
    {
        var card = await db.ProcessCards
            .Include(x => x.CardSteps)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (card is null) return NotFound(new { message = "流转卡不存在" });
        if (card.Status == "Completed")
            return BadRequest(new { message = "流转卡已完成，不能继续流转" });

        var totalSteps = card.CardSteps.Count;
        var next = card.CurrentStepNo + 1;

        if (next > totalSteps)
        {
            await CompleteCardCoreAsync(card, req?.Remark);
            return await CardDetail(card.Id);
        }

        var step = card.CardSteps.FirstOrDefault(x => x.StepNo == next);
        if (step is null) return BadRequest(new { message = "工序数据异常" });

        var now = DateTime.UtcNow;
        // 完成上一道（如果上道还没完成）
        var prev = card.CardSteps.FirstOrDefault(x => x.StepNo == card.CurrentStepNo);
        if (prev is { Status: "Running" })
        {
            prev.Status = "Completed";
            prev.CompletedAt = now;
        }
        // 开始当前工序，并填充纸质卡字段
        step.Status = step.Status == "Pending" ? "Running" : step.Status;
        step.StartedAt ??= now;
        if (req?.OperatorId.HasValue == true) step.OperatorId = req.OperatorId;
        if (!string.IsNullOrWhiteSpace(req?.Remark)) step.Remark = req.Remark;
        if (!string.IsNullOrWhiteSpace(req?.MachineNo)) step.MachineNo = req.MachineNo;
        if (!string.IsNullOrWhiteSpace(req?.Shift)) step.Shift = req.Shift;
        if (req?.Quantity.HasValue == true) step.Quantity = req.Quantity;
        step.WorkDate ??= DateTime.TryParse(req?.WorkDate, out var wd) ? wd.Date : now.Date;
        card.CurrentStepNo = next;
        card.StartedAt ??= now;

        if (next >= totalSteps && card.CardSteps.All(x => x.StepNo <= next))
            await CompleteCardCoreAsync(card, req?.Remark);

        await db.SaveChangesAsync();
        return await CardDetail(card.Id);
    }

    /// <summary>直接完成整张流转卡</summary>
    [HttpPost("process-cards/{id}/complete")]
    [ProducesResponseType(typeof(ProcessCardDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CompleteCard(int id, [FromBody] AdvanceCardRequest? req)
    {
        var card = await db.ProcessCards.Include(x => x.CardSteps).FirstOrDefaultAsync(x => x.Id == id);
        if (card is null) return NotFound(new { message = "流转卡不存在" });
        if (card.Status == "Completed")
            return BadRequest(new { message = "流转卡已完成" });

        await CompleteCardCoreAsync(card, req?.Remark);
        await db.SaveChangesAsync();
        return await CardDetail(card.Id);
    }

    /// <summary>回退上一道工序（处理中卡片可回退）</summary>
    [HttpPost("process-cards/{id}/rollback")]
    [ProducesResponseType(typeof(ProcessCardDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> RollbackCard(int id)
    {
        var card = await db.ProcessCards.Include(x => x.CardSteps).FirstOrDefaultAsync(x => x.Id == id);
        if (card is null) return NotFound(new { message = "流转卡不存在" });
        if (card.Status == "Completed")
            return BadRequest(new { message = "流转卡已完成，不能回退" });
        if (card.CurrentStepNo <= 1)
            return BadRequest(new { message = "已是第一道工序，不能回退" });

        var current = card.CardSteps.FirstOrDefault(x => x.StepNo == card.CurrentStepNo);
        if (current is { Status: "Running" })
        {
            current.Status = "Pending";
            current.StartedAt = null;
            current.CompletedAt = null;
            current.OperatorId = null;
        }
        card.CurrentStepNo -= 1;
        await db.SaveChangesAsync();
        return await CardDetail(card.Id);
    }

    /// <summary>删除流转卡</summary>
    [HttpDelete("process-cards/{id}")]
    public async Task<IActionResult> DeleteCard(int id)
    {
        var card = await db.ProcessCards.FindAsync(id);
        if (card is null) return NotFound(new { message = "流转卡不存在" });
        db.ProcessCards.Remove(card);
        await db.SaveChangesAsync();
        return Ok(new { message = "已删除" });
    }

    // ─────────────── 内部方法 ───────────────

    private static ProcessStep ToStep(CreateProcessStepRequest s) => new()
    {
        StepNo = s.StepNo,
        Name = s.Name.Trim(),
        Description = s.Description,
        EquipmentId = s.EquipmentId,
        DurationMinutes = s.DurationMinutes,
        RequiresInspection = s.RequiresInspection
    };

    private static ProcessFlowDto ToFlowDto(ProcessFlow x) => new()
    {
        Id = x.Id,
        Code = x.Code,
        Name = x.Name,
        ProductId = x.ProductId,
        ProductName = x.Product?.Name,
        ProductSpec = x.Product?.Specification,
        Version = x.Version,
        Status = x.Status,
        Description = x.Description,
        CreatedById = x.CreatedById,
        CreatorName = x.Creator?.Name,
        CreatedAt = x.CreatedAt,
        StepCount = x.Steps.Count
    };

    private static ProcessFlowDetailDto ToFlowDetailDto(ProcessFlow x) => new()
    {
        Id = x.Id,
        Code = x.Code,
        Name = x.Name,
        ProductId = x.ProductId,
        ProductName = x.Product?.Name,
        ProductSpec = x.Product?.Specification,
        Version = x.Version,
        Status = x.Status,
        Description = x.Description,
        CreatedById = x.CreatedById,
        CreatorName = x.Creator?.Name,
        CreatedAt = x.CreatedAt,
        StepCount = x.Steps.Count,
        Steps = x.Steps.OrderBy(s => s.StepNo).Select(s => new ProcessStepDto
        {
            Id = s.Id,
            ProcessFlowId = s.ProcessFlowId,
            StepNo = s.StepNo,
            Name = s.Name,
            Description = s.Description,
            EquipmentId = s.EquipmentId,
            EquipmentName = s.Equipment?.Name,
            DurationMinutes = s.DurationMinutes,
            RequiresInspection = s.RequiresInspection
        }).ToList()
    };

    private static ProcessCardDto ToCardDto(ProcessCard x) => new()
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
    };

    private static ProcessCardDetailDto ToCardDetailDto(ProcessCard x) => new()
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
        Remark = x.Remark,
        CardSteps = x.CardSteps.OrderBy(s => s.StepNo).Select(s => new ProcessCardStepDto
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
            Remark = s.Remark,
            MachineNo = s.MachineNo,
            WorkDate = s.WorkDate?.ToString("yyyy-MM-dd"),
            Shift = s.Shift,
            Quantity = s.Quantity,
            InspectorId = s.InspectorId,
            InspectorName = s.Inspector?.Name
        }).ToList()
    };

    private async Task CompleteCardCoreAsync(ProcessCard card, string? remark)
    {
        var now = DateTime.UtcNow;
        card.Status = "Completed";
        card.CompletedAt = now;
        card.CurrentStepNo = card.CardSteps.Count;
        if (!string.IsNullOrWhiteSpace(remark)) card.Remark = remark;

        foreach (var step in card.CardSteps.Where(x => x.Status != "Completed"))
        {
            step.Status = "Completed";
            step.CompletedAt ??= now;
            step.WorkDate ??= now.Date;
            step.Quantity ??= card.Quantity;
        }
        await db.SaveChangesAsync();
    }

    /// <summary>修改流转卡某道工序的纸质卡字段</summary>
    [HttpPut("process-cards/{id}/steps/{stepNo}")]
    public async Task<IActionResult> UpdateCardStep(int id, int stepNo, [FromBody] UpdateCardStepRequest req)
    {
        var card = await db.ProcessCards.Include(x => x.CardSteps).FirstOrDefaultAsync(x => x.Id == id);
        if (card is null) return NotFound(new { message = "流转卡不存在" });
        var step = card.CardSteps.FirstOrDefault(x => x.StepNo == stepNo);
        if (step is null) return NotFound(new { message = "工序不存在" });

        if (req.MachineNo is not null) step.MachineNo = req.MachineNo;
        if (req.WorkDate is not null && DateTime.TryParse(req.WorkDate, out var wd)) step.WorkDate = wd.Date;
        if (req.Shift is not null) step.Shift = req.Shift;
        if (req.Quantity.HasValue) step.Quantity = req.Quantity;
        if (req.OperatorId.HasValue) step.OperatorId = req.OperatorId;
        if (req.InspectorId.HasValue) step.InspectorId = req.InspectorId;
        if (req.Remark is not null) step.Remark = req.Remark;

        await db.SaveChangesAsync();
        return Ok(new { message = "已更新" });
    }

    private async Task<string> NextFlowCodeAsync(int productId)
    {
        var product = await db.Products.FindAsync(productId);
        var prefix = $"GY-{product?.Code ?? "P"}";
        var count = await db.ProcessFlows.CountAsync(x => x.Code.StartsWith(prefix + "-")) + 1;
        return $"{prefix}-{count:00}";
    }

    private async Task<string> NextCardCodeAsync()
    {
        var today = DateTime.UtcNow;
        var prefix = $"LZ-{today:yyyyMMdd}-";
        var count = await db.ProcessCards.CountAsync(x => x.Code.StartsWith(prefix)) + 1;
        return $"{prefix}{count:000}";
    }
}
