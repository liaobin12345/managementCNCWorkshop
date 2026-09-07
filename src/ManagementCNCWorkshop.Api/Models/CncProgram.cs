namespace ManagementCNCWorkshop.Api.Models;

/// <summary>编程助手 - 加工程序</summary>
public class CncProgram : ITenantScoped
{
    public int Id { get; set; }

    /// <summary>所属车间 ID</summary>
    public int WorkshopId { get; set; }

    /// <summary>零件名称</summary>
    public string PartName { get; set; } = string.Empty;

    /// <summary>图号</summary>
    public string? DrawingNo { get; set; }

    /// <summary>材料</summary>
    public string? Material { get; set; }

    /// <summary>基准：right_face / left_face</summary>
    public string Datum { get; set; } = "right_face";

    /// <summary>棒料直径（必填；两次加工时按此直径做棒料倒角去毛刺）</summary>
    public decimal? StockDia { get; set; }

    /// <summary>毛坯长度</summary>
    public decimal? StockLen { get; set; }

    /// <summary>精车余量（单边）</summary>
    public decimal? RoughAllowance { get; set; }

    /// <summary>每刀吃刀量</summary>
    public decimal? PerCutDepth { get; set; }

    /// <summary>进给量 mm/r</summary>
    public decimal? Feed { get; set; }

    /// <summary>主轴转速 rpm</summary>
    public decimal? Rpm { get; set; }

    /// <summary>刀号</summary>
    public int? ToolNo { get; set; }

    /// <summary>刀尖圆弧半径</summary>
    public decimal? ToolTipR { get; set; }

    /// <summary>切槽刀宽 mm</summary>
    public decimal? GrooveWidth { get; set; }

    /// <summary>未注倒角 C（未标注处去毛刺倒角，mm，默认 0.2）</summary>
    public decimal? ChamferC { get; set; }

    /// <summary>加工方式：single 单次装夹 / double 两次加工 / multi 多次</summary>
    public string? ProcessMode { get; set; }

    /// <summary>刀架类型：turret 刀塔（默认，换刀前 Z 退 ≥150）/ gang 排刀</summary>
    public string? ToolPost { get; set; }

    /// <summary>假想刀尖位置 0~9</summary>
    public int? TipPos { get; set; }

    /// <summary>目标机床系统，如 Fanuc 0i-TF</summary>
    public string? ControlSystem { get; set; }

    /// <summary>目标机床编号</summary>
    public string? MachineNo { get; set; }

    /// <summary>生成的 G 代码</summary>
    public string? Gcode { get; set; }

    /// <summary>来源：manual / dxf / vision</summary>
    public string Source { get; set; } = "manual";

    /// <summary>状态：Draft / Confirmed / Generated / Exported</summary>
    public string Status { get; set; } = "Draft";

    /// <summary>向导步骤进度 0~5</summary>
    public int Step { get; set; }

    /// <summary>创建者 ID</summary>
    public int CreatedById { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>更新时间</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Employee? Creator { get; set; }
    public Workshop? Workshop { get; set; }
    public List<CncContourPoint> ContourPoints { get; set; } = new();
}