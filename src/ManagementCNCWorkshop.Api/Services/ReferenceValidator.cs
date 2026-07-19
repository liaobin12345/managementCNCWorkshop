using ManagementCNCWorkshop.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace ManagementCNCWorkshop.Api.Services;

/// <summary>提交前校验外键 ID 是否存在，避免 500 外键错误</summary>
public static class ReferenceValidator
{
    public static async Task<string?> ValidateWorkReportAsync(
        AppDbContext db, int productId, int workshopId, int employeeId, int? equipmentId)
    {
        if (!await db.Products.AnyAsync(x => x.Id == productId))
            return $"产品 ID {productId} 不存在，请先调用 GET /api/master/products 查看可用 ID";

        if (!await db.Workshops.AnyAsync(x => x.Id == workshopId))
            return $"车间 ID {workshopId} 不存在，请先调用 GET /api/master/workshops 查看可用 ID";

        if (!await db.Employees.AnyAsync(x => x.Id == employeeId))
            return $"员工 ID {employeeId} 不存在，请先调用 GET /api/master/employees 查看可用 ID";

        if (equipmentId.HasValue && !await db.Equipments.AnyAsync(x => x.Id == equipmentId))
            return $"设备 ID {equipmentId} 不存在，请先调用 GET /api/master/equipments 查看可用 ID，或不传 equipmentId";

        return null;
    }

    public static async Task<string?> ValidateQualityRecordAsync(
        AppDbContext db, int productId, int workshopId, int inspectorId)
    {
        if (!await db.Products.AnyAsync(x => x.Id == productId))
            return $"产品 ID {productId} 不存在";

        if (!await db.Workshops.AnyAsync(x => x.Id == workshopId))
            return $"车间 ID {workshopId} 不存在";

        if (!await db.Employees.AnyAsync(x => x.Id == inspectorId))
            return $"质检员 ID {inspectorId} 不存在";

        return null;
    }
}
