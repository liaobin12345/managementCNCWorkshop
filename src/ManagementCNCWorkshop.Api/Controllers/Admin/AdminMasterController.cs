using ManagementCNCWorkshop.Api.Data;
using ManagementCNCWorkshop.Api.Models;
using ManagementCNCWorkshop.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagementCNCWorkshop.Api.Controllers.Admin;

/// <summary>运营后台主数据：车间、员工、产品、设备的维护</summary>
[ApiController]
[Route("api/admin/master")]
[Authorize(Roles = "Admin")]
[Tags("运营后台-主数据")]
public class AdminMasterController(AppDbContext db) : ControllerBase
{
    /// <summary>获取当前数据库中的主数据 ID（测试/联调用）</summary>
    [HttpGet("demo-info")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DemoInfo()
    {
        return Ok(new
        {
            workshops = await db.Workshops.Select(x => new { x.Id, x.Code, x.Name }).ToListAsync(),
            employees = await db.Employees.Select(x => new { x.Id, x.EmployeeNo, x.Name, x.Role, x.WorkshopId }).ToListAsync(),
            products = await db.Products.Select(x => new { x.Id, x.Code, x.Name, x.QrCode }).ToListAsync(),
            equipments = await db.Equipments.Select(x => new { x.Id, x.Code, x.Name, x.QrCode, x.WorkshopId }).ToListAsync()
        });
    }

    /// <summary>获取车间列表</summary>
    [HttpGet("workshops")]
    [ProducesResponseType(typeof(List<Workshop>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Workshops() =>
        Ok(await db.Workshops.OrderBy(x => x.Code).ToListAsync());

    /// <summary>新增车间</summary>
    [HttpPost("workshops")]
    [ProducesResponseType(typeof(Workshop), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateWorkshop([FromBody] Workshop workshop)
    {
        if (await db.Workshops.AnyAsync(x => x.Code == workshop.Code))
            return BadRequest(new { message = $"车间编码 {workshop.Code} 已存在" });

        db.Workshops.Add(workshop);
        await db.SaveChangesAsync();
        return Ok(workshop);
    }

    /// <summary>获取员工列表</summary>
    [HttpGet("employees")]
    [ProducesResponseType(typeof(List<Employee>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Employees([FromQuery] int? workshopId, [FromQuery] string? role)
    {
        var q = db.Employees.Include(x => x.Workshop).AsQueryable();
        if (workshopId.HasValue)
            q = q.Where(x => x.WorkshopId == workshopId);
        if (!string.IsNullOrEmpty(role))
            q = q.Where(x => x.Role == role);
        return Ok(await q.OrderBy(x => x.EmployeeNo).ToListAsync());
    }

    /// <summary>新增员工（工号唯一，默认密码 123456）</summary>
    [HttpPost("employees")]
    [ProducesResponseType(typeof(Employee), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateEmployee([FromBody] Employee employee)
    {
        if (await db.Employees.AnyAsync(x => x.EmployeeNo == employee.EmployeeNo))
            return BadRequest(new { message = $"工号 {employee.EmployeeNo} 已存在" });

        if (string.IsNullOrEmpty(employee.PasswordHash))
            employee.PasswordHash = PasswordHasher.Hash("123456");

        db.Employees.Add(employee);
        await db.SaveChangesAsync();
        return Ok(employee);
    }

    /// <summary>获取产品列表</summary>
    [HttpGet("products")]
    [ProducesResponseType(typeof(List<Product>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Products() =>
        Ok(await db.Products.OrderBy(x => x.Code).ToListAsync());

    /// <summary>新增产品</summary>
    [HttpPost("products")]
    [ProducesResponseType(typeof(Product), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateProduct([FromBody] Product product)
    {
        if (await db.Products.AnyAsync(x => x.Code == product.Code))
            return BadRequest(new { message = $"产品编码 {product.Code} 已存在" });

        db.Products.Add(product);
        await db.SaveChangesAsync();
        return Ok(product);
    }

    /// <summary>扫码查询产品</summary>
    [HttpGet("products/by-qrcode")]
    [ProducesResponseType(typeof(Product), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ProductByQrCode([FromQuery] string code)
    {
        var product = await db.Products.FirstOrDefaultAsync(x => x.QrCode == code);
        return product is null ? NotFound() : Ok(product);
    }

    /// <summary>获取设备列表</summary>
    [HttpGet("equipments")]
    [ProducesResponseType(typeof(List<Equipment>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Equipments([FromQuery] int? workshopId)
    {
        var q = db.Equipments.Include(x => x.Workshop).AsQueryable();
        if (workshopId.HasValue)
            q = q.Where(x => x.WorkshopId == workshopId);
        return Ok(await q.OrderBy(x => x.Code).ToListAsync());
    }

    /// <summary>新增设备</summary>
    [HttpPost("equipments")]
    [ProducesResponseType(typeof(Equipment), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateEquipment([FromBody] Equipment equipment)
    {
        if (await db.Equipments.AnyAsync(x => x.Code == equipment.Code))
            return BadRequest(new { message = $"设备编码 {equipment.Code} 已存在" });
        if (equipment.WorkshopId <= 0 || !await db.Workshops.AnyAsync(x => x.Id == equipment.WorkshopId))
            return BadRequest(new { message = "所选车间不存在" });

        db.Equipments.Add(equipment);
        await db.SaveChangesAsync();
        return Ok(equipment);
    }

    /// <summary>扫码查询设备</summary>
    [HttpGet("equipments/by-qrcode")]
    [ProducesResponseType(typeof(Equipment), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EquipmentByQrCode([FromQuery] string code)
    {
        var equipment = await db.Equipments.FirstOrDefaultAsync(x => x.QrCode == code);
        return equipment is null ? NotFound() : Ok(equipment);
    }
}
