namespace ManagementCNCWorkshop.Api.Models;

/// <summary>工艺流转卡：按工艺路线生成的批次加工流转凭证，贯穿整个加工环节</summary>
public class ProcessCard
{
    /// <summary>主键 ID</summary>
    public int Id { get; set; }

    /// <summary>流转卡编号，自动生成，如 LZ-20260825-001</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>采用的工艺路线 ID</summary>
    public int ProcessFlowId { get; set; }

    /// <summary>产品 ID</summary>
    public int ProductId { get; set; }

    /// <summary>批次数量</summary>
    public decimal Quantity { get; set; }

    /// <summary>材料规格（流转卡卡头字段，如 45#钢 Φ60）</summary>
    public string? MaterialSpec { get; set; }

    /// <summary>表面处理工艺（流转卡卡头字段，如 发黑/镀锌）</summary>
    public string? SurfaceTreatment { get; set; }

    /// <summary>状态：InProgress 加工中 / Completed 已完成</summary>
    public string Status { get; set; } = "InProgress";

    /// <summary>当前工序序号（0=未开始，每流转一步+1，等于最大步数时完成）</summary>
    public int CurrentStepNo { get; set; }

    /// <summary>创建人（员工 ID）</summary>
    public int? CreatedById { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>开始时间</summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>完成时间</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    /// <summary>关联工艺路线</summary>
    public ProcessFlow? ProcessFlow { get; set; }

    /// <summary>关联产品</summary>
    public Product? Product { get; set; }

    /// <summary>创建人</summary>
    public Employee? Creator { get; set; }

    /// <summary>工序执行记录</summary>
    public List<ProcessCardStep> CardSteps { get; set; } = new();
}

/// <summary>流转卡工序执行记录：每道工序的实际执行状态</summary>
public class ProcessCardStep
{
    /// <summary>主键 ID</summary>
    public int Id { get; set; }

    /// <summary>流转卡 ID</summary>
    public int ProcessCardId { get; set; }

    /// <summary>工序序号</summary>
    public int StepNo { get; set; }

    /// <summary>工序名称（冗余，方便展示）</summary>
    public string StepName { get; set; } = string.Empty;

    /// <summary>状态：Pending 待加工 / Running 加工中 / Completed 已完成</summary>
    public string Status { get; set; } = "Pending";

    /// <summary>机台号（纸质流转卡字段，如 C01）</summary>
    public string? MachineNo { get; set; }

    /// <summary>加工日期（纸质流转卡字段）</summary>
    public DateTime? WorkDate { get; set; }

    /// <summary>班次：白班 / 夜班</summary>
    public string? Shift { get; set; }

    /// <summary>本工序加工数量（纸质流转卡字段）</summary>
    public decimal? Quantity { get; set; }

    /// <summary>开始时间</summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>完成时间</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>操作员工 ID</summary>
    public int? OperatorId { get; set; }

    /// <summary>检查员工 ID</summary>
    public int? InspectorId { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    /// <summary>关联流转卡</summary>
    public ProcessCard? ProcessCard { get; set; }

    /// <summary>操作员</summary>
    public Employee? Operator { get; set; }

    /// <summary>检查员</summary>
    public Employee? Inspector { get; set; }
}