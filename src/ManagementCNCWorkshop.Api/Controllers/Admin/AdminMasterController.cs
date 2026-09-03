using ManagementCNCWorkshop.Api.Data;
using ManagementCNCWorkshop.Api.Models;
using ManagementCNCWorkshop.Api.Models.Dtos;
using ManagementCNCWorkshop.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ManagementCNCWorkshop.Api.Controllers.Admin;

/// <summary>运营后台主数据：车间、员工、产品、设备的维护</summary>
[ApiController]
[Route("api/admin/master")]
[Authorize(Roles = "Admin")]
[Tags("运营后台-主数据")]
public class AdminMasterController(AppDbContext db) : ControllerBase
{
    /// <summary>当前登录用户所属车间 ID（JWT 声明，用于默认归属）</summary>
    private int CurrentUserWorkshopId() =>
        int.TryParse(User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier), out var _)
            && int.TryParse(User.FindFirstValue("WorkshopId"), out var wid)
            ? wid
            : 0;

    /// <summary>获取当前数据库中的主数据 ID（测试/联调用）</summary>
    [HttpGet("demo-info")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DemoInfo()
    {
        return Ok(new
        {
            workshops = await db.Workshops.Select(x => new { x.Id, x.Code, x.Name }).ToListAsync(),
            employees = await db.Employees.Select(x => new { x.Id, x.EmployeeNo, x.Name, x.Role, x.WorkshopId }).ToListAsync(),
            products = await db.Products.Select(x => new { x.Id, x.Code, x.Name, x.QrCode, x.ImageUrl }).ToListAsync(),
            equipments = await db.Equipments.Select(x => new { x.Id, x.Code, x.Name, x.QrCode, x.ImageUrl, x.WorkshopId }).ToListAsync()
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

    /// <summary>更新车间</summary>
    [HttpPut("workshops/{id}")]
    [ProducesResponseType(typeof(Workshop), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateWorkshop(int id, [FromBody] Workshop input)
    {
        var workshop = await db.Workshops.FirstOrDefaultAsync(x => x.Id == id);
        if (workshop is null)
            return NotFound(new { message = "车间不存在" });
        if (await db.Workshops.AnyAsync(x => x.Code == input.Code && x.Id != id))
            return BadRequest(new { message = $"车间编码 {input.Code} 已存在" });

        workshop.Code = input.Code;
        workshop.Name = input.Name;
        await db.SaveChangesAsync();
        return Ok(workshop);
    }

    /// <summary>删除车间（车间下有员工或设备时禁止删除）</summary>
    [HttpDelete("workshops/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteWorkshop(int id)
    {
        var workshop = await db.Workshops.FirstOrDefaultAsync(x => x.Id == id);
        if (workshop is null)
            return NotFound(new { message = "车间不存在" });

        var employeeCount = await db.Employees.CountAsync(x => x.WorkshopId == id);
        if (employeeCount > 0)
            return BadRequest(new { message = $"该车间下有 {employeeCount} 名员工，请先调整员工所属车间后再删除" });

        var equipmentCount = await db.Equipments.CountAsync(x => x.WorkshopId == id);
        if (equipmentCount > 0)
            return BadRequest(new { message = $"该车间下有 {equipmentCount} 台设备，请先调整设备所属车间后再删除" });

        db.Workshops.Remove(workshop);
        await db.SaveChangesAsync();
        return Ok(new { message = "已删除" });
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

    /// <summary>新增员工（同车间内工号唯一，默认密码 123456）</summary>
    [HttpPost("employees")]
    [ProducesResponseType(typeof(Employee), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateEmployee([FromBody] Employee employee)
    {
        if (await db.Employees.AnyAsync(x => x.EmployeeNo == employee.EmployeeNo && x.WorkshopId == employee.WorkshopId))
            return BadRequest(new { message = $"车间内工号 {employee.EmployeeNo} 已存在" });

        if (string.IsNullOrEmpty(employee.PasswordHash))
            employee.PasswordHash = PasswordHasher.Hash("123456");

        db.Employees.Add(employee);
        await db.SaveChangesAsync();
        return Ok(employee);
    }

    /// <summary>更新员工（支持重置密码）</summary>
    [HttpPut("employees/{id}")]
    [ProducesResponseType(typeof(Employee), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateEmployee(int id, [FromBody] UpdateEmployeeRequest input)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(x => x.Id == id);
        if (employee is null)
            return NotFound(new { message = "员工不存在" });
        if (await db.Employees.AnyAsync(x => x.EmployeeNo == input.EmployeeNo && x.WorkshopId == input.WorkshopId && x.Id != id))
            return BadRequest(new { message = $"车间内工号 {input.EmployeeNo} 已存在" });
        if (input.WorkshopId <= 0 || !await db.Workshops.AnyAsync(x => x.Id == input.WorkshopId))
            return BadRequest(new { message = "所选车间不存在" });

        employee.EmployeeNo = input.EmployeeNo;
        employee.Name = input.Name;
        employee.Phone = input.Phone;
        employee.WorkshopId = input.WorkshopId;
        employee.Role = input.Role;

        if (!string.IsNullOrEmpty(input.Password))
        {
            if (input.Password.Length < 6)
                return BadRequest(new { message = "密码至少 6 位" });
            employee.PasswordHash = PasswordHasher.Hash(input.Password);
        }

        await db.SaveChangesAsync();
        return Ok(employee);
    }

    /// <summary>删除员工（存在业务数据引用时禁止删除）</summary>
    [HttpDelete("employees/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteEmployee(int id)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(x => x.Id == id);
        if (employee is null)
            return NotFound(new { message = "员工不存在" });

        var reportCount = await db.WorkReports.CountAsync(x => x.EmployeeId == id);
        if (reportCount > 0)
            return BadRequest(new { message = $"该员工有 {reportCount} 条报工记录，不能删除" });

        var qualityCount = await db.QualityRecords.CountAsync(x => x.InspectorId == id);
        if (qualityCount > 0)
            return BadRequest(new { message = $"该员工有 {qualityCount} 条质检记录，不能删除" });

        var timeCount = await db.EquipmentTimeRecords.CountAsync(x => x.EmployeeId == id);
        if (timeCount > 0)
            return BadRequest(new { message = $"该员工有 {timeCount} 条设备时间记录，不能删除" });

        var cardCount = await db.ProcessCards.CountAsync(x => x.CreatedById == id);
        if (cardCount > 0)
            return BadRequest(new { message = $"该员工创建了 {cardCount} 张工艺流转卡，不能删除" });

        var stepOpCount = await db.ProcessCardSteps.CountAsync(x => x.OperatorId == id);
        var stepInspCount = await db.ProcessCardSteps.CountAsync(x => x.InspectorId == id);
        if (stepOpCount > 0 || stepInspCount > 0)
            return BadRequest(new { message = "该员工参与过工序执行，不能删除" });

        var reminderCount = await db.MaintenanceReminders.CountAsync(x => x.CompletedById == id);
        if (reminderCount > 0)
            return BadRequest(new { message = $"该员工完成了 {reminderCount} 条保养提醒，不能删除" });

        db.Employees.Remove(employee);
        await db.SaveChangesAsync();
        return Ok(new { message = "已删除" });
    }

    /// <summary>获取产品列表</summary>
    [HttpGet("products")]
    [ProducesResponseType(typeof(List<Product>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Products() =>
        Ok(await db.Products.OrderBy(x => x.Code).ToListAsync());

    /// <summary>新增产品（未指定车间时默认归属当前用户所属车间）</summary>
    [HttpPost("products")]
    [ProducesResponseType(typeof(Product), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateProduct([FromBody] Product product)
    {
        if (product.WorkshopId <= 0)
            product.WorkshopId = CurrentUserWorkshopId();

        if (await db.Products.AnyAsync(x => x.Code == product.Code && x.WorkshopId == product.WorkshopId))
            return BadRequest(new { message = $"车间内产品编码 {product.Code} 已存在" });

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

    /// <summary>更新产品（含现场照片；未指定车间时保留原车间）</summary>
    [HttpPut("products/{id}")]
    [ProducesResponseType(typeof(Product), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProduct(int id, [FromBody] Product input)
    {
        var product = await db.Products.FirstOrDefaultAsync(x => x.Id == id);
        if (product is null)
            return NotFound(new { message = "产品不存在" });
        if (await db.Products.AnyAsync(x => x.Code == input.Code && x.Id != id && x.WorkshopId == product.WorkshopId))
            return BadRequest(new { message = $"车间内产品编码 {input.Code} 已存在" });

        product.Code = input.Code;
        product.Name = input.Name;
        product.Specification = input.Specification;
        product.QrCode = input.QrCode;
        product.ImageUrl = input.ImageUrl;
        await db.SaveChangesAsync();
        return Ok(product);
    }

    /// <summary>删除产品（存在业务数据引用时禁止删除）</summary>
    [HttpDelete("products/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        var product = await db.Products.FirstOrDefaultAsync(x => x.Id == id);
        if (product is null)
            return NotFound(new { message = "产品不存在" });

        var reportCount = await db.WorkReports.CountAsync(x => x.ProductId == id);
        if (reportCount > 0)
            return BadRequest(new { message = $"该产品有 {reportCount} 条报工记录，不能删除" });

        var qualityCount = await db.QualityRecords.CountAsync(x => x.ProductId == id);
        if (qualityCount > 0)
            return BadRequest(new { message = $"该产品有 {qualityCount} 条质检记录，不能删除" });

        var flowCount = await db.ProcessFlows.CountAsync(x => x.ProductId == id);
        if (flowCount > 0)
            return BadRequest(new { message = $"该产品关联了 {flowCount} 条工艺路线，不能删除" });

        var cardCount = await db.ProcessCards.CountAsync(x => x.ProductId == id);
        if (cardCount > 0)
            return BadRequest(new { message = $"该产品关联了 {cardCount} 张工艺流转卡，不能删除" });

        db.Products.Remove(product);
        await db.SaveChangesAsync();
        return Ok(new { message = "已删除" });
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
        if (equipment.WorkshopId <= 0 || !await db.Workshops.AnyAsync(x => x.Id == equipment.WorkshopId))
            return BadRequest(new { message = "所选车间不存在" });

        if (await db.Equipments.AnyAsync(x => x.Code == equipment.Code && x.WorkshopId == equipment.WorkshopId))
            return BadRequest(new { message = $"车间内设备编码 {equipment.Code} 已存在" });

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

    /// <summary>更新设备（含现场照片）</summary>
    [HttpPut("equipments/{id}")]
    [ProducesResponseType(typeof(Equipment), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateEquipment(int id, [FromBody] Equipment input)
    {
        var equipment = await db.Equipments.FirstOrDefaultAsync(x => x.Id == id);
        if (equipment is null)
            return NotFound(new { message = "设备不存在" });
        if (await db.Equipments.AnyAsync(x => x.Code == input.Code && x.WorkshopId == input.WorkshopId && x.Id != id))
            return BadRequest(new { message = $"车间内设备编码 {input.Code} 已存在" });
        if (input.WorkshopId <= 0 || !await db.Workshops.AnyAsync(x => x.Id == input.WorkshopId))
            return BadRequest(new { message = "所选车间不存在" });

        equipment.Code = input.Code;
        equipment.Name = input.Name;
        equipment.WorkshopId = input.WorkshopId;
        equipment.Status = input.Status;
        equipment.QrCode = input.QrCode;
        equipment.ImageUrl = input.ImageUrl;
        await db.SaveChangesAsync();
        return Ok(equipment);
    }

    /// <summary>删除设备（存在业务数据引用时禁止删除）</summary>
    [HttpDelete("equipments/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteEquipment(int id)
    {
        var equipment = await db.Equipments.FirstOrDefaultAsync(x => x.Id == id);
        if (equipment is null)
            return NotFound(new { message = "设备不存在" });

        var reportCount = await db.WorkReports.CountAsync(x => x.EquipmentId == id);
        if (reportCount > 0)
            return BadRequest(new { message = $"该设备有 {reportCount} 条报工记录，不能删除" });

        var timeCount = await db.EquipmentTimeRecords.CountAsync(x => x.EquipmentId == id);
        if (timeCount > 0)
            return BadRequest(new { message = $"该设备有 {timeCount} 条设备时间记录，不能删除" });

        var planCount = await db.MaintenancePlans.CountAsync(x => x.EquipmentId == id);
        if (planCount > 0)
            return BadRequest(new { message = $"该设备关联了 {planCount} 条保养计划，不能删除" });

        var reminderCount = await db.MaintenanceReminders.CountAsync(x => x.EquipmentId == id);
        if (reminderCount > 0)
            return BadRequest(new { message = $"该设备关联了 {reminderCount} 条保养提醒，不能删除" });

        var qualityCount = await db.QualityRecords.CountAsync(x => x.EquipmentId == id);
        if (qualityCount > 0)
            return BadRequest(new { message = $"该设备有 {qualityCount} 条质检记录，不能删除" });

        db.Equipments.Remove(equipment);
        await db.SaveChangesAsync();
        return Ok(new { message = "已删除" });
    }
}
