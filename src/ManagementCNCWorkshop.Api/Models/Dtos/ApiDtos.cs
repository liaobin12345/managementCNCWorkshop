namespace ManagementCNCWorkshop.Api.Models.Dtos;

/// <summary>分页结果</summary>
public class PagedResult<T>
{
    /// <summary>总记录数</summary>
    public int Total { get; set; }

    /// <summary>当前页码（从 1 开始）</summary>
    public int Page { get; set; }

    /// <summary>每页条数</summary>
    public int PageSize { get; set; }

    /// <summary>当前页数据</summary>
    public List<T> Items { get; set; } = new();
}

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

    /// <summary>工艺流转卡 ID（报工归属到具体批次，可选但建议传）</summary>
    public int? ProcessCardId { get; set; }

    /// <summary>工序序号（属于该流转卡的第几道工序）</summary>
    public int? ProcessStepNo { get; set; }

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

    public int? ProcessCardId { get; set; }
    public string? CardCode { get; set; }
    public int? ProcessStepNo { get; set; }
    public string? ProcessStepName { get; set; }
    public bool HasProcessContext => ProcessCardId.HasValue && ProcessStepNo.HasValue;
}

/// <summary>按产品/工序的质量统计</summary>
public class QualityProcessStatDto
{
    public int ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? ProductSpec { get; set; }
    public int? ProcessCardId { get; set; }
    public string? CardCode { get; set; }
    public int? ProcessStepNo { get; set; }
    public string? ProcessStepName { get; set; }
    public decimal SampleQty { get; set; }
    public decimal QualifiedQty { get; set; }
    public decimal DefectQty { get; set; }
    public decimal PassRate => SampleQty > 0 ? QualifiedQty / SampleQty : 0m;
}

/// <summary>质量趋势点</summary>
public class QualityTrendDto
{
    /// <summary>日期（yyyy-MM-dd）</summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>抽检/检验总数</summary>
    public decimal SampleQty { get; set; }

    /// <summary>合格数量</summary>
    public decimal QualifiedQty { get; set; }

    /// <summary>不良数量</summary>
    public decimal DefectQty { get; set; }

    /// <summary>合格率（0~1）</summary>
    public decimal PassRate { get; set; }
}

/// <summary>生成保养提醒的返回结果</summary>
public class GenerateRemindersResult
{
    /// <summary>本次新生成的提醒条数</summary>
    public int Created { get; set; }
}

/// <summary>工人端工作台概览（今日产量/合格率/不良）</summary>
public class WorkerSummaryDto
{
    /// <summary>车间 ID</summary>
    public int WorkshopId { get; set; }

    /// <summary>总产量</summary>
    public decimal TotalQty { get; set; }

    /// <summary>合格数量</summary>
    public decimal QualifiedQty { get; set; }

    /// <summary>不良数量</summary>
    public decimal DefectQty { get; set; }

    /// <summary>合格率（0~1）</summary>
    public decimal PassRate { get; set; }
}

/// <summary>工人端按产品统计</summary>
public class WorkerProductStatDto
{
    /// <summary>产品 ID</summary>
    public int ProductId { get; set; }

    /// <summary>产品名称</summary>
    public string? ProductName { get; set; }

    /// <summary>产品规格</summary>
    public string? ProductSpec { get; set; }

    /// <summary>总产量</summary>
    public decimal TotalQty { get; set; }

    /// <summary>合格数量</summary>
    public decimal QualifiedQty { get; set; }

    /// <summary>不良数量</summary>
    public decimal DefectQty { get; set; }

    /// <summary>合格率（0~1）</summary>
    public decimal PassRate { get; set; }
}

/// <summary>工序完成数量统计</summary>
public class WorkerProcessStepStatDto
{
    public int ProductId { get; set; }
    public string? ProductName { get; set; }
    public string? ProductSpec { get; set; }
    public int? ProcessCardId { get; set; }
    public string? CardCode { get; set; }
    public int? StepNo { get; set; }
    public string? StepName { get; set; }
    public decimal TotalQty { get; set; }
    public decimal QualifiedQty { get; set; }
    public decimal DefectQty { get; set; }
    public int ReportCount { get; set; }
    public decimal PassRate => TotalQty > 0 ? QualifiedQty / TotalQty : 1m;
}

/// <summary>操作工产量排名（一个操作工可对应多台机台）</summary>
public class WorkerOperatorRankingDto
{
    public int EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public string? EmployeeNo { get; set; }
    public decimal TotalQty { get; set; }
    public decimal QualifiedQty { get; set; }
    public decimal DefectQty { get; set; }
    public int MachineCount { get; set; }
    public List<WorkerMachineOutputDto> Machines { get; set; } = new();
    public decimal PassRate => TotalQty > 0 ? QualifiedQty / TotalQty : 1m;
}

/// <summary>操作工操作的单台机台的工序产量</summary>
public class WorkerMachineStepDto
{
    public int? ProcessCardId { get; set; }
    public string? CardCode { get; set; }
    public int? StepNo { get; set; }
    public string? StepName { get; set; }
    public decimal TotalQty { get; set; }
    public decimal QualifiedQty { get; set; }
    public decimal DefectQty { get; set; }
}

/// <summary>操作工操作的单台机台产量（含该机台上的工序明细）</summary>
public class WorkerMachineOutputDto
{
    public int? EquipmentId { get; set; }
    public string? EquipmentCode { get; set; }
    public string? EquipmentName { get; set; }
    public decimal TotalQty { get; set; }
    public decimal QualifiedQty { get; set; }
    public decimal DefectQty { get; set; }
    public List<WorkerMachineStepDto> Steps { get; set; } = new();
}

/// <summary>工艺流转进度与操作工排名综合结果</summary>
public class WorkerProcessStatsDto
{
    public List<WorkerProcessStepStatDto> ProcessSteps { get; set; } = new();
    public List<WorkerOperatorRankingDto> OperatorRankings { get; set; } = new();
}

/// <summary>工人端产量趋势点</summary>
public class WorkerTrendPointDto
{
    /// <summary>日期（yyyy-MM-dd）</summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>总产量</summary>
    public decimal TotalQty { get; set; }

    /// <summary>合格数量</summary>
    public decimal QualifiedQty { get; set; }

    /// <summary>不良数量</summary>
    public decimal DefectQty { get; set; }
}
