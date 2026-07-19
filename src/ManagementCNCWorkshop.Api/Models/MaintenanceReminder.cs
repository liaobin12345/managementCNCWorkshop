namespace ManagementCNCWorkshop.Api.Models;

/// <summary>设备保养提醒</summary>
public class MaintenanceReminder
{
    /// <summary>主键 ID</summary>
    public int Id { get; set; }

    /// <summary>关联保养计划 ID</summary>
    public int PlanId { get; set; }

    /// <summary>关联设备 ID</summary>
    public int EquipmentId { get; set; }

    /// <summary>应保养截止日期</summary>
    public DateTime DueDate { get; set; }

    /// <summary>开始提醒日期（DueDate 往前推 RemindDaysBefore 天）</summary>
    public DateTime RemindDate { get; set; }

    /// <summary>状态：Pending 待处理 / Overdue 已逾期 / Completed 已完成</summary>
    public string Status { get; set; } = "Pending";

    /// <summary>关联保养计划</summary>
    public MaintenancePlan? Plan { get; set; }

    /// <summary>关联设备</summary>
    public Equipment? Equipment { get; set; }
}
