namespace ManagementCNCWorkshop.Api.Models;

/// <summary>数控车常用材料库（含按行业经验配置的推荐切削参数，硬质合金刀具）</summary>
public class CncMaterial
{
    public int Id { get; set; }

    /// <summary>材料名称，如 45# 钢（中碳钢）、304 不锈钢</summary>
    public string Name { get; set; } = "";

    /// <summary>类别：碳钢/合金钢/不锈钢/铸铁/铝合金/铜合金/钛合金</summary>
    public string? Category { get; set; }

    /// <summary>粗车线速度 Vc（m/min）</summary>
    public decimal VcRough { get; set; }

    /// <summary>精车线速度 Vc（m/min）</summary>
    public decimal VcFinish { get; set; }

    /// <summary>粗车进给 f（mm/r）</summary>
    public decimal FeedRough { get; set; }

    /// <summary>精车进给 f（mm/r）</summary>
    public decimal FeedFinish { get; set; }

    /// <summary>端面进给 f（mm/r）</summary>
    public decimal FeedFace { get; set; }

    /// <summary>切槽进给 f（mm/r）</summary>
    public decimal FeedGroove { get; set; }

    /// <summary>粗车每刀切深 ap（mm，单边）</summary>
    public decimal ApRough { get; set; }

    public string? Remark { get; set; }
}
