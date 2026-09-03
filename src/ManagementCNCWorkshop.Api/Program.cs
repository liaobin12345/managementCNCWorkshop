using System.Reflection;
using System.Text;
using System.Text.Json;
using ManagementCNCWorkshop.Api.Data;
using ManagementCNCWorkshop.Api.Models;
using ManagementCNCWorkshop.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

// 确保 wwwroot 目录存在（静态文件服务用）
Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"));

var builder = WebApplication.CreateBuilder(args);

// ========== 数据迁移模式：dotnet run --project src/ManagementCNCWorkshop.Api -- migrate ==========
// 将 SQLite（workshop.db）里的全部数据复制到 MySQL，复制完成后进程退出。
if (args.Contains("migrate"))
{
    await MigrateSqliteToMySqlAsync(builder.Environment.ContentRootPath, builder.Configuration);
    return;
}

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Management CNC Workshop API",
        Version = "v1",
        Description = """
            CNC 数据车间管理系统后端接口（V2 架构拆分版）。

            ## 架构说明
            - **工人端**（`/api/worker/*`）：角色 Worker / Inspector / Programmer，供 UniApp 小程序调用
            - **运营后台**（`/api/admin/*`）：角色 Admin，供 Web 管理端调用
            - **认证**（`/api/auth/*`）：首次通过 POST /api/auth/login 获取 JWT，后续请求发在 Authorization: Bearer 头

            ## 演示账号
            | 角色 | 工号 | 密码 |
            |------|------|------|
            | 管理员 | E900 | admin123 |
            | 编程技术员 | E004 | 123456 |
            | 操作工 | E001 | 123456 |
            | 操作工 | E002 | 123456 |
            | 质检员 | E003 | 123456 |
            """
    });

    // Swagger 支持 JWT Bearer
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "输入登录获取的 JWT 令牌，如：Bearer eyJhbGci..."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
});

// --- 数据库（按 Database:Provider 切换：mysql / sqlite） ---
var dbProvider = builder.Configuration["Database:Provider"] ?? "sqlite";
var connString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("缺少配置 ConnectionStrings:DefaultConnection");
Console.WriteLine($"[数据库] Provider={dbProvider}");

// --- 多租户上下文（从 JWT 的 WorkshopId 声明解析当前车间） ---
builder.Services.AddScoped<TenantContext>();

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (dbProvider.Equals("mysql", StringComparison.OrdinalIgnoreCase))
        options.UseMySql(connString, ServerVersion.AutoDetect(connString));
    else
        options.UseSqlite(connString);
});
// 覆盖默认注册：让 AppDbContext 注入请求级的 TenantContext（覆盖 AddDbContext 默认的无参租户构造）
builder.Services.AddScoped<AppDbContext>(sp => new AppDbContext(
    sp.GetRequiredService<DbContextOptions<AppDbContext>>(),
    sp.GetRequiredService<TenantContext>()));

// --- JWT 认证 ---
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("缺少配置 Jwt:Secret");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });
builder.Services.AddAuthorization();

// --- 注册服务 ---
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddHttpClient<WechatService>();

// --- CORS（供 UniApp 和 Web 管理端调用） ---
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin()));

var app = builder.Build();

// --- 初始化数据库 ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (dbProvider.Equals("mysql", StringComparison.OrdinalIgnoreCase))
        await CreateDatabaseIfNotExistsAsync(connString);
    db.Database.EnsureCreated();

    if (dbProvider.Equals("mysql", StringComparison.OrdinalIgnoreCase))
    {
        // MySQL 补充模型新增的列（EnsureCreated 不会改已有表结构）
        await EnsureMySqlColumnAsync(connString, "Products", "ImageUrl", "longtext NULL");
        await EnsureMySqlColumnAsync(connString, "Equipments", "ImageUrl", "longtext NULL");
        await EnsureMySqlColumnAsync(connString, "WorkReports", "ProcessCardId", "int NULL");
        await EnsureMySqlColumnAsync(connString, "WorkReports", "ProcessStepNo", "int NULL");
        await EnsureMySqlColumnAsync(connString, "WorkReports", "ProcessStepName", "longtext NULL");
        await EnsureMySqlColumnAsync(connString, "Employees", "WeChatOpenId", "varchar(128) NULL");
        await EnsureMySqlEquipmentTimeTableAsync(connString);
        await EnsureMySqlProcessTablesAsync(connString);
        await EnsureMySqlProcessCardColumnsAsync(connString);
        await EnsureMySqlQualityProcessColumnsAsync(connString);
        await EnsureMySqlColumnAsync(connString, "QualityRecords", "EquipmentId", "int NULL");
        await EnsureMySqlColumnAsync(connString, "MaintenanceReminders", "CompletedById", "int NULL");
        await EnsureMySqlColumnAsync(connString, "MaintenanceReminders", "CompletedAt", "datetime(6) NULL");
        await EnsureMySqlInspectionTablesAsync(connString);

        // 多租户：为历史库补充 WorkshopId 列
        await EnsureMySqlTenantColumnsAsync(connString);
        await EnsureMySqlTenantIndexesAsync(connString);
    }
    else
    {
        // 旧 SQLite 库可能缺少模型新增的列，自动补充（文件不存在则跳过）
        var sqlitePath = Path.Combine(app.Environment.ContentRootPath, "workshop.db");
        if (File.Exists(sqlitePath))
        {
            await EnsureColumnAsync(sqlitePath, "Products", "ImageUrl");
            await EnsureColumnAsync(sqlitePath, "Equipments", "ImageUrl");
            await EnsureColumnAsync(sqlitePath, "WorkReports", "ProcessCardId");
            await EnsureColumnAsync(sqlitePath, "WorkReports", "ProcessStepNo");
            await EnsureColumnAsync(sqlitePath, "WorkReports", "ProcessStepName");
            await EnsureColumnAsync(sqlitePath, "Employees", "WeChatOpenId");
            await EnsureSqliteEquipmentTimeTableAsync(sqlitePath);
            await EnsureSqliteProcessTablesAsync(sqlitePath);
            await EnsureSqliteProcessCardColumnsAsync(sqlitePath);
            await EnsureColumnAsync(sqlitePath, "QualityRecords", "ProcessCardId");
            await EnsureColumnAsync(sqlitePath, "QualityRecords", "ProcessStepNo");
            await EnsureColumnAsync(sqlitePath, "QualityRecords", "ProcessStepName");
            await EnsureSqliteInspectionTablesAsync(sqlitePath);

            // 多租户：为历史库补充 WorkshopId 列
            await EnsureSqliteTenantColumnsAsync(sqlitePath);
            await EnsureSqliteTenantIndexesAsync(sqlitePath);
        }
    }

    // 多租户：历史数据回填到第一个车间（老库是单车间部署）
    await BackfillTenantWorkshopIdsAsync(db);

    DbSeed.Seed(db);
}

// --- 中间件管道 ---
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 静态文件服务：供 /uploads/... 现场照片访问
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors();
app.UseAuthentication();

// 多租户：从 JWT 声明解析当前车间（未登录时不设置，全局过滤器不生效，用于登录/种子场景）
app.Use(async (context, next) =>
{
    var tenant = context.RequestServices.GetRequiredService<TenantContext>();
    tenant.LoadFromClaims(context.User);
    await next();
});

app.UseAuthorization();
app.MapControllers();

// SPA 回退：非 API 请求全部返回 index.html，由前端路由接管（支持 history 模式刷新）
app.MapFallbackToFile("index.html");

app.Lifetime.ApplicationStarted.Register(() =>
{
    Console.WriteLine();
    Console.WriteLine("========================================");
    Console.WriteLine("  CNC 车间管理系统 API 已启动");
    Console.WriteLine("  浏览器: http://localhost:5219/swagger");
    Console.WriteLine("  健康检查: http://localhost:5219/api/health");
    Console.WriteLine("  演示账号: 管理员 E900/admin123");
    Console.WriteLine("  操作工   E001～E002/123456");
    Console.WriteLine("  质检员   E003/123456");
    Console.WriteLine("  按 Ctrl+C 停止服务");
    Console.WriteLine("========================================");
    Console.WriteLine();
});

app.Run();

// ========== SQLite → MySQL 数据迁移 ==========
static async Task MigrateSqliteToMySqlAsync(string contentRoot, IConfiguration configuration)
{
    Console.WriteLine("========== 开始迁移 SQLite → MySQL ==========");

    var sqlitePath = Path.Combine(contentRoot, "workshop.db");
    if (!File.Exists(sqlitePath))
        throw new FileNotFoundException($"找不到 SQLite 数据库文件：{sqlitePath}");

    var mysqlConn = configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("缺少 MySQL 连接串 ConnectionStrings:DefaultConnection");

    var optionsSqlite = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={sqlitePath}").Options;
    var optionsMySql = new DbContextOptionsBuilder<AppDbContext>().UseMySql(mysqlConn, ServerVersion.AutoDetect(mysqlConn)).Options;

    // 旧 SQLite 库可能没有模型新增的列，先补上（含多租户 WorkshopId 列与车间内唯一索引）
    await EnsureColumnAsync(sqlitePath, "Products", "ImageUrl");
    await EnsureColumnAsync(sqlitePath, "Equipments", "ImageUrl");
    await EnsureSqliteTenantColumnsAsync(sqlitePath);
    await EnsureSqliteTenantIndexesAsync(sqlitePath);

    // 回填旧数据的 WorkshopId 到第一个车间（单车间老库）
    await using (var sqliteBackfill = new AppDbContext(optionsSqlite))
    {
        await BackfillTenantWorkshopIdsAsync(sqliteBackfill);
    }

    await CreateDatabaseIfNotExistsAsync(mysqlConn);

    // 2. 建表 + 按外键顺序复制数据（保留原 ID）
    Console.WriteLine("[2/2] 建表并复制数据…");
    await using var sqlite = new AppDbContext(optionsSqlite);
    await using var mysql = new AppDbContext(optionsMySql);

    await mysql.Database.EnsureCreatedAsync();

    if (await mysql.Workshops.AnyAsync())
    {
        Console.WriteLine("[警告] MySQL 中已存在数据，为避免重复导入，迁移已中止。");
        Console.WriteLine("        如需重新迁移，请先清空 MySQL 的 ManagementCNCWorkshop 数据库。");
        return;
    }

    var workshops = await sqlite.Workshops.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
    mysql.Workshops.AddRange(workshops);
    await mysql.SaveChangesAsync();
    Console.WriteLine($"  车间：{workshops.Count} 条");

    var employees = await sqlite.Employees.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
    mysql.Employees.AddRange(employees);
    await mysql.SaveChangesAsync();
    Console.WriteLine($"  员工：{employees.Count} 条");

    var products = await sqlite.Products.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
    mysql.Products.AddRange(products);
    await mysql.SaveChangesAsync();
    Console.WriteLine($"  产品：{products.Count} 条");

    var equipments = await sqlite.Equipments.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
    mysql.Equipments.AddRange(equipments);
    await mysql.SaveChangesAsync();
    Console.WriteLine($"  设备：{equipments.Count} 条");

    var workReports = await sqlite.WorkReports.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
    mysql.WorkReports.AddRange(workReports);
    await mysql.SaveChangesAsync();
    Console.WriteLine($"  报工记录：{workReports.Count} 条");

    var qualityRecords = await sqlite.QualityRecords.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
    mysql.QualityRecords.AddRange(qualityRecords);
    await mysql.SaveChangesAsync();
    Console.WriteLine($"  质检记录：{qualityRecords.Count} 条");

    var plans = await sqlite.MaintenancePlans.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
    mysql.MaintenancePlans.AddRange(plans);
    await mysql.SaveChangesAsync();
    Console.WriteLine($"  保养计划：{plans.Count} 条");

    var reminders = await sqlite.MaintenanceReminders.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
    mysql.MaintenanceReminders.AddRange(reminders);
    await mysql.SaveChangesAsync();
    Console.WriteLine($"  保养提醒：{reminders.Count} 条");

    // 工艺路线/工序/流转卡（保证 MySQL 中已建表）
    await EnsureMySqlProcessTablesAsync(mysqlConn);

    var processFlows = await sqlite.ProcessFlows.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
    mysql.ProcessFlows.AddRange(processFlows);
    await mysql.SaveChangesAsync();
    Console.WriteLine($"  工艺路线：{processFlows.Count} 条");

    var processSteps = await sqlite.ProcessSteps.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
    mysql.ProcessSteps.AddRange(processSteps);
    await mysql.SaveChangesAsync();
    Console.WriteLine($"  工序：{processSteps.Count} 条");

    var processCards = await sqlite.ProcessCards.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
    mysql.ProcessCards.AddRange(processCards);
    await mysql.SaveChangesAsync();
    Console.WriteLine($"  流转卡：{processCards.Count} 条");

    var processCardSteps = await sqlite.ProcessCardSteps.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
    mysql.ProcessCardSteps.AddRange(processCardSteps);
    await mysql.SaveChangesAsync();
    Console.WriteLine($"  流转卡工序：{processCardSteps.Count} 条");

    Console.WriteLine();
    Console.WriteLine("========== 迁移完成！现在可以正常启动后端（连接 MySQL） ==========");
}

static async Task CreateDatabaseIfNotExistsAsync(string mysqlConn)
{
    var csb = new MySqlConnector.MySqlConnectionStringBuilder(mysqlConn);
    var dbName = csb.Database;
    csb.Database = null;

    Console.WriteLine($"[1/2] 创建数据库（如不存在）：{dbName}");
    await using var conn = new MySqlConnector.MySqlConnection(csb.ConnectionString);
    await conn.OpenAsync();
    await using var cmd = conn.CreateCommand();
    cmd.CommandText = $"CREATE DATABASE IF NOT EXISTS `{dbName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci";
    await cmd.ExecuteNonQueryAsync();
}

/// <summary>前置兼容：给旧 SQLite 表补充新字段（ALTER TABLE 仅在列不存在时执行）</summary>
static async Task EnsureColumnAsync(string sqlitePath, string table, string column)
{
    await using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={sqlitePath}");
    await conn.OpenAsync();

    var cols = new List<string>();
    await using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = $"PRAGMA table_info(`{table}`)";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            cols.Add(reader.GetString(1));
    }

    if (cols.Contains(column)) return;

    await using var cmd2 = conn.CreateCommand();
    cmd2.CommandText = $"ALTER TABLE `{table}` ADD COLUMN `{column}` TEXT NULL";
    await cmd2.ExecuteNonQueryAsync();
    Console.WriteLine($"  SQLite 表 {table} 已补充列 {column}");
}

/// <summary>前置兼容：给 MySQL 表补充新字段（EnsureCreated 不会修改已有表结构）</summary>
static async Task EnsureMySqlColumnAsync(string mysqlConn, string table, string column, string definition)
{
    await using var conn = new MySqlConnector.MySqlConnection(mysqlConn);
    await conn.OpenAsync();

    await using var check = conn.CreateCommand();
    check.CommandText =
        $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS " +
        $"WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{table}' AND COLUMN_NAME = '{column}'";
    if (Convert.ToInt32(await check.ExecuteScalarAsync()) > 0) return;

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = $"ALTER TABLE `{table}` ADD COLUMN `{column}` {definition}";
    await cmd.ExecuteNonQueryAsync();
    Console.WriteLine($"  MySQL 表 {table} 已补充列 {column}");
}

static async Task EnsureMySqlEquipmentTimeTableAsync(string mysqlConn)
{
    await using var conn = new MySqlConnector.MySqlConnection(mysqlConn);
    await conn.OpenAsync();

    await using var check = conn.CreateCommand();
    check.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'EquipmentTimeRecords'";
    if (Convert.ToInt32(await check.ExecuteScalarAsync()) > 0) return;

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
CREATE TABLE `EquipmentTimeRecords` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `EquipmentId` int NOT NULL,
  `RecordDate` datetime NOT NULL,
  `SetupHours` decimal(10,2) NOT NULL,
  `RunningHours` decimal(10,2) NOT NULL,
  `IdleHours` decimal(10,2) NOT NULL,
  `EmployeeId` int NULL,
  `Remark` longtext NULL,
  `CreatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_EquipmentTimeRecords_EquipmentId_RecordDate` (`EquipmentId`,`RecordDate`),
  KEY `IX_EquipmentTimeRecords_EquipmentId` (`EquipmentId`),
  KEY `IX_EquipmentTimeRecords_EmployeeId` (`EmployeeId`),
  CONSTRAINT `FK_EquipmentTimeRecords_Equipments_EquipmentId` FOREIGN KEY (`EquipmentId`) REFERENCES `Equipments` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_EquipmentTimeRecords_Employees_EmployeeId` FOREIGN KEY (`EmployeeId`) REFERENCES `Employees` (`Id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";
    await cmd.ExecuteNonQueryAsync();
    Console.WriteLine("  MySQL 表 EquipmentTimeRecords 已补充");
}

static async Task EnsureSqliteEquipmentTimeTableAsync(string sqlitePath)
{
    await using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={sqlitePath}");
    await conn.OpenAsync();

    await using var check = conn.CreateCommand();
    check.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='EquipmentTimeRecords'";
    var exists = await check.ExecuteScalarAsync();
    if (exists is not null) return;

    await using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS EquipmentTimeRecords (
  Id INTEGER NOT NULL CONSTRAINT PK_EquipmentTimeRecords PRIMARY KEY AUTOINCREMENT,
  EquipmentId INTEGER NOT NULL,
  RecordDate TEXT NOT NULL,
  SetupHours TEXT NOT NULL,
  RunningHours TEXT NOT NULL,
  IdleHours TEXT NOT NULL,
  EmployeeId INTEGER NULL,
  Remark TEXT NULL,
  CreatedAt TEXT NOT NULL,
  CONSTRAINT FK_EquipmentTimeRecords_Equipments_EquipmentId FOREIGN KEY (EquipmentId) REFERENCES Equipments (Id) ON DELETE CASCADE,
  CONSTRAINT FK_EquipmentTimeRecords_Employees_EmployeeId FOREIGN KEY (EmployeeId) REFERENCES Employees (Id) ON DELETE SET NULL
);";
    await cmd.ExecuteNonQueryAsync();
    Console.WriteLine("  SQLite 表 EquipmentTimeRecords 已补充");
}

/// <summary>MySQL：补充工艺路线/工序/流转卡表</summary>
static async Task EnsureMySqlProcessTablesAsync(string mysqlConn)
{
    await using var conn = new MySqlConnector.MySqlConnection(mysqlConn);
    await conn.OpenAsync();

    async Task CreateTableIfMissingAsync(string table, string ddl)
    {
        await using var check = conn.CreateCommand();
        check.CommandText =
            "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '" + table + "'";
        if (Convert.ToInt32(await check.ExecuteScalarAsync()) > 0) return;

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = ddl;
        await cmd.ExecuteNonQueryAsync();
        Console.WriteLine($"  MySQL 表 {table} 已补充");
    }

    await CreateTableIfMissingAsync("ProcessFlows", @"
CREATE TABLE `ProcessFlows` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `Code` varchar(64) NOT NULL,
  `Name` longtext NOT NULL,
  `ProductId` int NOT NULL,
  `Version` int NOT NULL,
  `Status` varchar(32) NOT NULL,
  `Description` longtext NULL,
  `CreatedById` int NULL,
  `CreatedAt` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_ProcessFlows_ProductId` (`ProductId`),
  KEY `IX_ProcessFlows_Status` (`Status`),
  KEY `IX_ProcessFlows_CreatedById` (`CreatedById`),
  CONSTRAINT `FK_ProcessFlows_Employees_CreatedById` FOREIGN KEY (`CreatedById`) REFERENCES `Employees` (`Id`) ON DELETE SET NULL,
  CONSTRAINT `FK_ProcessFlows_Products_ProductId` FOREIGN KEY (`ProductId`) REFERENCES `Products` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;");

    await CreateTableIfMissingAsync("ProcessSteps", @"
CREATE TABLE `ProcessSteps` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `ProcessFlowId` int NOT NULL,
  `StepNo` int NOT NULL,
  `Name` longtext NOT NULL,
  `Description` longtext NULL,
  `EquipmentId` int NULL,
  `DurationMinutes` int NULL,
  `RequiresInspection` tinyint(1) NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_ProcessSteps_ProcessFlowId_StepNo` (`ProcessFlowId`,`StepNo`),
  KEY `IX_ProcessSteps_EquipmentId` (`EquipmentId`),
  CONSTRAINT `FK_ProcessSteps_Equipments_EquipmentId` FOREIGN KEY (`EquipmentId`) REFERENCES `Equipments` (`Id`) ON DELETE SET NULL,
  CONSTRAINT `FK_ProcessSteps_ProcessFlows_ProcessFlowId` FOREIGN KEY (`ProcessFlowId`) REFERENCES `ProcessFlows` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;");

    await CreateTableIfMissingAsync("ProcessCards", @"
CREATE TABLE `ProcessCards` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `Code` varchar(64) NOT NULL,
  `ProcessFlowId` int NOT NULL,
  `ProductId` int NOT NULL,
  `Quantity` decimal(18,4) NOT NULL,
  `MaterialSpec` longtext NULL,
  `SurfaceTreatment` longtext NULL,
  `Status` varchar(32) NOT NULL,
  `CurrentStepNo` int NOT NULL,
  `CreatedById` int NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `StartedAt` datetime(6) NULL,
  `CompletedAt` datetime(6) NULL,
  `Remark` longtext NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_ProcessCards_ProcessFlowId` (`ProcessFlowId`),
  KEY `IX_ProcessCards_ProductId` (`ProductId`),
  KEY `IX_ProcessCards_Status` (`Status`),
  KEY `IX_ProcessCards_CreatedById` (`CreatedById`),
  CONSTRAINT `FK_ProcessCards_Employees_CreatedById` FOREIGN KEY (`CreatedById`) REFERENCES `Employees` (`Id`) ON DELETE SET NULL,
  CONSTRAINT `FK_ProcessCards_ProcessFlows_ProcessFlowId` FOREIGN KEY (`ProcessFlowId`) REFERENCES `ProcessFlows` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_ProcessCards_Products_ProductId` FOREIGN KEY (`ProductId`) REFERENCES `Products` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;");

    await CreateTableIfMissingAsync("ProcessCardSteps", @"
CREATE TABLE `ProcessCardSteps` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `ProcessCardId` int NOT NULL,
  `StepNo` int NOT NULL,
  `StepName` longtext NOT NULL,
  `Status` longtext NOT NULL,
  `MachineNo` varchar(64) NULL,
  `WorkDate` datetime(6) NULL,
  `Shift` varchar(16) NULL,
  `Quantity` decimal(18,4) NULL,
  `StartedAt` datetime(6) NULL,
  `CompletedAt` datetime(6) NULL,
  `OperatorId` int NULL,
  `InspectorId` int NULL,
  `Remark` longtext NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_ProcessCardSteps_ProcessCardId_StepNo` (`ProcessCardId`,`StepNo`),
  KEY `IX_ProcessCardSteps_OperatorId` (`OperatorId`),
  KEY `IX_ProcessCardSteps_InspectorId` (`InspectorId`),
  CONSTRAINT `FK_ProcessCardSteps_Employees_OperatorId` FOREIGN KEY (`OperatorId`) REFERENCES `Employees` (`Id`) ON DELETE SET NULL,
  CONSTRAINT `FK_ProcessCardSteps_Employees_InspectorId` FOREIGN KEY (`InspectorId`) REFERENCES `Employees` (`Id`) ON DELETE SET NULL,
  CONSTRAINT `FK_ProcessCardSteps_ProcessCards_ProcessCardId` FOREIGN KEY (`ProcessCardId`) REFERENCES `ProcessCards` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;");
}

/// <summary>SQLite：补充工艺路线/工序/流转卡表</summary>
static async Task EnsureSqliteProcessTablesAsync(string sqlitePath)
{
    await using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={sqlitePath}");
    await conn.OpenAsync();

    async Task CreateTableIfMissingAsync(string table, string ddl)
    {
        await using var check = conn.CreateCommand();
        check.CommandText = $"SELECT name FROM sqlite_master WHERE type='table' AND name='{table}'";
        var exists = await check.ExecuteScalarAsync();
        if (exists is not null) return;

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = ddl;
        await cmd.ExecuteNonQueryAsync();
        Console.WriteLine($"  SQLite 表 {table} 已补充");
    }

    await CreateTableIfMissingAsync("ProcessFlows", @"
CREATE TABLE IF NOT EXISTS ProcessFlows (
  Id INTEGER NOT NULL CONSTRAINT PK_ProcessFlows PRIMARY KEY AUTOINCREMENT,
  Code TEXT NOT NULL,
  Name TEXT NOT NULL,
  ProductId INTEGER NOT NULL,
  Version INTEGER NOT NULL,
  Status TEXT NOT NULL,
  Description TEXT NULL,
  CreatedById INTEGER NULL,
  CreatedAt TEXT NOT NULL,
  CONSTRAINT FK_ProcessFlows_Employees_CreatedById FOREIGN KEY (CreatedById) REFERENCES Employees (Id) ON DELETE SET NULL,
  CONSTRAINT FK_ProcessFlows_Products_ProductId FOREIGN KEY (ProductId) REFERENCES Products (Id) ON DELETE CASCADE
);");

    await CreateTableIfMissingAsync("ProcessSteps", @"
CREATE TABLE IF NOT EXISTS ProcessSteps (
  Id INTEGER NOT NULL CONSTRAINT PK_ProcessSteps PRIMARY KEY AUTOINCREMENT,
  ProcessFlowId INTEGER NOT NULL,
  StepNo INTEGER NOT NULL,
  Name TEXT NOT NULL,
  Description TEXT NULL,
  EquipmentId INTEGER NULL,
  DurationMinutes INTEGER NULL,
  RequiresInspection INTEGER NOT NULL,
  CONSTRAINT FK_ProcessSteps_Equipments_EquipmentId FOREIGN KEY (EquipmentId) REFERENCES Equipments (Id) ON DELETE SET NULL,
  CONSTRAINT FK_ProcessSteps_ProcessFlows_ProcessFlowId FOREIGN KEY (ProcessFlowId) REFERENCES ProcessFlows (Id) ON DELETE CASCADE
);");

    await CreateTableIfMissingAsync("ProcessCards", @"
CREATE TABLE IF NOT EXISTS ProcessCards (
  Id INTEGER NOT NULL CONSTRAINT PK_ProcessCards PRIMARY KEY AUTOINCREMENT,
  Code TEXT NOT NULL,
  ProcessFlowId INTEGER NOT NULL,
  ProductId INTEGER NOT NULL,
  Quantity TEXT NOT NULL,
  MaterialSpec TEXT NULL,
  SurfaceTreatment TEXT NULL,
  Status TEXT NOT NULL,
  CurrentStepNo INTEGER NOT NULL,
  CreatedById INTEGER NULL,
  CreatedAt TEXT NOT NULL,
  StartedAt TEXT NULL,
  CompletedAt TEXT NULL,
  Remark TEXT NULL,
  CONSTRAINT FK_ProcessCards_Employees_CreatedById FOREIGN KEY (CreatedById) REFERENCES Employees (Id) ON DELETE SET NULL,
  CONSTRAINT FK_ProcessCards_ProcessFlows_ProcessFlowId FOREIGN KEY (ProcessFlowId) REFERENCES ProcessFlows (Id) ON DELETE CASCADE,
  CONSTRAINT FK_ProcessCards_Products_ProductId FOREIGN KEY (ProductId) REFERENCES Products (Id) ON DELETE CASCADE
);");

    await CreateTableIfMissingAsync("ProcessCardSteps", @"
CREATE TABLE IF NOT EXISTS ProcessCardSteps (
  Id INTEGER NOT NULL CONSTRAINT PK_ProcessCardSteps PRIMARY KEY AUTOINCREMENT,
  ProcessCardId INTEGER NOT NULL,
  StepNo INTEGER NOT NULL,
  StepName TEXT NOT NULL,
  Status TEXT NOT NULL,
  MachineNo TEXT NULL,
  WorkDate TEXT NULL,
  Shift TEXT NULL,
  Quantity TEXT NULL,
  StartedAt TEXT NULL,
  CompletedAt TEXT NULL,
  OperatorId INTEGER NULL,
  InspectorId INTEGER NULL,
  Remark TEXT NULL,
  CONSTRAINT FK_ProcessCardSteps_Employees_OperatorId FOREIGN KEY (OperatorId) REFERENCES Employees (Id) ON DELETE SET NULL,
  CONSTRAINT FK_ProcessCardSteps_Employees_InspectorId FOREIGN KEY (InspectorId) REFERENCES Employees (Id) ON DELETE SET NULL,
  CONSTRAINT FK_ProcessCardSteps_ProcessCards_ProcessCardId FOREIGN KEY (ProcessCardId) REFERENCES ProcessCards (Id) ON DELETE CASCADE
);");
}

/// <summary>MySQL：补充流转卡新增的纸质卡字段</summary>
static async Task EnsureMySqlProcessCardColumnsAsync(string mysqlConn)
{
    await EnsureMySqlColumnAsync(mysqlConn, "ProcessCards", "MaterialSpec", "longtext NULL");
    await EnsureMySqlColumnAsync(mysqlConn, "ProcessCards", "SurfaceTreatment", "longtext NULL");
    await EnsureMySqlColumnAsync(mysqlConn, "ProcessCardSteps", "MachineNo", "varchar(64) NULL");
    await EnsureMySqlColumnAsync(mysqlConn, "ProcessCardSteps", "WorkDate", "datetime(6) NULL");
    await EnsureMySqlColumnAsync(mysqlConn, "ProcessCardSteps", "Shift", "varchar(16) NULL");
    await EnsureMySqlColumnAsync(mysqlConn, "ProcessCardSteps", "Quantity", "decimal(18,4) NULL");
    await EnsureMySqlColumnAsync(mysqlConn, "ProcessCardSteps", "InspectorId", "int NULL");

    // 补 InspectorId 索引 + 外键（已有表不会自动加）
    await using var conn = new MySqlConnector.MySqlConnection(mysqlConn);
    await conn.OpenAsync();

    await using var chkIdx = conn.CreateCommand();
    chkIdx.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'ProcessCardSteps' AND INDEX_NAME = 'IX_ProcessCardSteps_InspectorId'";
    if (Convert.ToInt32(await chkIdx.ExecuteScalarAsync()) == 0)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "CREATE INDEX `IX_ProcessCardSteps_InspectorId` ON `ProcessCardSteps` (`InspectorId`)";
        await cmd.ExecuteNonQueryAsync();
        Console.WriteLine("  MySQL ProcessCardSteps 索引 IX_InspectorId 已补充");
    }

    await using var chkFk = conn.CreateCommand();
    chkFk.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'ProcessCardSteps' AND CONSTRAINT_NAME = 'FK_ProcessCardSteps_Employees_InspectorId'";
    if (Convert.ToInt32(await chkFk.ExecuteScalarAsync()) == 0)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "ALTER TABLE `ProcessCardSteps` ADD CONSTRAINT `FK_ProcessCardSteps_Employees_InspectorId` FOREIGN KEY (`InspectorId`) REFERENCES `Employees` (`Id`) ON DELETE SET NULL";
        await cmd.ExecuteNonQueryAsync();
        Console.WriteLine("  MySQL ProcessCardSteps 外键 FK_InspectorId 已补充");
    }
}

/// <summary>SQLite：补充流转卡新增的纸质卡字段</summary>
static async Task EnsureMySqlQualityProcessColumnsAsync(string mysqlConn)
{
    await EnsureMySqlColumnAsync(mysqlConn, "QualityRecords", "ProcessCardId", "int NULL");
    await EnsureMySqlColumnAsync(mysqlConn, "QualityRecords", "ProcessStepNo", "int NULL");
    await EnsureMySqlColumnAsync(mysqlConn, "QualityRecords", "ProcessStepName", "varchar(255) NULL");
}

static async Task EnsureSqliteProcessCardColumnsAsync(string sqlitePath)
{
    await EnsureColumnAsync(sqlitePath, "ProcessCards", "MaterialSpec");
    await EnsureColumnAsync(sqlitePath, "ProcessCards", "SurfaceTreatment");
    await EnsureColumnAsync(sqlitePath, "ProcessCardSteps", "MachineNo");
    await EnsureColumnAsync(sqlitePath, "ProcessCardSteps", "WorkDate");
    await EnsureColumnAsync(sqlitePath, "ProcessCardSteps", "Shift");
    await EnsureColumnAsync(sqlitePath, "ProcessCardSteps", "Quantity");
    await EnsureColumnAsync(sqlitePath, "ProcessCardSteps", "InspectorId");
}

/// <summary>MySQL：补充设备点检表（EquipmentInspections + EquipmentInspectionItems）</summary>
static async Task EnsureMySqlInspectionTablesAsync(string mysqlConn)
{
    await using var conn = new MySqlConnector.MySqlConnection(mysqlConn);
    await conn.OpenAsync();

    async Task CreateTableIfMissingAsync(string table, string ddl)
    {
        await using var check = conn.CreateCommand();
        check.CommandText =
            "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '" + table + "'";
        if (Convert.ToInt32(await check.ExecuteScalarAsync()) > 0) return;

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = ddl;
        await cmd.ExecuteNonQueryAsync();
        Console.WriteLine($"  MySQL 表 {table} 已补充");
    }

    await CreateTableIfMissingAsync("EquipmentInspections", @"
CREATE TABLE `EquipmentInspections` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `EquipmentId` int NOT NULL,
  `InspectDate` datetime(6) NOT NULL,
  `Shift` varchar(16) NOT NULL,
  `InspectorId` int NULL,
  `AbnormalNote` longtext NULL,
  `Remark` longtext NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_EquipmentInspections_EquipmentId_InspectDate_Shift` (`EquipmentId`,`InspectDate`,`Shift`),
  KEY `IX_EquipmentInspections_InspectDate` (`InspectDate`),
  KEY `IX_EquipmentInspections_InspectorId` (`InspectorId`),
  CONSTRAINT `FK_EquipmentInspections_Equipments_EquipmentId` FOREIGN KEY (`EquipmentId`) REFERENCES `Equipments` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_EquipmentInspections_Employees_InspectorId` FOREIGN KEY (`InspectorId`) REFERENCES `Employees` (`Id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;");

    await CreateTableIfMissingAsync("EquipmentInspectionItems", @"
CREATE TABLE `EquipmentInspectionItems` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `InspectionId` int NOT NULL,
  `ItemNo` int NOT NULL,
  `ItemName` longtext NOT NULL,
  `Status` varchar(16) NOT NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_EquipmentInspectionItems_InspectionId_ItemNo` (`InspectionId`,`ItemNo`),
  CONSTRAINT `FK_EquipmentInspectionItems_EquipmentInspections_InspectionId` FOREIGN KEY (`InspectionId`) REFERENCES `EquipmentInspections` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;");
}

/// <summary>SQLite：补充设备点检表</summary>
static async Task EnsureSqliteInspectionTablesAsync(string sqlitePath)
{
    await using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={sqlitePath}");
    await conn.OpenAsync();

    async Task CreateTableIfMissingAsync(string table, string ddl)
    {
        await using var check = conn.CreateCommand();
        check.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='" + table + "'";
        if (await check.ExecuteScalarAsync() is not null) return;

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = ddl;
        await cmd.ExecuteNonQueryAsync();
        Console.WriteLine($"  SQLite 表 {table} 已补充");
    }

    await CreateTableIfMissingAsync("EquipmentInspections", @"
CREATE TABLE IF NOT EXISTS EquipmentInspections (
  Id INTEGER NOT NULL CONSTRAINT PK_EquipmentInspections PRIMARY KEY AUTOINCREMENT,
  EquipmentId INTEGER NOT NULL,
  InspectDate TEXT NOT NULL,
  Shift TEXT NOT NULL,
  InspectorId INTEGER NULL,
  AbnormalNote TEXT NULL,
  Remark TEXT NULL,
  CreatedAt TEXT NOT NULL,
  UpdatedAt TEXT NULL,
  CONSTRAINT FK_EquipmentInspections_Equipments_EquipmentId FOREIGN KEY (EquipmentId) REFERENCES Equipments (Id) ON DELETE CASCADE,
  CONSTRAINT FK_EquipmentInspections_Employees_InspectorId FOREIGN KEY (InspectorId) REFERENCES Employees (Id) ON DELETE SET NULL
);
CREATE UNIQUE INDEX IX_EquipmentInspections_EquipmentId_InspectDate_Shift ON EquipmentInspections (EquipmentId, InspectDate, Shift);
CREATE INDEX IX_EquipmentInspections_InspectDate ON EquipmentInspections (InspectDate);");

    await CreateTableIfMissingAsync("EquipmentInspectionItems", @"
CREATE TABLE IF NOT EXISTS EquipmentInspectionItems (
  Id INTEGER NOT NULL CONSTRAINT PK_EquipmentInspectionItems PRIMARY KEY AUTOINCREMENT,
  InspectionId INTEGER NOT NULL,
  ItemNo INTEGER NOT NULL,
  ItemName TEXT NOT NULL,
  Status TEXT NOT NULL,
  CONSTRAINT FK_EquipmentInspectionItems_EquipmentInspections_InspectionId FOREIGN KEY (InspectionId) REFERENCES EquipmentInspections (Id) ON DELETE CASCADE
);
CREATE INDEX IX_EquipmentInspectionItems_InspectionId_ItemNo ON EquipmentInspectionItems (InspectionId, ItemNo);");
}

// ─────────────────── 多租户（车间）兼容 ───────────────────

/// <summary>需要按车间隔离的表清单（已有 WorkshopId 列的表不需要在此处理）</summary>
static string[] TenantTables() => new[]
{
    "Products",
    "MaintenancePlans",
    "MaintenanceReminders",
    "ProcessFlows",
    "ProcessSteps",
    "ProcessCards",
    "ProcessCardSteps",
    "EquipmentTimeRecords",
    "EquipmentInspections",
    "EquipmentInspectionItems"
};

/// <summary>MySQL：为历史表补充 WorkshopId 列（新库由 EnsureCreated 直接创建，无需处理）</summary>
static async Task EnsureMySqlTenantColumnsAsync(string mysqlConn)
{
    foreach (var table in TenantTables())
        await EnsureMySqlColumnAsync(mysqlConn, table, "WorkshopId", "int NOT NULL DEFAULT 0");
}

/// <summary>SQLite：为历史表补充 WorkshopId 列</summary>
static async Task EnsureSqliteTenantColumnsAsync(string sqlitePath)
{
    foreach (var table in TenantTables())
        await EnsureColumnAsync(sqlitePath, table, "WorkshopId");
}

/// <summary>历史数据回填：老库是单车间部署，把 WorkshopId 为空或 0 的数据归到第一个车间</summary>
static async Task BackfillTenantWorkshopIdsAsync(AppDbContext db)
{
    var firstWorkshopId = await db.Workshops.OrderBy(x => x.Id).Select(x => (int?)x.Id).FirstOrDefaultAsync();
    if (firstWorkshopId is null) return;

    foreach (var table in TenantTables())
    {
        await db.Database.ExecuteSqlRawAsync(
            $"UPDATE `{table}` SET `WorkshopId` = {firstWorkshopId} WHERE `WorkshopId` IS NULL OR `WorkshopId` = 0");
    }
    Console.WriteLine($"  [多租户] 历史数据已回填到车间 {firstWorkshopId}");
}

/// <summary>需要从全局唯一改为车间内唯一的（表, 旧索引名, 新索引列）</summary>
static (string Table, string OldIndex, string NewIndex, string Column)[] TenantUniqueIndexes() => new[]
{
    ("Employees", "IX_Employees_EmployeeNo", "IX_Employees_WorkshopId_EmployeeNo", "EmployeeNo"),
    ("Products", "IX_Products_Code", "IX_Products_WorkshopId_Code", "Code"),
    ("Equipments", "IX_Equipments_Code", "IX_Equipments_WorkshopId_Code", "Code"),
    ("ProcessFlows", "IX_ProcessFlows_Code", "IX_ProcessFlows_WorkshopId_Code", "Code"),
    ("ProcessCards", "IX_ProcessCards_Code", "IX_ProcessCards_WorkshopId_Code", "Code")
};

/// <summary>MySQL：把全局唯一索引迁移为车间内唯一（老库可能已存在旧索引）</summary>
static async Task EnsureMySqlTenantIndexesAsync(string mysqlConn)
{
    await using var conn = new MySqlConnector.MySqlConnection(mysqlConn);
    await conn.OpenAsync();

    foreach (var (table, oldIndex, newIndex, column) in TenantUniqueIndexes())
    {
        // 删除旧全局唯一索引
        await using var checkOld = conn.CreateCommand();
        checkOld.CommandText =
            $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS " +
            $"WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{table}' AND INDEX_NAME = '{oldIndex}'";
        if (Convert.ToInt32(await checkOld.ExecuteScalarAsync()) > 0)
        {
            await using var drop = conn.CreateCommand();
            drop.CommandText = $"ALTER TABLE `{table}` DROP INDEX `{oldIndex}`";
            await drop.ExecuteNonQueryAsync();
            Console.WriteLine($"  MySQL 表 {table} 已删除旧唯一索引 {oldIndex}");
        }

        // 建立车间内唯一索引（若不存在）
        await using var checkNew = conn.CreateCommand();
        checkNew.CommandText =
            $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS " +
            $"WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{table}' AND INDEX_NAME = '{newIndex}'";
        if (Convert.ToInt32(await checkNew.ExecuteScalarAsync()) == 0)
        {
            await using var create = conn.CreateCommand();
            create.CommandText = $"CREATE UNIQUE INDEX `{newIndex}` ON `{table}` (`WorkshopId`, `{column}`)";
            await create.ExecuteNonQueryAsync();
            Console.WriteLine($"  MySQL 表 {table} 已建立车间内唯一索引 {newIndex}");
        }
    }
}

/// <summary>SQLite：把全局唯一索引迁移为车间内唯一</summary>
static async Task EnsureSqliteTenantIndexesAsync(string sqlitePath)
{
    await using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={sqlitePath}");
    await conn.OpenAsync();

    foreach (var (table, oldIndex, newIndex, column) in TenantUniqueIndexes())
    {
        // SQLite 老索引名（EF Core 生成：IX_{Table}_{Column}）
        await using var drop = conn.CreateCommand();
        drop.CommandText = $"DROP INDEX IF EXISTS `{oldIndex}`";
        await drop.ExecuteNonQueryAsync();

        await using var checkNew = conn.CreateCommand();
        checkNew.CommandText = $"SELECT name FROM sqlite_master WHERE type='index' AND name='{newIndex}'";
        if (await checkNew.ExecuteScalarAsync() is null)
        {
            await using var create = conn.CreateCommand();
            create.CommandText = $"CREATE UNIQUE INDEX `{newIndex}` ON `{table}` (`WorkshopId`, `{column}`)";
            await create.ExecuteNonQueryAsync();
            Console.WriteLine($"  SQLite 表 {table} 已建立车间内唯一索引 {newIndex}");
        }
    }
}
