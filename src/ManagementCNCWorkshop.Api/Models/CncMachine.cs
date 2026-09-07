namespace ManagementCNCWorkshop.Api.Models;

/// <summary>编程助手 - 数控机床配置</summary>
public class CncMachine : ITenantScoped
{
    public int Id { get; set; }

    /// <summary>所属车间 ID</summary>
    public int WorkshopId { get; set; }

    /// <summary>机床编码，车间内唯一，如 LATHE-01</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>机床名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>数控系统，如 Fanuc 0i-TF / GSK 980TD / LNC 520</summary>
    public string ControlSystem { get; set; } = string.Empty;

    /// <summary>是否启用</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>网络地址（DNC 传输用，可为空）</summary>
    public string? NetworkAddress { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Workshop? Workshop { get; set; }
}