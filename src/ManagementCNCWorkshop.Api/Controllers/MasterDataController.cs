using ManagementCNCWorkshop.Api.Data;
using ManagementCNCWorkshop.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagementCNCWorkshop.Api.Controllers;

/// <summary>基础主数据：车间、员工、产品、设备的维护与扫码查询</summary>
[ApiController]
[Route("api/master")]
[Tags("主数据")]
public class MasterDataController(AppDbContext db) : ControllerBase
{
    /// <summary>获取当前数据库中的主数据 ID（测试接口时用）</summary>
    /// <remarks>Swagger 测试前先看这里，用返回的真实 Id，不要猜。</remarks>
    [HttpGet("demo-info")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DemoInfo()
    {
        return Ok(new
        {
            workshops = await db.Workshops.Select(x => new { x.Id, x.Code, x.Name }).ToListAsync(),
            employees = await db.Employees.Select(x => new { x.Id, x.EmployeeNo, x.Name, x.WorkshopId }).ToListAsync(),
            products = await db.Products.Select(x => new { x.Id, x.Code, x.Name, x.QrCode }).ToListAsync(),
            equipments = await db.Equipments.Select(x => new { x.Id, x.Code, x.Name, x.QrCode, x.WorkshopId }).ToListAsync(),
            tip = "报工示例：productId/products.id, workshopId/workshops.id, employeeId/employees.id, equipmentId 可选"
        });
    }

    /// <summary>获取车间列表</summary>
    /// <returns>全部车间，按编码排序</returns>
    [HttpGet("workshops")]
    [ProducesResponseType(typeof(List<Workshop>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Workshops() =>
        Ok(await db.Workshops.OrderBy(x => x.Code).ToListAsync());

    /// <summary>新增车间</summary>
    /// <param name="workshop">车间信息（Code、Name 必填）</param>
    [HttpPost("workshops")]
    [ProducesResponseType(typeof(Workshop), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateWorkshop([FromBody] Workshop workshop)
    {
        db.Workshops.Add(workshop);
        await db.SaveChangesAsync();
        return Ok(workshop);
    }

    /// <summary>获取员工列表</summary>
    /// <param name="workshopId">按车间筛选（可选）</param>
    [HttpGet("employees")]
    [ProducesResponseType(typeof(List<Employee>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Employees([FromQuery] int? workshopId)
    {
        var q = db.Employees.Include(x => x.Workshop).AsQueryable();
        if (workshopId.HasValue)
            q = q.Where(x => x.WorkshopId == workshopId);
        return Ok(await q.OrderBy(x => x.EmployeeNo).ToListAsync());
    }

    /// <summary>新增员工</summary>
    /// <param name="employee">员工信息（EmployeeNo、Name、WorkshopId 必填）</param>
    [HttpPost("employees")]
    [ProducesResponseType(typeof(Employee), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateEmployee([FromBody] Employee employee)
    {
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
    /// <param name="product">产品信息（Code、Name 必填，QrCode 用于扫码匹配）</param>
    [HttpPost("products")]
    [ProducesResponseType(typeof(Product), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateProduct([FromBody] Product product)
    {
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return Ok(product);
    }

    /// <summary>扫码查询产品</summary>
    /// <param name="code">二维码内容，如 PROD:P001</param>
    /// <returns>匹配的产品信息，未找到返回 404</returns>
    [HttpGet("products/by-qrcode")]
    [ProducesResponseType(typeof(Product), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ProductByQrCode([FromQuery] string code)
    {
        var product = await db.Products.FirstOrDefaultAsync(x => x.QrCode == code);
        return product is null ? NotFound() : Ok(product);
    }

    /// <summary>获取设备列表</summary>
    /// <param name="workshopId">按车间筛选（可选）</param>
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
    /// <param name="equipment">设备信息（Code、Name、WorkshopId 必填）</param>
    [HttpPost("equipments")]
    [ProducesResponseType(typeof(Equipment), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateEquipment([FromBody] Equipment equipment)
    {
        db.Equipments.Add(equipment);
        await db.SaveChangesAsync();
        return Ok(equipment);
    }

    /// <summary>扫码查询设备</summary>
    /// <param name="code">二维码内容，如 EQ:CNC-01</param>
    /// <returns>匹配的设备信息，未找到返回 404</returns>
    [HttpGet("equipments/by-qrcode")]
    [ProducesResponseType(typeof(Equipment), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EquipmentByQrCode([FromQuery] string code)
    {
        var equipment = await db.Equipments.FirstOrDefaultAsync(x => x.QrCode == code);
        return equipment is null ? NotFound() : Ok(equipment);
    }
}
