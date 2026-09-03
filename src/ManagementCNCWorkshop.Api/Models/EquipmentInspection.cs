namespace ManagementCNCWorkshop.Api.Models;

/// <summary>设备点检记录（一台设备·一天·一个班次一条）</summary>
public class EquipmentInspection : ITenantScoped
{
    /// <summary>主键 ID</summary>
    public int Id { get; set; }

    /// <summary>设备 ID</summary>
    public int EquipmentId { get; set; }

    /// <summary>所属车间 ID</summary>
    public int WorkshopId { get; set; }

    /// <summary>点检日期（取日期部分）</summary>
    public DateTime InspectDate { get; set; }

    /// <summary>班次：Day 白班 / Night 夜班</summary>
    public string Shift { get; set; } = "Day";

    /// <summary>点检人（编程技术员）</summary>
    public int? InspectorId { get; set; }

    /// <summary>异常记录（有异常项时填写具体情况）</summary>
    public string? AbnormalNote { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>最后修改时间</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>关联设备</summary>
    public Equipment? Equipment { get; set; }

    /// <summary>关联点检人</summary>
    public Employee? Inspector { get; set; }

    /// <summary>点检明细项（对应纸质表 11 项）</summary>
    public List<EquipmentInspectionItem> Items { get; set; } = new();
}
