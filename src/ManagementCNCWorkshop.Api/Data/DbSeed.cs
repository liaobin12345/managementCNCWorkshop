using ManagementCNCWorkshop.Api.Models;
using ManagementCNCWorkshop.Api.Services;

namespace ManagementCNCWorkshop.Api.Data;

public static class DbSeed
{
    public static void Seed(AppDbContext db)
    {
        if (!db.Workshops.Any())
        {
            var workshop = new Workshop { Code = "WS01", Name = "CNC一车间" };
            db.Workshops.Add(workshop);
            db.SaveChanges();

            db.Employees.AddRange(
                new Employee
                {
                    EmployeeNo = "E001", Name = "张三", Phone = "13800000001",
                    WorkshopId = workshop.Id, Role = "Worker",
                    PasswordHash = PasswordHasher.Hash("123456")
                },
                new Employee
                {
                    EmployeeNo = "E002", Name = "李四", Phone = "13800000002",
                    WorkshopId = workshop.Id, Role = "Worker",
                    PasswordHash = PasswordHasher.Hash("123456")
                },
                new Employee
                {
                    EmployeeNo = "E003", Name = "王五", Phone = "13800000003",
                    WorkshopId = workshop.Id, Role = "Inspector",
                    PasswordHash = PasswordHasher.Hash("123456")
                },
                new Employee
                {
                    EmployeeNo = "E900", Name = "管理员", Phone = "13800000099",
                    WorkshopId = workshop.Id, Role = "Admin",
                    PasswordHash = PasswordHasher.Hash("admin123")
                }
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

            Console.WriteLine("[演示数据] 账号：管理员 E900/admin123，工人 E001~E002/123456，质检员 E003/123456");
            Console.WriteLine("[演示数据] 可用 ID：Workshop=1, Employee=1/2/3/4, Product=1/2, Equipment=1");
        }

        SeedIncremental(db);
    }

    /// <summary>增量种子：已存在的数据库也会执行（新角色/演示设备时间记录等）</summary>
    private static void SeedIncremental(AppDbContext db)
    {
        // 编程技术员（E004）——已存在则跳过
        if (!db.Employees.Any(x => x.EmployeeNo == "E004"))
        {
            var workshopId = db.Workshops.First().Id;
            db.Employees.Add(new Employee
            {
                EmployeeNo = "E004",
                Name = "赵技术",
                Phone = "13800000004",
                WorkshopId = workshopId,
                Role = "Programmer",
                PasswordHash = PasswordHasher.Hash("123456")
            });
            db.SaveChanges();
            Console.WriteLine("[增量数据] 已添加编程技术员 E004/123456（赵技术）");
        }

        // 演示设备时间记录（仅当没有任何记录时填充最近 3 天）
        if (!db.EquipmentTimeRecords.Any())
        {
            var equipmentId = db.Equipments.First().Id;
            var employeeId = db.Employees.FirstOrDefault(x => x.EmployeeNo == "E004")?.Id;
            var today = DateTime.UtcNow.Date;

            var demo = new[]
            {
                new EquipmentTimeRecord { EquipmentId = equipmentId, RecordDate = today.AddDays(-2), SetupHours = 1.5m, RunningHours = 6m, IdleHours = 1m, EmployeeId = employeeId, Remark = "新程序首件调试" },
                new EquipmentTimeRecord { EquipmentId = equipmentId, RecordDate = today.AddDays(-1), SetupHours = 0.5m, RunningHours = 7m, IdleHours = 0.5m, EmployeeId = employeeId, Remark = "正常生产" },
                new EquipmentTimeRecord { EquipmentId = equipmentId, RecordDate = today, SetupHours = 0m, RunningHours = 3m, IdleHours = 1m, EmployeeId = employeeId, Remark = "上午班次" }
            };
            db.EquipmentTimeRecords.AddRange(demo);
            db.SaveChanges();
            Console.WriteLine("[增量数据] 已添加演示设备时间记录（最近 3 天）");
        }
    }
}
