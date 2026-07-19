namespace ManagementCNCWorkshop.Api.Models;

/// <summary>员工（操作工、质检员等，预留微信登录）</summary>
public class Employee
{
    /// <summary>主键 ID</summary>
    public int Id { get; set; }

    /// <summary>工号，唯一</summary>
    public string EmployeeNo { get; set; } = string.Empty;

    /// <summary>姓名</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>手机号</summary>
    public string? Phone { get; set; }

    /// <summary>所属车间 ID</summary>
    public int WorkshopId { get; set; }

    /// <summary>微信小程序 OpenId（登录后绑定，V1 暂未启用）</summary>
    public string? WeChatOpenId { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>所属车间（查询时关联返回）</summary>
    public Workshop? Workshop { get; set; }
}
