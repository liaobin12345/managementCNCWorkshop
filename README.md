# Management CNC Workshop

CNC 数据车间管理系统 — 前后端分离架构，供 UniApp 小程序（工人端）和 Web 运营后台使用。

## 架构总览

```
┌───────────────────┐     ┌─────────────────────────────┐
│  工人端（手机）      │     │  运营后台（PC 浏览器）        │
│  UniApp 小程序      │     │  Vue 3 + Element Plus       │
│  扫码报工 / 质检     │     │  主数据 / 统计 / 报表        │
│  保养提醒 / 我的产量  │     │  保养计划 / 提醒生成          │
└─────────┬─────────┘     └──────────────┬──────────────┘
          │                              │
          ▼                              ▼
┌──────────────────────────────────────────────────────┐
│                ASP.NET Core 8 后端 API                 │
│  认证: POST /api/auth/login → JWT                     │
│  工人端: /api/worker/*   [角色 Worker/Inspector]       │
│  运营后台: /api/admin/*  [角色 Admin]                  │
│  SQLite 数据库                                        │
└──────────────────────────────────────────────────────┘
```

> **角色与权限通过 JWT 隔离**：工人小程序只能访问 `/api/worker/*`，运营后台只能访问 `/api/admin/*`，主数据维护、统计报表等敏感接口不再暴露给工人端。

## 目录结构

```
cnc/
├── ManagementCNCWorkshop.sln
└── src/
    ├── ManagementCNCWorkshop.Api/     # 后端 API（ASP.NET Core 8 + SQLite）
    │   ├── Controllers/
    │   │   ├── AuthController.cs      # 登录（获取 JWT）
    │   │   ├── Worker/                # 工人端接口 /api/worker/*
    │   │   ├── Admin/                 # 运营后台接口 /api/admin/*
    │   │   └── HealthController.cs    # 健康检查（匿名）
    │   ├── Models/                    # 8 张表实体 + DTO
    │   ├── Services/                  # JWT 生成、密码哈希、外键校验
    │   └── Data/                      # EF Core 上下文 + 演示数据
    └── ManagementCNCWorkshop.Web/     # 运营后台前端（Vue 3 + Element Plus）
        └── src/
            ├── views/                 # 看板、主数据、产量、质量、保养
            ├── layout/                # 侧边栏 + 顶栏布局
            ├── api/                   # Axios 封装（自动带 JWT）
            └── store/                 # Pinia 登录态
```

## 快速启动

### 1. 启动后端 API

```bash
cd /Users/liaobin/Desktop/cnc
zsh scripts/restore.sh      # 首次执行，修复 NuGet 权限并还原
dotnet run --project src/ManagementCNCWorkshop.Api
```

浏览器打开 `http://localhost:5219/swagger`。

### 2. 启动运营后台（Web 管理端）

```bash
cd src/ManagementCNCWorkshop.Web
npm install
npm run dev
```

浏览器打开 `http://localhost:5173`，使用管理员账号登录。

> Vite 开发服务器已配置 `/api` 代理到 `http://localhost:5219`，无需处理跨域。

## 演示账号

| 角色 | 工号 | 密码 | 用途 |
|------|------|------|------|
| 管理员 | E900 | admin123 | 登录运营后台（Web） |
| 操作工 | E001 / E002 | 123456 | 登录工人端小程序 |
| 质检员 | E003 | 123456 | 登录工人端小程序，可录入质检 |

## API 一览

### 认证（匿名）

| 功能 | 方法 | 路径 |
|------|------|------|
| 账号密码登录，返回 JWT | POST | `/api/auth/login` |

### 工人端（需 JWT，角色 Worker / Inspector）

| 功能 | 方法 | 路径 |
|------|------|------|
| 我的信息 | GET | `/api/worker/me` |
| 车间/产品/设备列表 | GET | `/api/worker/workshops`、`/products`、`/equipments` |
| 扫码查产品 | GET | `/api/worker/products/by-qrcode?code=PROD:P001` |
| 扫码查设备 | GET | `/api/worker/equipments/by-qrcode?code=EQ:CNC-01` |
| 扫码报工 | POST | `/api/worker/work-reports/scan` |
| 我的报工记录（分页） | GET | `/api/worker/work-reports/my` |
| 录入质检记录（Inspector） | POST | `/api/worker/quality` |
| 待处理保养提醒 | GET | `/api/worker/maintenance/reminders/pending` |

### 运营后台（需 JWT，角色 Admin）

| 功能 | 方法 | 路径 |
|------|------|------|
| 主数据（车间/员工/产品/设备）增查 | GET/POST | `/api/admin/master/workshops\|employees\|products\|equipments` |
| 扫码查产品/设备 | GET | `/api/admin/master/products\|equipments/by-qrcode` |
| 报工记录（分页、筛选） | GET | `/api/admin/work-reports` |
| 产量统计 日/周/月 | GET | `/api/admin/stats/production/daily\|weekly\|monthly` |
| 质检记录（分页） | GET | `/api/admin/quality` |
| 质量统计 日/周/月 | GET | `/api/admin/quality/stats/daily\|weekly\|monthly` |
| 保养计划 增查 | GET/POST | `/api/admin/maintenance/plans` |
| 待处理保养提醒 | GET | `/api/admin/maintenance/reminders/pending` |
| 自动生成保养提醒 | POST | `/api/admin/maintenance/reminders/generate` |

### 请求示例

```bash
# 1. 登录获取 token
curl -X POST http://localhost:5219/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"employeeNo":"E900","password":"admin123"}'

# 2. 携带 token 调用运营后台接口
curl http://localhost:5219/api/admin/stats/production/daily \
  -H "Authorization: Bearer <token>"
```

## 数据表设计（8 张）

| 表 | 用途 | 核心字段 |
|----|------|----------|
| `Workshops` | 车间 | Code, Name |
| `Employees` | 员工/小程序用户 | EmployeeNo, Name, **Role**, **PasswordHash**, WorkshopId, WeChatOpenId |
| `Products` | 产品/零件 | Code, Name, Specification, QrCode |
| `WorkReports` | 扫码报工 | ProductId, WorkshopId, EmployeeId, Quantity, QualifiedQty, DefectQty, ScanPayload |
| `QualityRecords` | 质量记录 | SampleQty, QualifiedQty, DefectQty, DefectType |
| `Equipments` | CNC 设备 | Code, Name, Status, LastMaintenanceDate, QrCode |
| `MaintenancePlans` | 保养计划 | CycleDays, NextDueDate, RemindDaysBefore, Content |
| `MaintenanceReminders` | 保养提醒 | DueDate, RemindDate, Status |

## 注意事项

- **升级旧库**：V1 的 `workshop.db` 没有 `Role` / `PasswordHash` 字段。如沿用旧库请删除 `src/ManagementCNCWorkshop.Api/workshop.db` 后重启，会自动重建并写入演示数据。
- 演示账号密码为开发环境使用；生产环境请接入微信登录（`WeChatOpenId` 字段已预留）并替换 JWT 密钥。

## 后续可扩展

- 微信登录 + JWT 鉴权（接口已按角色隔离，`WeChatOpenId` 字段已预留）
- 工单、工位、不良类型字典
- 运营后台：数据编辑/删除、权限角色管理、导出报表
- 统计汇总表 + 后台定时任务（自动生成保养提醒）
- SQL Server 生产部署 + EF Migrations
