using ManagementCNCWorkshop.Api.Models;

namespace ManagementCNCWorkshop.Api.Services.Programming;

/// <summary>
/// 数控车常用材料推荐切削参数（硬质合金刀具，行业经验值）。
/// 同时用于：种子数据初始化材料库、生成器按程序材料自动匹配参数。
/// </summary>
public static class CncMaterialDefaults
{
    public static readonly IReadOnlyList<CncMaterial> All = new List<CncMaterial>
    {
        new() { Name = "45# 钢（中碳钢）", Category = "碳钢", VcRough = 180, VcFinish = 220, FeedRough = 0.25m, FeedFinish = 0.10m, FeedFace = 0.12m, FeedGroove = 0.06m, ApRough = 2.0m, Remark = "最常用料；转速按 Vc 与首段直径自动换算" },
        new() { Name = "Q235（低碳钢）", Category = "碳钢", VcRough = 160, VcFinish = 200, FeedRough = 0.25m, FeedFinish = 0.10m, FeedFace = 0.12m, FeedGroove = 0.06m, ApRough = 2.5m, Remark = "料软偏黏，大切深中转速" },
        new() { Name = "40Cr（合金结构钢）", Category = "合金钢", VcRough = 120, VcFinish = 160, FeedRough = 0.20m, FeedFinish = 0.10m, FeedFace = 0.10m, FeedGroove = 0.05m, ApRough = 1.5m, Remark = "调质后适当降速" },
        new() { Name = "42CrMo（合金钢调质）", Category = "合金钢", VcRough = 100, VcFinish = 140, FeedRough = 0.18m, FeedFinish = 0.09m, FeedFace = 0.10m, FeedGroove = 0.05m, ApRough = 1.2m, Remark = "高硬度，注意刀片牌号" },
        new() { Name = "304 不锈钢", Category = "不锈钢", VcRough = 120, VcFinish = 150, FeedRough = 0.20m, FeedFinish = 0.10m, FeedFace = 0.10m, FeedGroove = 0.05m, ApRough = 1.5m, Remark = "易粘刀，进给不宜过小、不断续屑" },
        new() { Name = "316 不锈钢", Category = "不锈钢", VcRough = 100, VcFinish = 130, FeedRough = 0.18m, FeedFinish = 0.09m, FeedFace = 0.10m, FeedGroove = 0.05m, ApRough = 1.2m, Remark = "更黏更韧，降速降切深" },
        new() { Name = "HT200 灰铸铁", Category = "铸铁", VcRough = 150, VcFinish = 180, FeedRough = 0.30m, FeedFinish = 0.12m, FeedFace = 0.15m, FeedGroove = 0.08m, ApRough = 3.0m, Remark = "大切深高进给；碎屑注意防护" },
        new() { Name = "6061 铝合金", Category = "铝合金", VcRough = 400, VcFinish = 500, FeedRough = 0.30m, FeedFinish = 0.12m, FeedFace = 0.15m, FeedGroove = 0.08m, ApRough = 3.0m, Remark = "高转速，注意机床主轴上限" },
        new() { Name = "7075 铝合金", Category = "铝合金", VcRough = 450, VcFinish = 550, FeedRough = 0.30m, FeedFinish = 0.12m, FeedFace = 0.15m, FeedGroove = 0.08m, ApRough = 3.0m, Remark = "可承受更高线速度" },
        new() { Name = "H62 黄铜", Category = "铜合金", VcRough = 250, VcFinish = 300, FeedRough = 0.25m, FeedFinish = 0.10m, FeedFace = 0.12m, FeedGroove = 0.06m, ApRough = 2.5m, Remark = "" },
        new() { Name = "TC4 钛合金", Category = "钛合金", VcRough = 60, VcFinish = 80, FeedRough = 0.15m, FeedFinish = 0.08m, FeedFace = 0.08m, FeedGroove = 0.04m, ApRough = 1.0m, Remark = "低线速度+大流量冷却，防烧刀" },
    };

    /// <summary>
    /// 按程序里填的材料名模糊匹配材料库（"304"、"45#钢"、"6061铝"、"Q235" 等写法都能命中）。
    /// </summary>
    public static CncMaterial? Find(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var s = Normalize(name);

        foreach (var m in All)
        {
            var key = Normalize(m.Name);
            var bare = key.Split('（')[0]; // "45钢" / "304不锈钢"
            if (key.Contains(s) || s.Contains(key) || s.Contains(bare) || bare.Contains(s))
                return m;
        }

        // 别名兜底
        return s switch
        {
            var x when x.Contains("42crmo") => All[3],
            var x when x.Contains("40cr") => All[2],
            var x when x.Contains("316") => All[5],
            var x when x.Contains("304") => All[4],
            var x when x.Contains("ht") || x.Contains("铸铁") => All[6],
            var x when x.Contains("7075") => All[8],
            var x when x.Contains("6061") || x.Contains("铝") => All[7],
            var x when x.Contains("h62") || x.Contains("黄铜") || x.Contains("铜") => All[9],
            var x when x.Contains("tc4") || x.Contains("钛") => All[10],
            var x when x.Contains("q235") || x == "a3" => All[1],
            var x when x.Contains("45") => All[0],
            _ => null
        };
    }

    private static string Normalize(string s) =>
        s.Trim().ToLowerInvariant().Replace(" ", "").Replace("#", "").Replace("＃", "");
}
