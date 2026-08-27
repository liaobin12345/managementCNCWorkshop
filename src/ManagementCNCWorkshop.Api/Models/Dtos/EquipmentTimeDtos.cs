namespace ManagementCNCWorkshop.Api.Models.Dtos;

/// <summary>设备时间记录请求体（小程序/后台录入）</summary>
public class EquipmentTimeRequest
{
    /// <summary>设备 ID</summary>
    public int EquipmentId { get; set; }

    /// <summary>记录日期（默认当天）</summary>
    public DateTime? RecordDate { get; set; }

    /// <summary>调试时间（小时）</summary>
    public decimal SetupHours { get; set; }

    /// <summary>正常开机时间（小时）</summary>
    public decimal RunningHours { get; set; }

    /// <summary>待机时间（小时）</summary>
    public decimal IdleHours { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }
}

/// <summary>设备时间记录（返回给前端）</summary>
public class EquipmentTimeRecordDto
{
    public int Id { get; set; }

    public int EquipmentId { get; set; }

    public string? EquipmentCode { get; set; }

    public string? EquipmentName { get; set; }

    /// <summary>记录日期（yyyy-MM-dd）</summary>
    public string RecordDate { get; set; } = string.Empty;

    public decimal SetupHours { get; set; }

    public decimal RunningHours { get; set; }

    public decimal IdleHours { get; set; }

    public int? EmployeeId { get; set; }

    public string? EmployeeName { get; set; }

    public string? Remark { get; set; }
}

/// <summary>设备时间汇总（看板/统计用）</summary>
public class EquipmentTimeSummaryDto
{
    public int EquipmentId { get; set; }

    public string? EquipmentCode { get; set; }

    public string? EquipmentName { get; set; }

    public decimal SetupHours { get; set; }

    public decimal RunningHours { get; set; }

    public decimal IdleHours { get; set; }

    /// <summary>总时长（调试+开机+待机）</summary>
    public decimal TotalHours => SetupHours + RunningHours + IdleHours;

    /// <summary>开机率（0~1）= 开机时间 / 总时长</summary>
    public decimal Availability => TotalHours > 0 ? RunningHours / TotalHours : 0m;

    /// <summary>区间内总产量</summary>
    public decimal TotalQty { get; set; }

    /// <summary>区间内合格数量</summary>
    public decimal QualifiedQty { get; set; }

    /// <summary>区间内不良数量</summary>
    public decimal DefectQty { get; set; }

    /// <summary>良品率（0~1）</summary>
    public decimal PassRate { get; set; } = 1m;

    /// <summary>OEE 估算 = 开机率 × 良品率</summary>
    public decimal Oee => Availability * PassRate;
}

/// <summary>工人端设备产量统计（按设备分组 + 当日时间记录）</summary>
public class WorkerEquipmentStatDto
{
    public int EquipmentId { get; set; }

    public string? EquipmentCode { get; set; }

    public string? EquipmentName { get; set; }

    /// <summary>总产量</summary>
    public decimal TotalQty { get; set; }

    /// <summary>合格数量</summary>
    public decimal QualifiedQty { get; set; }

    /// <summary>不良数量</summary>
    public decimal DefectQty { get; set; }

    /// <summary>合格率（0~1）</summary>
    public decimal PassRate { get; set; } = 1m;

    /// <summary>今日调试时间（小时）</summary>
    public decimal SetupHours { get; set; }

    /// <summary>今日正常开机时间（小时）</summary>
    public decimal RunningHours { get; set; }

    /// <summary>今日待机时间（小时）</summary>
    public decimal IdleHours { get; set; }
}
