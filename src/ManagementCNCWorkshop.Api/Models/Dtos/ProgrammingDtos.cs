namespace ManagementCNCWorkshop.Api.Models.Dtos;

// ── 材料库 ──

public class CncMaterialDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public decimal VcRough { get; set; }
    public decimal VcFinish { get; set; }
    public decimal FeedRough { get; set; }
    public decimal FeedFinish { get; set; }
    public decimal FeedFace { get; set; }
    public decimal FeedGroove { get; set; }
    public decimal ApRough { get; set; }
    public string? Remark { get; set; }
}

// ── 机床 ──

public class CncMachineDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ControlSystem { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string? NetworkAddress { get; set; }
    public string? Remark { get; set; }
}

public class CncMachineInput
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ControlSystem { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string? NetworkAddress { get; set; }
    public string? Remark { get; set; }
    /// <summary>仅平台管理员（跨车间）创建时需要</summary>
    public int? WorkshopId { get; set; }
}

// ── 轮廓点 ──

public class CncContourPointDto
{
    public int Id { get; set; }
    public int Seq { get; set; }
    public string Type { get; set; } = "step";
    public decimal X { get; set; }
    public decimal Z { get; set; }
    public decimal? ArcR { get; set; }
    public string? ArcDir { get; set; }
    public decimal? Chamfer { get; set; }
    public decimal? ThreadPitch { get; set; }
    public string? Note { get; set; }
    public decimal? Confidence { get; set; }
    public bool Verified { get; set; }
    public bool Manual { get; set; }
    public string Source { get; set; } = "manual";
}

public class CncContourPointInput
{
    public int Seq { get; set; }
    public string Type { get; set; } = "step";
    public decimal X { get; set; }
    public decimal Z { get; set; }
    public decimal? ArcR { get; set; }
    public string? ArcDir { get; set; }
    public decimal? Chamfer { get; set; }
    public decimal? ThreadPitch { get; set; }
    public string? Note { get; set; }
    public bool Verified { get; set; }
}

// ── 程序 ──

public class CncProgramDto
{
    public int Id { get; set; }
    public string PartName { get; set; } = string.Empty;
    public string? DrawingNo { get; set; }
    public string? Material { get; set; }
    public string Datum { get; set; } = "right_face";
    public decimal? StockDia { get; set; }
    public decimal? StockLen { get; set; }
    public decimal? RoughAllowance { get; set; }
    public decimal? PerCutDepth { get; set; }
    public decimal? Feed { get; set; }
    public decimal? Rpm { get; set; }
    public int? ToolNo { get; set; }
    public decimal? ToolTipR { get; set; }
    public int? TipPos { get; set; }
    public decimal? GrooveWidth { get; set; }
    public decimal? ChamferC { get; set; }
    public string? ProcessMode { get; set; }
    public string? ToolPost { get; set; }
    public string? ControlSystem { get; set; }
    public string? MachineNo { get; set; }
    public string? Gcode { get; set; }
    /// <summary>正反面拆分后的独立程序（单个程序时与 Gcode 相同）</summary>
    public List<string>? GcodeParts { get; set; }
    public string Source { get; set; } = "manual";
    public string Status { get; set; } = "Draft";
    public int Step { get; set; }
    public int CreatedById { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int PointCount { get; set; }
    public List<CncContourPointDto> ContourPoints { get; set; } = new();
}

public class CncProgramInput
{
    public string PartName { get; set; } = string.Empty;
    public string? DrawingNo { get; set; }
    public string? Material { get; set; }
    public string Datum { get; set; } = "right_face";
    public decimal? StockDia { get; set; }
    public decimal? StockLen { get; set; }
    public decimal? RoughAllowance { get; set; }
    public decimal? PerCutDepth { get; set; }
    public decimal? Feed { get; set; }
    public decimal? Rpm { get; set; }
    public int? ToolNo { get; set; }
    public decimal? ToolTipR { get; set; }
    public int? TipPos { get; set; }
    public decimal? GrooveWidth { get; set; }
    public decimal? ChamferC { get; set; }
    public string? ProcessMode { get; set; }
    public string? ToolPost { get; set; }
    public string? ControlSystem { get; set; }
    public string? MachineNo { get; set; }
    public string Source { get; set; } = "manual";
    public List<CncContourPointInput>? Points { get; set; }
}

// ── 工艺预判 ──

public class ProcessPlanDto
{
    /// <summary>推荐加工方式：single / double / multi</summary>
    public string Mode { get; set; } = "single";

    /// <summary>推荐加工方式中文说明</summary>
    public string ModeText { get; set; } = string.Empty;

    /// <summary>工步顺序（含刀具）</summary>
    public List<string> Steps { get; set; } = new();

    /// <summary>给编程人员的建议（逐条）</summary>
    public List<string> Advice { get; set; } = new();

    /// <summary>是否检测到缩颈/下切（影响工序顺序与切槽刀）</summary>
    public bool HasGroove { get; set; }

    /// <summary>是否含螺纹</summary>
    public bool HasThread { get; set; }

    /// <summary>轮廓总长 mm</summary>
    public decimal TotalLen { get; set; }

    /// <summary>最大直径 mm</summary>
    public decimal MaxDia { get; set; }

    /// <summary>长径比（用于刚性/一次装夹判断）</summary>
    public decimal LwrRatio { get; set; }
}
