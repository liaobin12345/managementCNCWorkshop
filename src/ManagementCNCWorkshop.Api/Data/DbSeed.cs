using ManagementCNCWorkshop.Api.Models;

namespace ManagementCNCWorkshop.Api.Data;

public static class DbSeed
{
    public static void Seed(AppDbContext db)
    {
        if (db.Workshops.Any())
            return;

        var workshop = new Workshop { Code = "WS01", Name = "CNC一车间" };
        db.Workshops.Add(workshop);
        db.SaveChanges();

        db.Employees.AddRange(
            new Employee { EmployeeNo = "E001", Name = "张三", Phone = "13800000001", WorkshopId = workshop.Id },
            new Employee { EmployeeNo = "E002", Name = "李四", Phone = "13800000002", WorkshopId = workshop.Id }
        );

        db.Products.AddRange(
            new Product { Code = "P001", Name = "轴承座", Specification = "Φ50×80", QrCode = "PROD:P001" },
            new Product { Code = "P002", Name = "法兰盘", Specification = "DN100", QrCode = "PROD:P002" }
        );

        var equipment = new Equipment
        {
            Code = "CNC-01",
            Name = "五轴加工中心",
            WorkshopId = workshop.Id,
            Status = "Running",
            QrCode = "EQ:CNC-01",
            LastMaintenanceDate = DateTime.UtcNow.AddDays(-25)
        };
        db.Equipments.Add(equipment);
        db.SaveChanges();

        db.MaintenancePlans.Add(new MaintenancePlan
        {
            EquipmentId = equipment.Id,
            PlanName = "月度润滑保养",
            CycleDays = 30,
            NextDueDate = DateTime.UtcNow.AddDays(5),
            RemindDaysBefore = 3,
            Content = "检查润滑油、清洁导轨、紧固刀具"
        });

        db.SaveChanges();

        Console.WriteLine("[演示数据] 可用 ID：Workshop=1, Employee=1/2, Product=1/2, Equipment=1");
    }
}
