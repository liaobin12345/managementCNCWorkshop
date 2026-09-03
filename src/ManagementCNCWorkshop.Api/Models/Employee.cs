namespace ManagementCNCWorkshop.Api.Models;

/// <summary>员工（操作工、质检员、管理员，预留微信登录）</summary>
public class Employee : ITenantScoped
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

    /// <summary>角色：Admin 管理员 / Worker 操作工 / Inspector 质检员</summary>
    public string Role { get; set; } = "Worker";

    /// <summary>登录密码哈希（PBKDF2），V1 演示账号，生产环境请接入微信登录</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>微信小程序 OpenId（微信登录后绑定）</summary>
    public string? WeChatOpenId { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>所属车间（查询时关联返回）</summary>
    public Workshop? Workshop { get; set; }
}
