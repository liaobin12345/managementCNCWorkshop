namespace ManagementCNCWorkshop.Api.Models;

/// <summary>编程助手 - 订阅</summary>
public class CncSubscription
{
    public int Id { get; set; }

    /// <summary>订阅者（员工/编程员）ID</summary>
    public int EmployeeId { get; set; }

    /// <summary>套餐编码：personal / pro / enterprise</summary>
    public string? PlanCode { get; set; }

    /// <summary>状态：active / expired / none</summary>
    public string Status { get; set; } = "none";

    /// <summary>生效日期</summary>
    public DateTime? StartDate { get; set; }

    /// <summary>到期日期</summary>
    public DateTime? ExpireDate { get; set; }

    /// <summary>识图额度（次/年）</summary>
    public int VisionQuota { get; set; }

    /// <summary>已用识图次数</summary>
    public int VisionUsed { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Employee? Employee { get; set; }
}