namespace ManagementCNCWorkshop.Api.Models;

/// <summary>设备点检明细项（对应纸质点检表的每一项）</summary>
public class EquipmentInspectionItem
{
    /// <summary>主键 ID</summary>
    public int Id { get; set; }

    /// <summary>所属点检记录 ID</summary>
    public int InspectionId { get; set; }

    /// <summary>项序号（1~11）</summary>
    public int ItemNo { get; set; }

    /// <summary>点检项名称快照</summary>
    public string ItemName { get; set; } = string.Empty;

    /// <summary>点检结果：Ok 正常 / Abnormal 异常 / Stopped 停机 / Rest 休息</summary>
    public string Status { get; set; } = "Ok";

    /// <summary>关联点检记录</summary>
    public EquipmentInspection? Inspection { get; set; }
}
