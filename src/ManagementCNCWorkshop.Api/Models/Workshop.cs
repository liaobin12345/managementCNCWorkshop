namespace ManagementCNCWorkshop.Api.Models;

/// <summary>车间</summary>
public class Workshop
{
    /// <summary>主键 ID</summary>
    public int Id { get; set; }

    /// <summary>车间编码，唯一，如 WS01</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>车间名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
