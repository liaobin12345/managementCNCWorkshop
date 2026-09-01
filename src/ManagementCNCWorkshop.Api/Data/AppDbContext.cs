using ManagementCNCWorkshop.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ManagementCNCWorkshop.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Workshop> Workshops => Set<Workshop>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<WorkReport> WorkReports => Set<WorkReport>();
    public DbSet<QualityRecord> QualityRecords => Set<QualityRecord>();
    public DbSet<Equipment> Equipments => Set<Equipment>();
    public DbSet<EquipmentTimeRecord> EquipmentTimeRecords => Set<EquipmentTimeRecord>();
    public DbSet<MaintenancePlan> MaintenancePlans => Set<MaintenancePlan>();
    public DbSet<MaintenanceReminder> MaintenanceReminders => Set<MaintenanceReminder>();
    public DbSet<ProcessFlow> ProcessFlows => Set<ProcessFlow>();
    public DbSet<ProcessStep> ProcessSteps => Set<ProcessStep>();
    public DbSet<ProcessCard> ProcessCards => Set<ProcessCard>();
    public DbSet<ProcessCardStep> ProcessCardSteps => Set<ProcessCardStep>();
    public DbSet<EquipmentInspection> EquipmentInspections => Set<EquipmentInspection>();
    public DbSet<EquipmentInspectionItem> EquipmentInspectionItems => Set<EquipmentInspectionItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Workshop>().HasIndex(x => x.Code).IsUnique();

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.HasIndex(x => x.EmployeeNo).IsUnique();
            entity.HasOne(x => x.Workshop).WithMany().HasForeignKey(x => x.WorkshopId);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => x.QrCode);
        });

        modelBuilder.Entity<Equipment>(entity =>
        {
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => x.QrCode);
            entity.HasOne(x => x.Workshop).WithMany().HasForeignKey(x => x.WorkshopId);
        });

        modelBuilder.Entity<EquipmentTimeRecord>(entity =>
        {
            entity.HasIndex(x => new { x.EquipmentId, x.RecordDate }).IsUnique();
            entity.Property(x => x.SetupHours).HasPrecision(10, 2);
            entity.Property(x => x.RunningHours).HasPrecision(10, 2);
            entity.Property(x => x.IdleHours).HasPrecision(10, 2);
            entity.HasOne(x => x.Equipment).WithMany().HasForeignKey(x => x.EquipmentId);
            entity.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).IsRequired(false);
        });

        modelBuilder.Entity<WorkReport>(entity =>
        {
            entity.HasIndex(x => x.ReportDate);
            entity.Property(x => x.Quantity).HasPrecision(18, 4);
            entity.Property(x => x.QualifiedQty).HasPrecision(18, 4);
            entity.Property(x => x.DefectQty).HasPrecision(18, 4);
            entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
            entity.HasOne(x => x.Workshop).WithMany().HasForeignKey(x => x.WorkshopId);
            entity.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId);
            entity.HasOne(x => x.Equipment).WithMany().HasForeignKey(x => x.EquipmentId).IsRequired(false);
        });

        modelBuilder.Entity<QualityRecord>(entity =>
        {
            entity.HasIndex(x => x.RecordDate);
            entity.Property(x => x.SampleQty).HasPrecision(18, 4);
            entity.Property(x => x.QualifiedQty).HasPrecision(18, 4);
            entity.Property(x => x.DefectQty).HasPrecision(18, 4);
            entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
            entity.HasOne(x => x.Workshop).WithMany().HasForeignKey(x => x.WorkshopId);
            entity.HasOne(x => x.Inspector).WithMany().HasForeignKey(x => x.InspectorId);
            entity.HasOne(x => x.ProcessCard).WithMany().HasForeignKey(x => x.ProcessCardId).IsRequired(false);
            entity.HasOne(x => x.Equipment).WithMany().HasForeignKey(x => x.EquipmentId).IsRequired(false);
        });

        modelBuilder.Entity<MaintenancePlan>(entity =>
        {
            entity.HasOne(x => x.Equipment).WithMany().HasForeignKey(x => x.EquipmentId);
        });

        modelBuilder.Entity<MaintenanceReminder>(entity =>
        {
            entity.HasIndex(x => x.Status);
            entity.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId);
            entity.HasOne(x => x.Equipment).WithMany().HasForeignKey(x => x.EquipmentId);
        });

        modelBuilder.Entity<ProcessFlow>(entity =>
        {
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => x.Status);
            entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
            entity.HasOne(x => x.Creator).WithMany().HasForeignKey(x => x.CreatedById).IsRequired(false);
        });

        modelBuilder.Entity<ProcessStep>(entity =>
        {
            entity.HasIndex(x => new { x.ProcessFlowId, x.StepNo });
            entity.HasOne(x => x.ProcessFlow).WithMany(x => x.Steps).HasForeignKey(x => x.ProcessFlowId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Equipment).WithMany().HasForeignKey(x => x.EquipmentId).IsRequired(false);
        });

        modelBuilder.Entity<ProcessCard>(entity =>
        {
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => x.Status);
            entity.Property(x => x.Quantity).HasPrecision(18, 4);
            entity.HasOne(x => x.ProcessFlow).WithMany().HasForeignKey(x => x.ProcessFlowId);
            entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
            entity.HasOne(x => x.Creator).WithMany().HasForeignKey(x => x.CreatedById).IsRequired(false);
        });

        modelBuilder.Entity<ProcessCardStep>(entity =>
        {
            entity.HasIndex(x => new { x.ProcessCardId, x.StepNo });
            entity.Property(x => x.Quantity).HasPrecision(18, 4);
            entity.HasOne(x => x.ProcessCard).WithMany(x => x.CardSteps).HasForeignKey(x => x.ProcessCardId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Operator).WithMany().HasForeignKey(x => x.OperatorId).IsRequired(false);
            entity.HasOne(x => x.Inspector).WithMany().HasForeignKey(x => x.InspectorId).IsRequired(false);
        });

        modelBuilder.Entity<EquipmentInspection>(entity =>
        {
            entity.HasIndex(x => new { x.EquipmentId, x.InspectDate, x.Shift }).IsUnique();
            entity.HasIndex(x => x.InspectDate);
            entity.HasOne(x => x.Equipment).WithMany().HasForeignKey(x => x.EquipmentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Inspector).WithMany().HasForeignKey(x => x.InspectorId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<EquipmentInspectionItem>(entity =>
        {
            entity.HasIndex(x => new { x.InspectionId, x.ItemNo });
            entity.HasOne(x => x.Inspection).WithMany(x => x.Items).HasForeignKey(x => x.InspectionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
