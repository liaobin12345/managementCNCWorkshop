using System.Reflection;
using System.Text;
using ManagementCNCWorkshop.Api.Data;
using ManagementCNCWorkshop.Api.Models;
using ManagementCNCWorkshop.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ========== 数据迁移模式：dotnet run --project src/ManagementCNCWorkshop.Api -- migrate ==========
// 将 SQLite（workshop.db）里的全部数据复制到 MySQL，复制完成后进程退出。
if (args.Contains("migrate"))
{
    await MigrateSqliteToMySqlAsync(builder.Environment.ContentRootPath, builder.Configuration);
    return;
}

builder.Services.AddControllers();
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
            - **工人端**（`/api/worker/*`）：角色 Worker / Inspector，供 UniApp 小程序调用
            - **运营后台**（`/api/admin/*`）：角色 Admin，供 Web 管理端调用
            - **认证**（`/api/auth/*`）：首次通过 POST /api/auth/login 获取 JWT，后续请求发在 Authorization: Bearer 头

            ## 演示账号
            | 角色 | 工号 | 密码 |
            |------|------|------|
            | 管理员 | E900 | admin123 |
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

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (dbProvider.Equals("mysql", StringComparison.OrdinalIgnoreCase))
        options.UseMySql(connString, ServerVersion.AutoDetect(connString));
    else
        options.UseSqlite(connString);
});

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
    DbSeed.Seed(db);
}

// --- 中间件管道 ---
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Lifetime.ApplicationStarted.Register(() =>
{
    Console.WriteLine();
    Console.WriteLine("========================================");
    Console.WriteLine("  CNC 车间管理系统 API 已启动");
    Console.WriteLine("  浏览器: http://localhost:5216/swagger");
    Console.WriteLine("  健康检查: http://localhost:5216/api/health");
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
