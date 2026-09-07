using ManagementCNCWorkshop.Api.Data;
using ManagementCNCWorkshop.Api.Models;
using ManagementCNCWorkshop.Api.Models.Dtos;
using ManagementCNCWorkshop.Api.Services;
using ManagementCNCWorkshop.Api.Services.Programming;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ManagementCNCWorkshop.Api.Controllers.Programming;

/// <summary>
/// 编程助手：机床配置 + 加工程序 + 轮廓坐标点管理。
/// 由数控车编程助手小程序调用（登录用户均可使用，按车间数据隔离）。
/// </summary>
[ApiController]
[Route("api/programming")]
[Authorize(Roles = "Worker,Inspector,Programmer,Admin")]
[Tags("编程助手")]
public class ProgrammingController(AppDbContext db, TenantContext tenant, FanucProgramGenerator generator, DxfContourParser dxfParser) : ControllerBase
{
    private int CurrentEmployeeId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    private bool IsAdmin => User.IsInRole("Admin");

    /// <summary>当前有效车间 ID（Admin 跨车间时为 null）</summary>
    private int? WorkshopId => tenant.HasTenant ? tenant.WorkshopId : null;

    // ─────────────────────────── 材料库 ───────────────────────────

    /// <summary>常用材料列表（含行业经验推荐切削参数，供前端下拉选择与参数预览）</summary>
    [HttpGet("materials")]
    [ProducesResponseType(typeof(List<CncMaterialDto>), StatusCodes.Status200OK)]
    public IActionResult ListMaterials()
    {
        var list = CncMaterialDefaults.All.Select(m => new CncMaterialDto
        {
            Id = m.Id,
            Name = m.Name,
            Category = m.Category,
            VcRough = m.VcRough,
            VcFinish = m.VcFinish,
            FeedRough = m.FeedRough,
            FeedFinish = m.FeedFinish,
            FeedFace = m.FeedFace,
            FeedGroove = m.FeedGroove,
            ApRough = m.ApRough,
            Remark = m.Remark,
        }).ToList();
        return Ok(list);
    }

    // ─────────────────────────── 机床 ───────────────────────────

    /// <summary>机床列表</summary>
    [HttpGet("machines")]
    [ProducesResponseType(typeof(List<CncMachineDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListMachines([FromQuery] bool? active = null)
    {
        var q = db.CncMachines.AsNoTracking().AsQueryable();
        if (active.HasValue) q = q.Where(x => x.IsActive == active.Value);

        var list = await q.OrderBy(x => x.Id).ToListAsync();
        return Ok(list.Select(x => new CncMachineDto
        {
            Id = x.Id,
            Code = x.Code,
            Name = x.Name,
            ControlSystem = x.ControlSystem,
            IsActive = x.IsActive,
            NetworkAddress = x.NetworkAddress,
            Remark = x.Remark,
        }).ToList());
    }

    /// <summary>新增机床</summary>
    [HttpPost("machines")]
    [ProducesResponseType(typeof(CncMachineDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateMachine([FromBody] CncMachineInput input)
    {
        var wid = WorkshopId ?? input.WorkshopId ?? 0;
        if (wid <= 0) return BadRequest(new { message = "缺少车间（WorkshopId）" });

        if (string.IsNullOrWhiteSpace(input.Code))
            return BadRequest(new { message = "机床编码不能为空" });
        if (string.IsNullOrWhiteSpace(input.ControlSystem))
            return BadRequest(new { message = "请选择数控系统" });

        var dup = await db.CncMachines.AnyAsync(x => x.WorkshopId == wid && x.Code == input.Code.Trim());
        if (dup) return BadRequest(new { message = $"机床编码 {input.Code} 已存在" });

        var machine = new CncMachine
        {
            WorkshopId = wid,
            Code = input.Code.Trim(),
            Name = input.Name.Trim(),
            ControlSystem = input.ControlSystem.Trim(),
            IsActive = input.IsActive,
            NetworkAddress = input.NetworkAddress,
            Remark = input.Remark,
        };
        db.CncMachines.Add(machine);
        await db.SaveChangesAsync();

        return Ok(ToMachineDto(machine));
    }

    /// <summary>编辑机床</summary>
    [HttpPut("machines/{id:int}")]
    [ProducesResponseType(typeof(CncMachineDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateMachine(int id, [FromBody] CncMachineInput input)
    {
        var machine = await db.CncMachines.FirstOrDefaultAsync(x => x.Id == id);
        if (machine is null) return NotFound(new { message = "机床不存在" });

        if (string.IsNullOrWhiteSpace(input.Code))
            return BadRequest(new { message = "机床编码不能为空" });
        if (string.IsNullOrWhiteSpace(input.ControlSystem))
            return BadRequest(new { message = "请选择数控系统" });

        var dup = await db.CncMachines.AnyAsync(x => x.WorkshopId == machine.WorkshopId && x.Code == input.Code.Trim() && x.Id != id);
        if (dup) return BadRequest(new { message = $"机床编码 {input.Code} 已存在" });

        machine.Code = input.Code.Trim();
        machine.Name = input.Name.Trim();
        machine.ControlSystem = input.ControlSystem.Trim();
        machine.IsActive = input.IsActive;
        machine.NetworkAddress = input.NetworkAddress;
        machine.Remark = input.Remark;
        await db.SaveChangesAsync();

        return Ok(ToMachineDto(machine));
    }

    /// <summary>删除机床</summary>
    [HttpDelete("machines/{id:int}")]
    public async Task<IActionResult> DeleteMachine(int id)
    {
        var machine = await db.CncMachines.FirstOrDefaultAsync(x => x.Id == id);
        if (machine is null) return NotFound(new { message = "机床不存在" });

        db.CncMachines.Remove(machine);
        await db.SaveChangesAsync();
        return Ok(new { message = "已删除" });
    }

    // ─────────────────────────── 程序 ───────────────────────────

    /// <summary>程序列表（mine=1 只看自己的；默认看本车间全部）</summary>
    [HttpGet("programs")]
    [ProducesResponseType(typeof(List<CncProgramDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListPrograms([FromQuery] bool mine = false, [FromQuery] string? status = null)
    {
        var q = db.CncPrograms
            .Include(x => x.Creator)
            .AsNoTracking().AsQueryable();

        if (mine) q = q.Where(x => x.CreatedById == CurrentEmployeeId);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.Status == status);

        var list = await q.OrderByDescending(x => x.UpdatedAt).Take(100).ToListAsync();
        return Ok(list.Select(x => ToProgramDto(x, includePoints: false)).ToList());
    }

    /// <summary>程序详情（含轮廓坐标点）</summary>
    [HttpGet("programs/{id:int}")]
    [ProducesResponseType(typeof(CncProgramDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProgram(int id)
    {
        var program = await db.CncPrograms
            .Include(x => x.Creator)
            .Include(x => x.ContourPoints)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (program is null) return NotFound(new { message = "程序不存在" });
        return Ok(ToProgramDto(program, includePoints: true));
    }

    /// <summary>新建程序（可携带轮廓点与工艺参数）</summary>
    [HttpPost("programs")]
    [ProducesResponseType(typeof(CncProgramDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateProgram([FromBody] CncProgramInput input)
    {
        var wid = WorkshopId ?? 0;
        if (wid <= 0) return BadRequest(new { message = "请登录车间账号后使用" });
        if (string.IsNullOrWhiteSpace(input.PartName))
            return BadRequest(new { message = "零件名称不能为空" });

        var program = new CncProgram
        {
            WorkshopId = wid,
            PartName = input.PartName.Trim(),
            DrawingNo = input.DrawingNo,
            Material = input.Material,
            Datum = string.IsNullOrWhiteSpace(input.Datum) ? "right_face" : input.Datum,
            StockDia = input.StockDia,
            StockLen = input.StockLen,
            RoughAllowance = input.RoughAllowance,
            PerCutDepth = input.PerCutDepth,
            Feed = input.Feed,
            Rpm = input.Rpm,
            ToolNo = input.ToolNo,
            ToolTipR = input.ToolTipR,
            TipPos = input.TipPos,
            GrooveWidth = input.GrooveWidth,
            ChamferC = input.ChamferC,
            ProcessMode = input.ProcessMode,
            ToolPost = input.ToolPost,
            ControlSystem = input.ControlSystem,
            MachineNo = input.MachineNo,
            Source = string.IsNullOrWhiteSpace(input.Source) ? "manual" : input.Source,
            Status = "Draft",
            Step = 1,
            CreatedById = CurrentEmployeeId,
        };

        if (input.Points is { Count: > 0 })
        {
            var seq = 1;
            foreach (var p in input.Points.OrderBy(x => x.Seq > 0 ? x.Seq : int.MaxValue))
            {
                program.ContourPoints.Add(new CncContourPoint
                {
                    WorkshopId = wid,
                    Seq = p.Seq > 0 ? p.Seq : seq,
                    Type = string.IsNullOrWhiteSpace(p.Type) ? "step" : p.Type,
                    X = p.X,
                    Z = p.Z,
                    ArcR = p.ArcR,
                    ArcDir = p.ArcDir,
                    Chamfer = p.Chamfer,
                    ThreadPitch = p.ThreadPitch,
                    Note = p.Note,
                    Confidence = 1m,
                    Verified = p.Verified,
                    Manual = true,
                    Source = "manual",
                });
                seq++;
            }
            program.Step = 2;
        }

        db.CncPrograms.Add(program);
        await db.SaveChangesAsync();

        return Ok(ToProgramDto(program, includePoints: true));
    }

    /// <summary>更新程序基础信息（零件/参数）</summary>
    [HttpPut("programs/{id:int}")]
    [ProducesResponseType(typeof(CncProgramDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateProgram(int id, [FromBody] CncProgramInput input)
    {
        var program = await db.CncPrograms.FirstOrDefaultAsync(x => x.Id == id);
        if (program is null) return NotFound(new { message = "程序不存在" });
        if (program.CreatedById != CurrentEmployeeId && !IsAdmin)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "只能编辑自己创建的程序" });

        if (!string.IsNullOrWhiteSpace(input.PartName)) program.PartName = input.PartName.Trim();
        program.DrawingNo = input.DrawingNo ?? program.DrawingNo;
        program.Material = input.Material ?? program.Material;
        if (!string.IsNullOrWhiteSpace(input.Datum)) program.Datum = input.Datum;
        // 切削参数：直接赋值（唯一调用方是向导全量保存）。不能 ?? 合并——
        // 否则用户清空参数想交给材料库自动匹配时，旧值永远清不掉
        program.StockDia = input.StockDia;
        program.StockLen = input.StockLen;
        program.RoughAllowance = input.RoughAllowance;
        program.PerCutDepth = input.PerCutDepth;
        program.Feed = input.Feed;
        program.Rpm = input.Rpm;
        program.ToolNo = input.ToolNo;
        program.ToolTipR = input.ToolTipR;
        program.TipPos = input.TipPos;
        program.GrooveWidth = input.GrooveWidth;
        program.ChamferC = input.ChamferC;
        program.ProcessMode = input.ProcessMode ?? program.ProcessMode;
        program.ToolPost = input.ToolPost ?? program.ToolPost;
        program.ControlSystem = input.ControlSystem ?? program.ControlSystem;
        program.MachineNo = input.MachineNo ?? program.MachineNo;
        program.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Ok(ToProgramDto(program, includePoints: false));
    }

    /// <summary>整体替换轮廓坐标点（批量保存）</summary>
    [HttpPut("programs/{id:int}/points")]
    [ProducesResponseType(typeof(CncProgramDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> SavePoints(int id, [FromBody] List<CncContourPointInput> points)
    {
        var program = await db.CncPrograms.FirstOrDefaultAsync(x => x.Id == id);
        if (program is null) return NotFound(new { message = "程序不存在" });
        if (program.CreatedById != CurrentEmployeeId && !IsAdmin)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "只能编辑自己创建的程序" });

        // 先删旧点，再按新顺序写入（整表替换，简单可靠）
        var old = await db.CncContourPoints.Where(x => x.CncProgramId == id).ToListAsync();
        db.CncContourPoints.RemoveRange(old);
        await db.SaveChangesAsync();

        var seq = 1;
        foreach (var p in points.OrderBy(x => x.Seq > 0 ? x.Seq : int.MaxValue))
        {
            db.CncContourPoints.Add(new CncContourPoint
            {
                WorkshopId = program.WorkshopId,
                CncProgramId = id,
                Seq = p.Seq > 0 ? p.Seq : seq,
                Type = string.IsNullOrWhiteSpace(p.Type) ? "step" : p.Type,
                X = p.X,
                Z = p.Z,
                ArcR = p.ArcR,
                ArcDir = p.ArcDir,
                Chamfer = p.Chamfer,
                ThreadPitch = p.ThreadPitch,
                Note = p.Note,
                Confidence = 1m,
                Verified = p.Verified,
                Manual = true,
                Source = "manual",
            });
            seq++;
        }

        program.Step = Math.Max(program.Step, 2);
        program.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var full = await db.CncPrograms.Include(x => x.ContourPoints).FirstAsync(x => x.Id == id);
        return Ok(ToProgramDto(full, includePoints: true));
    }

    /// <summary>确认坐标（Step 完成）→ 状态变 Confirmed</summary>
    [HttpPost("programs/{id:int}/confirm")]
    [ProducesResponseType(typeof(CncProgramDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ConfirmProgram(int id)
    {
        var program = await db.CncPrograms.Include(x => x.ContourPoints).FirstOrDefaultAsync(x => x.Id == id);
        if (program is null) return NotFound(new { message = "程序不存在" });
        if (program.CreatedById != CurrentEmployeeId && !IsAdmin)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "只能确认自己创建的程序" });

        if (program.ContourPoints.Count < 2)
            return BadRequest(new { message = "至少需要 2 个轮廓坐标点才能确认" });

        foreach (var p in program.ContourPoints) p.Verified = true;
        program.Status = "Confirmed";
        program.Step = Math.Max(program.Step, 3);
        program.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(ToProgramDto(program, includePoints: true));
    }

    /// <summary>工艺方案预判：根据轮廓特征推荐一次/两次/多次加工与工步顺序（生成前确认）</summary>
    [HttpGet("programs/{id:int}/process-plan")]
    [ProducesResponseType(typeof(ProcessPlanDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ProcessPlan(int id)
    {
        var program = await db.CncPrograms
            .Include(x => x.ContourPoints)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (program is null) return NotFound(new { message = "程序不存在" });

        var points = program.ContourPoints.OrderBy(x => x.Seq).ToList();
        if (points.Count < 2)
            return BadRequest(new { message = "轮廓坐标点不足，无法预判工艺" });

        var plan = ProcessPlanner.Build(program, points);
        return Ok(plan);
    }

    /// <summary>生成 Fanuc G 代码（G71 粗车 + G70 精车 + 螺纹 G76 初稿）</summary>
    [HttpPost("programs/{id:int}/generate")]
    [ProducesResponseType(typeof(CncProgramDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Generate(int id)
    {
        var program = await db.CncPrograms
            .Include(x => x.ContourPoints)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (program is null) return NotFound(new { message = "程序不存在" });
        if (program.CreatedById != CurrentEmployeeId && !IsAdmin)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "只能对自己创建的程序操作" });

        if (program.Status != "Confirmed")
            return BadRequest(new { message = "请先确认轮廓坐标再生成程序" });
        if (program.StockDia is null or <= 0)
            return BadRequest(new { message = "请填写棒料直径：两次加工调头时需按棒料外圆倒角去毛刺" });

        var points = program.ContourPoints.OrderBy(x => x.Seq).ToList();
        if (points.Count < 2)
            return BadRequest(new { message = "轮廓坐标点不足，无法生成" });

        var parts = generator.GeneratePrograms(program, points).ToList();
        var gcode = string.Join("\n\n", parts);
        program.Gcode = gcode;
        program.Status = "Generated";
        program.Step = Math.Max(program.Step, 4);
        program.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var dto = ToProgramDto(program, includePoints: true);
        dto.GcodeParts = parts;
        return Ok(new
        {
            message = "已生成程序初稿（请核对刀补、圆弧方向与螺纹参数）",
            generated = true,
            program = dto,
        });
    }

    /// <summary>上传 DXF 图纸并解析出轮廓坐标点（外部轮廓，X 轴向 / Y 半径）</summary>
    [HttpPost("dxf-parse")]
    [ProducesResponseType(typeof(DxfParseResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ParseDxf(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "请上传 DXF 文件" });

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var result = dxfParser.Parse(file.FileName, ms.ToArray());
        return Ok(result);
    }

    /// <summary>删除程序</summary>
    [HttpDelete("programs/{id:int}")]
    public async Task<IActionResult> DeleteProgram(int id)
    {
        var program = await db.CncPrograms.FirstOrDefaultAsync(x => x.Id == id);
        if (program is null) return NotFound(new { message = "程序不存在" });
        if (program.CreatedById != CurrentEmployeeId && !IsAdmin)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "只能删除自己创建的程序" });

        db.CncPrograms.Remove(program);
        await db.SaveChangesAsync();
        return Ok(new { message = "已删除" });
    }

    // ─────────────────────────── 辅助 ───────────────────────────

    private static CncMachineDto ToMachineDto(CncMachine m) => new()
    {
        Id = m.Id,
        Code = m.Code,
        Name = m.Name,
        ControlSystem = m.ControlSystem,
        IsActive = m.IsActive,
        NetworkAddress = m.NetworkAddress,
        Remark = m.Remark,
    };

    private static CncProgramDto ToProgramDto(CncProgram p, bool includePoints)
    {
        var dto = new CncProgramDto
        {
            Id = p.Id,
            PartName = p.PartName,
            DrawingNo = p.DrawingNo,
            Material = p.Material,
            Datum = p.Datum,
            StockDia = p.StockDia,
            StockLen = p.StockLen,
            RoughAllowance = p.RoughAllowance,
            PerCutDepth = p.PerCutDepth,
            Feed = p.Feed,
            Rpm = p.Rpm,
            ToolNo = p.ToolNo,
            ToolTipR = p.ToolTipR,
            TipPos = p.TipPos,
            GrooveWidth = p.GrooveWidth,
            ChamferC = p.ChamferC,
            ProcessMode = p.ProcessMode,
            ToolPost = p.ToolPost,
            ControlSystem = p.ControlSystem,
            MachineNo = p.MachineNo,
            Gcode = p.Gcode,
            Source = p.Source,
            Status = p.Status,
            Step = p.Step,
            CreatedById = p.CreatedById,
            CreatorName = p.Creator?.Name,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt,
            PointCount = p.ContourPoints.Count,
        };

        if (includePoints)
        {
            dto.ContourPoints = p.ContourPoints.OrderBy(x => x.Seq)
                .Select(x => new CncContourPointDto
                {
                    Id = x.Id,
                    Seq = x.Seq,
                    Type = x.Type,
                    X = x.X,
                    Z = x.Z,
                    ArcR = x.ArcR,
                    ArcDir = x.ArcDir,
                    Chamfer = x.Chamfer,
                    ThreadPitch = x.ThreadPitch,
                    Note = x.Note,
                    Confidence = x.Confidence,
                    Verified = x.Verified,
                    Manual = x.Manual,
                    Source = x.Source,
                }).ToList();
        }

        return dto;
    }
}
