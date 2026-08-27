namespace ManagementCNCWorkshop.Api.Models.Dtos;

// ─── 工艺路线 DTOs ───

/// <summary>工艺路线列表项</summary>
public class ProcessFlowDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? ProductSpec { get; set; }
    public int Version { get; set; }
    public string Status { get; set; } = "Draft";
    public string? Description { get; set; }
    public int? CreatedById { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreatedAt { get; set; }
    public int StepCount { get; set; }
}

/// <summary>工艺路线详情（含工序列表）</summary>
public class ProcessFlowDetailDto : ProcessFlowDto
{
    public List<ProcessStepDto> Steps { get; set; } = new();
}

/// <summary>工序</summary>
public class ProcessStepDto
{
    public int Id { get; set; }
    public int ProcessFlowId { get; set; }
    public int StepNo { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? EquipmentId { get; set; }
    public string? EquipmentName { get; set; }
    public int? DurationMinutes { get; set; }
    public bool RequiresInspection { get; set; }
}

/// <summary>创建工艺路线请求</summary>
public class CreateProcessFlowRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public string? Description { get; set; }
    public int? CreatedById { get; set; }
    public List<CreateProcessStepRequest> Steps { get; set; } = new();
}

/// <summary>创建工序请求</summary>
public class CreateProcessStepRequest
{
    public int StepNo { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? EquipmentId { get; set; }
    public int? DurationMinutes { get; set; }
    public bool RequiresInspection { get; set; }
}

/// <summary>更新工艺路线请求（含工序全量替换）</summary>
public class UpdateProcessFlowRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public string? Description { get; set; }
    public List<CreateProcessStepRequest> Steps { get; set; } = new();
}

// ─── 工艺流转卡 DTOs ───

/// <summary>流转卡列表项</summary>
public class ProcessCardDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public int ProcessFlowId { get; set; }
    public string? FlowName { get; set; }
    public int ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? ProductSpec { get; set; }
    public decimal Quantity { get; set; }
    public string? MaterialSpec { get; set; }
    public string? SurfaceTreatment { get; set; }
    public string Status { get; set; } = "InProgress";
    public int CurrentStepNo { get; set; }
    public int? CreatedById { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Remark { get; set; }

    /// <summary>是否已完工（最后一道工序走完）</summary>
    public bool Finished => Status == "Completed";

    /// <summary>状态文字：加工中 / 已完工</summary>
    public string StatusText => Status == "Completed" ? "已完工" : "加工中";
}

/// <summary>流转卡详情（含工序执行记录）</summary>
public class ProcessCardDetailDto : ProcessCardDto
{
    public List<ProcessCardStepDto> CardSteps { get; set; } = new();
}

/// <summary>流转卡工序执行记录</summary>
public class ProcessCardStepDto
{
    public int Id { get; set; }
    public int ProcessCardId { get; set; }
    public int StepNo { get; set; }
    public string StepName { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? OperatorId { get; set; }
    public string? OperatorName { get; set; }
    public string? Remark { get; set; }
    public string? MachineNo { get; set; }
    public string? WorkDate { get; set; }
    public string? Shift { get; set; }
    public decimal? Quantity { get; set; }
    public int? InspectorId { get; set; }
    public string? InspectorName { get; set; }

    /// <summary>来自报工的累计完成数量（工序执行记录上统计）</summary>
    public decimal CompletedQty { get; set; }

    /// <summary>累计合格数量</summary>
    public decimal QualifiedQty { get; set; }

    /// <summary>累计不良数量</summary>
    public decimal DefectQty { get; set; }

    /// <summary>状态文字：未开始 / 进行中 / 已完成</summary>
    public string StatusText =>
        Status == "Completed" ? "已完成" :
        Status == "Running" ? "进行中" : "未开始";

    /// <summary>该工序是否已完成</summary>
    public bool Finished => Status == "Completed";
}

/// <summary>创建流转卡请求</summary>
public class CreateProcessCardRequest
{
    public int ProcessFlowId { get; set; }
    public decimal Quantity { get; set; }
    public int? CreatedById { get; set; }
    public string? Remark { get; set; }
    public string? MaterialSpec { get; set; }
    public string? SurfaceTreatment { get; set; }
}

/// <summary>流转操作请求（前进/完成工序）</summary>
public class AdvanceCardRequest
{
    public int? OperatorId { get; set; }
    public string? Remark { get; set; }
    public string? MachineNo { get; set; }
    public string? Shift { get; set; }
    public decimal? Quantity { get; set; }
    public string? WorkDate { get; set; }
}

/// <summary>更新流转卡某道工序的纸质卡字段请求</summary>
public class UpdateCardStepRequest
{
    public string? MachineNo { get; set; }
    public string? WorkDate { get; set; }
    public string? Shift { get; set; }
    public decimal? Quantity { get; set; }
    public int? OperatorId { get; set; }
    public int? InspectorId { get; set; }
    public string? Remark { get; set; }
}

/// <summary>工序进度（某张流转卡中一道工序的完成情况）</summary>
public class StepProgressDto
{
    public int StepNo { get; set; }
    public string StepName { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string StatusText { get; set; } = "未开始";
    public bool Finished { get; set; }
    public decimal CompletedQty { get; set; }
    public decimal QualifiedQty { get; set; }
    public decimal DefectQty { get; set; }
    public string? MachineNo { get; set; }
    public string? WorkDate { get; set; }
    public string? Shift { get; set; }
    public string? OperatorName { get; set; }
    public string? InspectorName { get; set; }
    public string? Remark { get; set; }
}

/// <summary>工艺流转卡进度（产品/批次的工序完成总览）</summary>
public class ProcessCardProgressDto
{
    public int CardId { get; set; }
    public string CardCode { get; set; } = string.Empty;
    public int ProcessFlowId { get; set; }
    public string? FlowName { get; set; }
    public int ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? ProductSpec { get; set; }
    public decimal Quantity { get; set; }
    public string? MaterialSpec { get; set; }
    public string? SurfaceTreatment { get; set; }
    public string Status { get; set; } = "InProgress";
    public bool Finished { get; set; }
    public string StatusText { get; set; } = "加工中";
    public int CurrentStepNo { get; set; }
    public int TotalSteps { get; set; }
    public int CompletedSteps { get; set; }
    public decimal TotalCompletedQty { get; set; }
    public decimal TotalQualifiedQty { get; set; }
    public decimal TotalDefectQty { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Remark { get; set; }
    public List<StepProgressDto> Steps { get; set; } = new();
}