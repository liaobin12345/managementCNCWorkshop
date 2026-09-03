namespace ManagementCNCWorkshop.Api.Models;

/// <summary>工艺路线：工程师为产品设计的加工工艺流程</summary>
public class ProcessFlow : ITenantScoped
{
    /// <summary>主键 ID</summary>
    public int Id { get; set; }

    /// <summary>工艺编号，唯一，如 GY-P001-V1</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>工艺名称，如 传动轴加工工艺</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>对应产品 ID</summary>
    public int ProductId { get; set; }

    /// <summary>所属车间 ID</summary>
    public int WorkshopId { get; set; }

    /// <summary>版本号（同一产品可有多版工艺，默认 1）</summary>
    public int Version { get; set; } = 1;

    /// <summary>状态：Draft 草稿 / Active 已发布 / Inactive 停用</summary>
    public string Status { get; set; } = "Draft";

    /// <summary>工艺说明</summary>
    public string? Description { get; set; }

    /// <summary>设计工程师（员工 ID）</summary>
    public int? CreatedById { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>关联产品</summary>
    public Product? Product { get; set; }

    /// <summary>设计工程师</summary>
    public Employee? Creator { get; set; }

    /// <summary>工序列表</summary>
    public List<ProcessStep> Steps { get; set; } = new();
}

/// <summary>工序：工艺路线中的一道加工步骤</summary>
public class ProcessStep : ITenantScoped
{
    /// <summary>主键 ID</summary>
    public int Id { get; set; }

    /// <summary>所属工艺路线 ID</summary>
    public int ProcessFlowId { get; set; }

    /// <summary>所属车间 ID</summary>
    public int WorkshopId { get; set; }

    /// <summary>工序序号（从 1 开始，加工顺序）</summary>
    public int StepNo { get; set; }

    /// <summary>工序名称，如 下料 / 车削 / 铣削 / 钻孔 / 质检 / 入库</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>工艺内容/加工要求</summary>
    public string? Description { get; set; }

    /// <summary>建议使用设备 ID（可选）</summary>
    public int? EquipmentId { get; set; }

    /// <summary>标准工时（分钟）</summary>
    public int? DurationMinutes { get; set; }

    /// <summary>该工序完成后是否需要质检</summary>
    public bool RequiresInspection { get; set; }

    /// <summary>所属工艺路线</summary>
    public ProcessFlow? ProcessFlow { get; set; }

    /// <summary>建议设备</summary>
    public Equipment? Equipment { get; set; }
}
