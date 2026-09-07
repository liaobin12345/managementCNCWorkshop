namespace ManagementCNCWorkshop.Api.Models;

/// <summary>编程助手 - 轮廓坐标点</summary>
public class CncContourPoint : ITenantScoped
{
    public int Id { get; set; }

    /// <summary>所属车间 ID</summary>
    public int WorkshopId { get; set; }

    /// <summary>所属程序 ID</summary>
    public int CncProgramId { get; set; }

    /// <summary>顺序号（从 1 开始）</summary>
    public int Seq { get; set; }

    /// <summary>特征类型：face / step / chamfer / arc / thread / groove / bore / cutoff</summary>
    public string Type { get; set; } = "step";

    /// <summary>X 直径值 (mm)</summary>
    public decimal X { get; set; }

    /// <summary>Z 坐标 (mm)，基准右端面 Z0 时通常 ≤0</summary>
    public decimal Z { get; set; }

    /// <summary>圆弧半径 (type=arc)</summary>
    public decimal? ArcR { get; set; }

    /// <summary>圆弧方向：cw / ccw（G02 / G03）</summary>
    public string? ArcDir { get; set; }

    /// <summary>倒角 C 值</summary>
    public decimal? Chamfer { get; set; }

    /// <summary>螺距 (type=thread)</summary>
    public decimal? ThreadPitch { get; set; }

    /// <summary>备注，如 C2、R3、M20x1.5</summary>
    public string? Note { get; set; }

    /// <summary>置信度 0~1（dxf=1）</summary>
    public decimal? Confidence { get; set; }

    /// <summary>是否已人工确认</summary>
    public bool Verified { get; set; }

    /// <summary>是否手动编辑过</summary>
    public bool Manual { get; set; }

    /// <summary>来源：manual / dxf / vision</summary>
    public string Source { get; set; } = "manual";

    // Navigation
    public CncProgram? Program { get; set; }
}