namespace ManagementCNCWorkshop.Api.Models;

/// <summary>设备点检表模板（固定 11 项，来自现场纸质点检表 QR-060）</summary>
public static class EquipmentInspectionTemplate
{
    /// <summary>11 个点检项（序号 + 名称）</summary>
    public static readonly (int No, string Name)[] Items =
    {
        (1, "导轨油油量"),
        (2, "切削油油量"),
        (3, "液压油油量"),
        (4, "机床有无漏液"),
        (5, "安全门、开关是否完好"),
        (6, "机床开动时声音是否异常"),
        (7, "操作面板是否完好"),
        (8, "电器部件是否完好"),
        (9, "机械部件是否完好"),
        (10, "每个产品装夹前，清理一次刀具、夹具渣屑"),
        (11, "对应加工作业指导书是否张贴在机台上"),
    };

    /// <summary>点检结果中文名</summary>
    public static string StatusName(string status) => status switch
    {
        "Ok" => "正常",
        "Abnormal" => "异常",
        "Stopped" => "停机",
        "Rest" => "休息",
        _ => status,
    };
}
