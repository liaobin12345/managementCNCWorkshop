using ManagementCNCWorkshop.Api.Data;
using ManagementCNCWorkshop.Api.Models;
using ManagementCNCWorkshop.Api.Models.Dtos;
using ManagementCNCWorkshop.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagementCNCWorkshop.Api.Controllers;

/// <summary>认证登录：用于运营后台和小程序员工端</summary>
[ApiController]
[Route("api/auth")]
[Tags("认证")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtTokenService _tokenService;
    private readonly WechatService _wechatService;

    public AuthController(AppDbContext db, JwtTokenService tokenService, WechatService wechatService)
    {
        _db = db;
        _tokenService = tokenService;
        _wechatService = wechatService;
    }
    /// <summary>账号密码登录</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var employee = await _db.Employees.Include(x => x.Workshop)
            .FirstOrDefaultAsync(x => x.EmployeeNo == req.EmployeeNo);
        if (employee is null)
            return Unauthorized(new { message = "账号或密码错误" });

        if (string.IsNullOrWhiteSpace(employee.PasswordHash) || !PasswordHasher.Verify(req.Password, employee.PasswordHash))
            return Unauthorized(new { message = "账号或密码错误" });

        return Ok(BuildLoginResponse(employee));
    }

    /// <summary>微信小程序登录：code 换 openid，已绑定员工则直接登录</summary>
    /// <remarks>
    /// 首次使用该微信的用户返回 <c>needsBind=true</c> 和 openid，
    /// 客户端引导用户用工号+密码调用 <c>/api/auth/wechat-bind</c> 完成绑定。
    /// </remarks>
    [HttpPost("wechat-login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> WechatLogin([FromBody] WechatLoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Code))
            return BadRequest(new { message = "缺少微信登录凭证 code" });

        string openId;
        try
        {
            openId = await _wechatService.ExchangeCodeForOpenIdAsync(req.Code);
        }
        catch (WechatNotConfiguredException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = "微信登录失败：" + ex.Message });
        }

        var employee = await _db.Employees.Include(x => x.Workshop)
            .FirstOrDefaultAsync(x => x.WeChatOpenId == openId);
        if (employee is null)
            return Ok(new { needsBind = true, openid = openId });

        return Ok(BuildLoginResponse(employee));
    }

    /// <summary>微信绑定员工账号（首次登录引导绑定，后续该微信自动登录）</summary>
    /// <remarks>
    /// 用工号+密码校验员工身份后，把当前 openid 绑定到该员工；
    /// 同一 openid 已被其他员工绑定时拒绝，避免顶号。
    /// </remarks>
    [HttpPost("wechat-bind")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> WechatBind([FromBody] WechatBindRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.OpenId))
            return BadRequest(new { message = "缺少微信 openid" });

        var employee = await _db.Employees.Include(x => x.Workshop)
            .FirstOrDefaultAsync(x => x.EmployeeNo == req.EmployeeNo);
        if (employee is null || string.IsNullOrWhiteSpace(employee.PasswordHash)
            || !PasswordHasher.Verify(req.Password, employee.PasswordHash))
            return Unauthorized(new { message = "工号或密码错误" });

        var alreadyBound = await _db.Employees
            .FirstOrDefaultAsync(x => x.WeChatOpenId == req.OpenId && x.Id != employee.Id);
        if (alreadyBound is not null)
            return BadRequest(new { message = $"该微信已绑定账号 {alreadyBound.EmployeeNo}，请用该账号登录或联系管理员解绑" });

        employee.WeChatOpenId = req.OpenId;
        await _db.SaveChangesAsync();

        return Ok(BuildLoginResponse(employee));
    }

    private LoginResponse BuildLoginResponse(Employee employee)
    {
        var token = _tokenService.CreateToken(employee, employee.Role);
        return new LoginResponse
        {
            Token = token,
            Employee = new AuthUserDto
            {
                Id = employee.Id,
                EmployeeNo = employee.EmployeeNo,
                Name = employee.Name,
                Role = employee.Role,
                WorkshopId = employee.WorkshopId,
                WorkshopName = employee.Workshop?.Name
            }
        };
    }
}

/// <summary>微信登录请求（登录凭证 code）</summary>
public class WechatLoginRequest
{
    /// <summary>uni.login 获取的临时登录凭证</summary>
    public string Code { get; set; } = string.Empty;
}

/// <summary>微信绑定请求（首次登录时用工号密码完成绑定）</summary>
public class WechatBindRequest
{
    /// <summary>微信 openid（由 wechat-login 接口返回）</summary>
    public string OpenId { get; set; } = string.Empty;

    /// <summary>工号</summary>
    public string EmployeeNo { get; set; } = string.Empty;

    /// <summary>密码</summary>
    public string Password { get; set; } = string.Empty;
}
