namespace ManagementCNCWorkshop.Api.Models;

/// <summary>产品/零件</summary>
public class Product : ITenantScoped
{
    /// <summary>主键 ID</summary>
    public int Id { get; set; }

    /// <summary>所属车间 ID</summary>
    public int WorkshopId { get; set; }

    /// <summary>产品编码，唯一</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>产品名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>规格型号，如 Φ50×80</summary>
    public string? Specification { get; set; }

    /// <summary>二维码内容，扫码报工时匹配，如 PROD:P001</summary>
    public string? QrCode { get; set; }

    /// <summary>现场照片 URL（上传后返回的相对路径，如 /uploads/202608/xxx.jpg）</summary>
    public string? ImageUrl { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
