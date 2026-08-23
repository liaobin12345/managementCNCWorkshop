using ManagementCNCWorkshop.Api.Data;
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
public class AuthController(AppDbContext db, JwtTokenService tokenService) : ControllerBase
{
    /// <summary>账号密码登录</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var employee = await db.Employees.Include(x => x.Workshop)
            .FirstOrDefaultAsync(x => x.EmployeeNo == req.EmployeeNo);
        if (employee is null)
            return Unauthorized(new { message = "账号或密码错误" });

        if (string.IsNullOrWhiteSpace(employee.PasswordHash) || !PasswordHasher.Verify(req.Password, employee.PasswordHash))
            return Unauthorized(new { message = "账号或密码错误" });

        var token = tokenService.CreateToken(employee, employee.Role);

        return Ok(new LoginResponse
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
        });
    }
}
