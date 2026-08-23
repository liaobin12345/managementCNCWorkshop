namespace ManagementCNCWorkshop.Api.Models.Dtos;

/// <summary>账号密码登录请求</summary>
public class LoginRequest
{
    /// <summary>工号</summary>
    /// <example>E900</example>
    public string EmployeeNo { get; set; } = string.Empty;

    /// <summary>密码</summary>
    /// <example>123456</example>
    public string Password { get; set; } = string.Empty;
}

/// <summary>登录响应</summary>
public class LoginResponse
{
    /// <summary>JWT 访问令牌（后续请求放在 Authorization: Bearer 头中）</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>当前登录用户信息</summary>
    public AuthUserDto Employee { get; set; } = new();
}

/// <summary>当前登录用户信息</summary>
public class AuthUserDto
{
    /// <summary>员工 ID</summary>
    public int Id { get; set; }

    /// <summary>工号</summary>
    public string EmployeeNo { get; set; } = string.Empty;

    /// <summary>姓名</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>角色：Admin / Worker / Inspector</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>所属车间 ID</summary>
    public int WorkshopId { get; set; }

    /// <summary>所属车间名称</summary>
    public string? WorkshopName { get; set; }
}
