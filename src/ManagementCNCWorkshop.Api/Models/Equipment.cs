namespace ManagementCNCWorkshop.Api.Models;

/// <summary>CNC 设备</summary>
public class Equipment : ITenantScoped
{
    /// <summary>主键 ID</summary>
    public int Id { get; set; }

    /// <summary>设备编码，唯一，如 CNC-01</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>设备名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>所属车间 ID</summary>
    public int WorkshopId { get; set; }

    /// <summary>运行状态：Idle 空闲 / Running 运行中 / Maintenance 保养中 / Fault 故障</summary>
    public string Status { get; set; } = "Idle";

    /// <summary>上次保养日期</summary>
    public DateTime? LastMaintenanceDate { get; set; }

    /// <summary>设备二维码内容，如 EQ:CNC-01</summary>
    public string? QrCode { get; set; }

    /// <summary>现场照片 URL（上传后返回的相对路径，如 /uploads/202608/xxx.jpg）</summary>
    public string? ImageUrl { get; set; }

    /// <summary>关联车间</summary>
    public Workshop? Workshop { get; set; }
}
