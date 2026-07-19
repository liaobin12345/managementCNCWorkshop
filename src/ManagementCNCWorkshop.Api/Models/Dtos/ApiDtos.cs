namespace ManagementCNCWorkshop.Api.Models.Dtos;

/// <summary>扫码报工请求体（小程序提交）</summary>
public class ScanWorkReportRequest
{
    /// <summary>产品 ID（可先调 /api/master/products/by-qrcode 扫码获取）</summary>
    /// <example>1</example>
    public int ProductId { get; set; }

    /// <summary>车间 ID</summary>
    /// <example>1</example>
    public int WorkshopId { get; set; }

    /// <summary>报工员工 ID</summary>
    /// <example>1</example>
    public int EmployeeId { get; set; }

    /// <summary>使用的设备 ID（可选）</summary>
    /// <example>1</example>
    public int? EquipmentId { get; set; }

    /// <summary>报工时间，不传则默认当前时间</summary>
    public DateTime? ReportDate { get; set; }

    /// <summary>报工数量（总产量）</summary>
    /// <example>100</example>
    public decimal Quantity { get; set; }

    /// <summary>合格数量</summary>
    /// <example>98</example>
    public decimal QualifiedQty { get; set; }

    /// <summary>不良/报废数量</summary>
    /// <example>2</example>
    public decimal DefectQty { get; set; }

    /// <summary>扫码原始内容（便于追溯，如 PROD:P001）</summary>
    /// <example>PROD:P001</example>
    public string? ScanPayload { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }
}

/// <summary>产量统计结果</summary>
public class ProductionStatDto
{
    /// <summary>车间 ID</summary>
    public int WorkshopId { get; set; }

    /// <summary>产品 ID</summary>
    public int ProductId { get; set; }

    /// <summary>总产量</summary>
    public decimal TotalQty { get; set; }

    /// <summary>合格数量</summary>
    public decimal QualifiedQty { get; set; }

    /// <summary>不良数量</summary>
    public decimal DefectQty { get; set; }
}

/// <summary>质量统计结果</summary>
public class QualityStatDto
{
    /// <summary>车间 ID</summary>
    public int WorkshopId { get; set; }

    /// <summary>产品 ID</summary>
    public int ProductId { get; set; }

    /// <summary>抽检/检验总数</summary>
    public decimal SampleQty { get; set; }

    /// <summary>合格数量</summary>
    public decimal QualifiedQty { get; set; }

    /// <summary>不良数量</summary>
    public decimal DefectQty { get; set; }

    /// <summary>合格率（0~1，如 0.98 表示 98%）</summary>
    public decimal PassRate { get; set; }
}

/// <summary>生成保养提醒的返回结果</summary>
public class GenerateRemindersResult
{
    /// <summary>本次新生成的提醒条数</summary>
    public int Created { get; set; }
}
