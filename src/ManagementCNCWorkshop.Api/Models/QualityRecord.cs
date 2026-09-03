namespace ManagementCNCWorkshop.Api.Models;

/// <summary>质量检验记录，质量统计的数据来源</summary>
public class QualityRecord : ITenantScoped
{
    /// <summary>主键 ID</summary>
    public int Id { get; set; }

    /// <summary>产品 ID</summary>
    public int ProductId { get; set; }

    /// <summary>车间 ID</summary>
    public int WorkshopId { get; set; }

    /// <summary>质检员员工 ID</summary>
    public int InspectorId { get; set; }

    /// <summary>检验时间</summary>
    public DateTime RecordDate { get; set; } = DateTime.UtcNow;

    /// <summary>抽检/检验总数</summary>
    public decimal SampleQty { get; set; }

    /// <summary>合格数量</summary>
    public decimal QualifiedQty { get; set; }

    /// <summary>不良数量</summary>
    public decimal DefectQty { get; set; }

    /// <summary>不良类型描述，如 尺寸超差、表面划伤</summary>
    public string? DefectType { get; set; }

    /// <summary>关联工艺流转卡（可选，历史记录允许为空）</summary>
    public int? ProcessCardId { get; set; }

    /// <summary>关联工序序号</summary>
    public int? ProcessStepNo { get; set; }

    /// <summary>工序名称快照</summary>
    public string? ProcessStepName { get; set; }

    /// <summary>关联设备（可选）</summary>
    public int? EquipmentId { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    /// <summary>关联产品</summary>
    public Product? Product { get; set; }

    /// <summary>关联车间</summary>
    public Workshop? Workshop { get; set; }

    /// <summary>关联质检员</summary>
    public Employee? Inspector { get; set; }

    /// <summary>关联工艺流转卡</summary>
    public ProcessCard? ProcessCard { get; set; }

    /// <summary>关联设备</summary>
    public Equipment? Equipment { get; set; }
}
