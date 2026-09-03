namespace ManagementCNCWorkshop.Api.Models;

/// <summary>扫码报工记录，产量统计的数据来源</summary>
public class WorkReport : ITenantScoped
{
    /// <summary>主键 ID</summary>
    public int Id { get; set; }

    /// <summary>产品 ID</summary>
    public int ProductId { get; set; }

    /// <summary>车间 ID</summary>
    public int WorkshopId { get; set; }

    /// <summary>报工员工 ID</summary>
    public int EmployeeId { get; set; }

    /// <summary>使用的设备 ID（可选）</summary>
    public int? EquipmentId { get; set; }

    /// <summary>报工时间</summary>
    public DateTime ReportDate { get; set; } = DateTime.UtcNow;

    /// <summary>报工总产量</summary>
    public decimal Quantity { get; set; }

    /// <summary>合格数量</summary>
    public decimal QualifiedQty { get; set; }

    /// <summary>不良/报废数量</summary>
    public decimal DefectQty { get; set; }

    /// <summary>扫码原始内容</summary>
    public string? ScanPayload { get; set; }

    /// <summary>关联的工艺流转卡 ID（可选，报工归属到具体批次）</summary>
    public int? ProcessCardId { get; set; }

    /// <summary>关联的工序序号（属于流转卡的第几道工序）</summary>
    public int? ProcessStepNo { get; set; }

    /// <summary>工序名称冗余（便于统计与展示）</summary>
    public string? ProcessStepName { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    /// <summary>关联产品</summary>
    public Product? Product { get; set; }

    /// <summary>关联车间</summary>
    public Workshop? Workshop { get; set; }

    /// <summary>关联员工</summary>
    public Employee? Employee { get; set; }

    /// <summary>关联设备</summary>
    public Equipment? Equipment { get; set; }
}
