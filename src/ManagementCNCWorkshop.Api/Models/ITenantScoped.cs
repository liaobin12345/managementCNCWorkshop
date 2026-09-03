namespace ManagementCNCWorkshop.Api.Models;

/// <summary>需要按车间进行数据隔离的实体</summary>
public interface ITenantScoped
{
    /// <summary>所属车间 ID</summary>
    int WorkshopId { get; set; }
}
