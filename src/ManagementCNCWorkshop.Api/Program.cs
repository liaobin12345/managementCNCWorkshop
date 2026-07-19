using System.Reflection;
using ManagementCNCWorkshop.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Management CNC Workshop API",
        Version = "v1",
        Description = """
            CNC 数据车间管理系统后端接口（V1 初版）。

            模块说明：
            - 主数据：车间、员工、产品、设备的基础维护与扫码查询
            - 扫码报工：小程序扫码后提交产量，是产量统计的数据来源
            - 产量统计：按日/周/月汇总报工数据
            - 质量追踪：录入质检记录，按日/周/月统计合格率
            - 设备保养：保养计划、到期提醒、自动生成提醒
            """
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
});

var dbPath = Path.Combine(builder.Environment.ContentRootPath, "workshop.db");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    DbSeed.Seed(db);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.MapControllers();

app.Lifetime.ApplicationStarted.Register(() =>
{
    Console.WriteLine();
    Console.WriteLine("========================================");
    Console.WriteLine("  API 已启动，服务正在运行中");
    Console.WriteLine("  浏览器打开: http://localhost:5216/swagger");
    Console.WriteLine("  健康检查:   http://localhost:5216/api/health");
    Console.WriteLine("  按 Ctrl+C 停止服务");
    Console.WriteLine("========================================");
    Console.WriteLine();
});

app.Run();
