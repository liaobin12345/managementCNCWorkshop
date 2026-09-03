using System.Security.Claims;

namespace ManagementCNCWorkshop.Api.Services;

/// <summary>
/// 当前请求的租户上下文：从 JWT 中的 WorkshopId 声明解析。
/// 值为 null 时（未登录/登录流程/系统任务），全局查询过滤器不生效，
/// 用于登录查询与数据种子等系统级操作。
/// </summary>
public class TenantContext
{
    private const string WorkshopIdClaimType = "WorkshopId";

    /// <summary>当前车间 ID（租户 ID），未登录或系统级操作为 null</summary>
    public int? WorkshopId { get; private set; }

    /// <summary>是否有明确的租户上下文</summary>
    public bool HasTenant => WorkshopId.HasValue;

    /// <summary>从当前用户声明刷新租户上下文</summary>
    /// <remarks>
    /// Admin 角色是平台运营者（跨车间管理所有租户，用于开通车间/分配账号），不参与租户过滤；
    /// Worker/Inspector/Programmer 等车间用户严格按自己的车间隔离。
    /// </remarks>
    public void LoadFromClaims(ClaimsPrincipal? user)
    {
        WorkshopId = null;
        if (user is null) return;

        if (user.IsInRole("Admin"))
            return;

        var raw = user.FindFirstValue(WorkshopIdClaimType);
        if (int.TryParse(raw, out var workshopId) && workshopId > 0)
            WorkshopId = workshopId;
    }

    /// <summary>直接设置租户（供系统级/测试场景）</summary>
    public void SetWorkshopId(int? workshopId) => WorkshopId = workshopId > 0 ? workshopId : null;
}
