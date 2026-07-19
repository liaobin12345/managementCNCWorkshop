namespace ManagementCNCWorkshop.Api.Models;

/// <summary>设备保养计划</summary>
public class MaintenancePlan
{
    /// <summary>主键 ID</summary>
    public int Id { get; set; }

    /// <summary>关联设备 ID</summary>
    public int EquipmentId { get; set; }

    /// <summary>计划名称，如 月度润滑保养</summary>
    public string PlanName { get; set; } = string.Empty;

    /// <summary>保养周期（天），如 30 表示每 30 天保养一次</summary>
    public int CycleDays { get; set; }

    /// <summary>下次应保养日期</summary>
    public DateTime NextDueDate { get; set; }

    /// <summary>提前几天开始提醒，默认 3 天</summary>
    public int RemindDaysBefore { get; set; } = 3;

    /// <summary>保养内容说明</summary>
    public string? Content { get; set; }

    /// <summary>关联设备</summary>
    public Equipment? Equipment { get; set; }
}
