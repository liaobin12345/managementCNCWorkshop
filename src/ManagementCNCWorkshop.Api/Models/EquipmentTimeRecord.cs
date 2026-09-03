namespace ManagementCNCWorkshop.Api.Models;

/// <summary>设备每日时间记录：调试时间 / 正常开机时间 / 待机时间（编程技术员/操作工按日录入）</summary>
public class EquipmentTimeRecord : ITenantScoped
{
    /// <summary>主键 ID</summary>
    public int Id { get; set; }

    /// <summary>设备 ID</summary>
    public int EquipmentId { get; set; }

    /// <summary>所属车间 ID</summary>
    public int WorkshopId { get; set; }

    /// <summary>记录日期（某一天的用时，单位：小时）</summary>
    public DateTime RecordDate { get; set; }

    /// <summary>调试时间（小时）：编程、调机、首件调试等</summary>
    public decimal SetupHours { get; set; }

    /// <summary>正常开机时间（小时）：机器正常切削运行</summary>
    public decimal RunningHours { get; set; }

    /// <summary>待机时间（小时）：开机但未生产（等待、停机等）</summary>
    public decimal IdleHours { get; set; }

    /// <summary>记录人（员工 ID，可选）</summary>
    public int? EmployeeId { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>关联设备</summary>
    public Equipment? Equipment { get; set; }

    /// <summary>关联员工</summary>
    public Employee? Employee { get; set; }
}
