using System.Security.Claims;
using ManagementCNCWorkshop.Api.Data;
using ManagementCNCWorkshop.Api.Models;
using ManagementCNCWorkshop.Api.Models.Dtos;
using ManagementCNCWorkshop.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagementCNCWorkshop.Api.Controllers.Worker;

/// <summary>工人端基础查询：我的信息、车间、产品、设备（供扫码/下拉选择）</summary>
[ApiController]
[Route("api/worker")]
[Authorize(Roles = "Worker,Inspector,Programmer,Admin")]
[Tags("工人端-查询")]
public class WorkerQueryController(AppDbContext db, TenantContext tenant) : ControllerBase
{
    private TenantContext _tenant => tenant;
    /// <summary>获取当前登录用户信息</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(AuthUserDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Me()
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var employee = await db.Employees.Include(x => x.Workshop).FirstOrDefaultAsync(x => x.Id == id);
        if (employee is null)
            return NotFound();

        return Ok(new AuthUserDto
        {
            Id = employee.Id,
            EmployeeNo = employee.EmployeeNo,
            Name = employee.Name,
            Role = employee.Role,
            WorkshopId = employee.WorkshopId,
            WorkshopName = employee.Workshop?.Name
        });
    }

    /// <summary>车间列表（报工下拉选择用；租户用户只返回自己车间，管理员返回全部）</summary>
    [HttpGet("workshops")]
    [ProducesResponseType(typeof(List<Workshop>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Workshops()
    {
        var q = db.Workshops.AsQueryable();
        if (_tenant.WorkshopId is int wid)
            q = q.Where(x => x.Id == wid);
        return Ok(await q.OrderBy(x => x.Code).ToListAsync());
    }

    /// <summary>产品列表（扫码失败时手动选择）</summary>
    [HttpGet("products")]
    [ProducesResponseType(typeof(List<Product>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Products() =>
        Ok(await db.Products.OrderBy(x => x.Code).ToListAsync());

    /// <summary>扫码查询产品</summary>
    [HttpGet("products/by-qrcode")]
    [ProducesResponseType(typeof(Product), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ProductByQrCode([FromQuery] string code)
    {
        var product = await db.Products.FirstOrDefaultAsync(x => x.QrCode == code);
        return product is null ? NotFound(new { message = $"未找到二维码 {code} 对应的产品" }) : Ok(product);
    }

    /// <summary>设备列表（扫码失败时手动选择）</summary>
    [HttpGet("equipments")]
    [ProducesResponseType(typeof(List<Equipment>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Equipments() =>
        Ok(await db.Equipments.OrderBy(x => x.Code).ToListAsync());

    /// <summary>扫码查询设备</summary>
    [HttpGet("equipments/by-qrcode")]
    [ProducesResponseType(typeof(Equipment), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EquipmentByQrCode([FromQuery] string code)
    {
        var equipment = await db.Equipments.FirstOrDefaultAsync(x => x.QrCode == code);
        return equipment is null ? NotFound(new { message = $"未找到二维码 {code} 对应的设备" }) : Ok(equipment);
    }
}
