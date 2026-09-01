using System.Security.Claims;
using ManagementCNCWorkshop.Api.Data;
using ManagementCNCWorkshop.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ManagementCNCWorkshop.Api.Controllers.Worker;

/// <summary>工人端设备点检：编程技术员每日点检设备，并查看点检报表</summary>
[ApiController]
[Route("api/worker/equipment-inspections")]
[Authorize(Roles = "Worker,Inspector,Programmer,Admin")]
[Tags("工人端-设备点检")]
public class WorkerEquipmentInspectionController(AppDbContext db) : ControllerBase
{
    private int CurrentEmployeeId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>点检表模板（固定 11 项，与现场纸质点检表一致）</summary>
    [HttpGet("template")]
    [ProducesResponseType(typeof(List<object>), StatusCodes.Status200OK)]
    public IActionResult Template() =>
        Ok(EquipmentInspectionTemplate.Items.Select(x => new { itemNo = x.No, itemName = x.Name }));

    /// <summary>查询点检记录（可按设备、月份、班次过滤，默认只看本月）</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<EquipmentInspection>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int? equipmentId,
        [FromQuery] string? yearMonth,
        [FromQuery] string? shift,
        [FromQuery] DateTime? date)
    {
        var q = db.EquipmentInspections
            .Include(x => x.Equipment)
            .Include(x => x.Inspector)
            .Include(x => x.Items)
            .AsQueryable();

        if (equipmentId.HasValue) q = q.Where(x => x.EquipmentId == equipmentId);
        if (!string.IsNullOrWhiteSpace(shift)) q = q.Where(x => x.Shift == shift);
        if (date.HasValue) q = q.Where(x => x.InspectDate == date.Value.Date);
        else if (!string.IsNullOrWhiteSpace(yearMonth))
        {
            if (DateTime.TryParseExact(yearMonth, "yyyy-MM", null,
                    System.Globalization.DateTimeStyles.None, out var ym))
                q = q.Where(x => x.InspectDate.Year == ym.Year && x.InspectDate.Month == ym.Month);
        }

        var list = await q.OrderByDescending(x => x.InspectDate)
            .ThenByDescending(x => x.CreatedAt)
            .Take(200)
            .ToListAsync();
        return Ok(list);
    }

    /// <summary>点检记录详情（含全部明细项）</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(EquipmentInspection), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Detail(int id)
    {
        var record = await db.EquipmentInspections
            .Include(x => x.Equipment)
            .Include(x => x.Inspector)
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (record is null) return NotFound(new { message = "点检记录不存在" });
        return Ok(record);
    }

    /// <summary>提交/修改点检记录（同一设备同一天同一班次只保留一条，重复提交即更新）</summary>
    /// <remarks>点检填报由编程技术员负责，提交权限仅限 Programmer / Admin。</remarks>
    [HttpPost]
    [Authorize(Roles = "Programmer,Admin")]
    [ProducesResponseType(typeof(EquipmentInspection), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Submit([FromBody] InspectionSubmitRequest req)
    {
        var equipment = await db.Equipments.FirstOrDefaultAsync(x => x.Id == req.EquipmentId);
        if (equipment is null) return NotFound(new { message = "设备不存在" });

        if (!DateTime.TryParse(req.InspectDate, out var date) || date == default)
            return BadRequest(new { message = "点检日期无效" });
        date = date.Date;
        if (date > DateTime.Today)
            return BadRequest(new { message = "不能点检未来日期" });

        var shift = req.Shift == "Night" ? "Night" : "Day";
        if (date < DateTime.Today.AddDays(-365))
            return BadRequest(new { message = "点检日期过早" });

        var template = EquipmentInspectionTemplate.Items;
        var statusMap = new HashSet<string> { "Ok", "Abnormal", "Stopped", "Rest" };
        var submitted = req.Items?
            .Where(x => statusMap.Contains(x.Status))
            .ToDictionary(x => x.ItemNo, x => x.Status) ?? new Dictionary<int, string>();

        // 同一设备同一天同班次只保留一条（重复提交即更新）
        var record = await db.EquipmentInspections
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.EquipmentId == equipment.Id && x.InspectDate == date && x.Shift == shift);

        if (record is null)
        {
            record = new EquipmentInspection
            {
                EquipmentId = equipment.Id,
                InspectDate = date,
                Shift = shift,
                InspectorId = CurrentEmployeeId,
                CreatedAt = DateTime.UtcNow,
                Items = new List<EquipmentInspectionItem>(),
            };
            db.EquipmentInspections.Add(record);
        }
        else
        {
            record.InspectorId = CurrentEmployeeId;
        }

        record.AbnormalNote = req.AbnormalNote;
        record.Remark = req.Remark;
        record.UpdatedAt = DateTime.UtcNow;

        // 全量重建明细项（顺序与模板一致，未提交的项默认"正常"）
        record.Items.Clear();
        foreach (var (no, name) in template)
        {
            record.Items.Add(new EquipmentInspectionItem
            {
                ItemNo = no,
                ItemName = name,
                Status = submitted.TryGetValue(no, out var s) ? s : "Ok",
            });
        }

        await db.SaveChangesAsync();

        await db.Entry(record).Reference(x => x.Equipment).LoadAsync();
        await db.Entry(record).Reference(x => x.Inspector).LoadAsync();
        await db.Entry(record).Collection(x => x.Items).LoadAsync();
        return Ok(record);
    }

    /// <summary>月度点检报表：11 项 × 当月每天，按班次汇总（仿纸质点检表）</summary>
    /// <remarks>statuses 数组长度为当月天数，索引即日号-1；空串表示当日未点检。</remarks>
    [HttpGet("report")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> Report(
        [FromQuery] int? equipmentId,
        [FromQuery] string? yearMonth,
        [FromQuery] string? shift)
    {
        var now = DateTime.Now;
        var ym = now.ToString("yyyy-MM");
        if (!string.IsNullOrWhiteSpace(yearMonth)
            && DateTime.TryParseExact(yearMonth, "yyyy-MM", null,
                System.Globalization.DateTimeStyles.None, out var parsed))
            ym = parsed.ToString("yyyy-MM");
        var year = int.Parse(ym[..4]);
        var month = int.Parse(ym[5..]);
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var shiftFilter = shift == "Night" ? "Night" : "Day";

        var query = db.EquipmentInspections
            .Include(x => x.Inspector)
            .Include(x => x.Items)
            .Where(x => x.InspectDate.Year == year && x.InspectDate.Month == month && x.Shift == shiftFilter);
        if (equipmentId.HasValue)
            query = query.Where(x => x.EquipmentId == equipmentId.Value);

        var records = await query.OrderBy(x => x.InspectDate).ToListAsync();

        var items = new List<object>();
        foreach (var (no, name) in EquipmentInspectionTemplate.Items)
        {
            var days = Enumerable.Repeat(string.Empty, daysInMonth).ToArray();
            foreach (var r in records)
            {
                var day = r.InspectDate.Day - 1;
                if (day < 0 || day >= daysInMonth) continue;
                var item = r.Items.FirstOrDefault(x => x.ItemNo == no);
                if (item is not null) days[day] = item.Status;
            }
            items.Add(new { itemNo = no, itemName = name, days });
        }

        var abnormalRecords = records
            .Where(x => x.Items.Any(i => i.Status == "Abnormal") || !string.IsNullOrWhiteSpace(x.AbnormalNote))
            .Select(x => new
            {
                x.Id,
                x.InspectDate,
                inspector = x.Inspector?.Name,
                x.AbnormalNote,
                x.Remark,
                abnormalItems = x.Items.Where(i => i.Status == "Abnormal")
                    .Select(i => new { i.ItemNo, i.ItemName }),
            })
            .ToList();

        return Ok(new
        {
            equipmentId = equipmentId,
            yearMonth = ym,
            daysInMonth,
            shift = shiftFilter,
            checkedDays = records.Count,
            items,
            abnormalRecords,
        });
    }

    /// <summary>提交请求体</summary>
    public class InspectionSubmitRequest
    {
        /// <summary>设备 ID</summary>
        public int EquipmentId { get; set; }

        /// <summary>点检日期 yyyy-MM-dd</summary>
        public string InspectDate { get; set; } = string.Empty;

        /// <summary>班次：Day / Night</summary>
        public string Shift { get; set; } = "Day";

        /// <summary>明细项列表（可只提交有变化的项，其余默认正常）</summary>
        public List<InspectionItemRequest>? Items { get; set; }

        /// <summary>异常记录</summary>
        public string? AbnormalNote { get; set; }

        /// <summary>备注</summary>
        public string? Remark { get; set; }
    }

    /// <summary>明细项请求体</summary>
    public class InspectionItemRequest
    {
        /// <summary>项序号 1~11</summary>
        public int ItemNo { get; set; }

        /// <summary>Ok / Abnormal / Stopped / Rest</summary>
        public string Status { get; set; } = "Ok";
    }
}
