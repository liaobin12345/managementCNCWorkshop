# CNC 车间管理系统

> 面向中小 CNC 加工厂的车间数字化管理系统：扫码报工 / 设备点检 / 产量质量统计 / 保养管理 / 工艺流转卡 / 微信登录
> **UniApp 小程序（工人端）+ Vue3 Web（运营后台）+ ASP.NET Core 8 后端 + MySQL**

[ASP.NET Core 8](https://dotnet.microsoft.com/) · [UniApp](https://uniapp.dcloud.net.cn/) · [Vue 3](https://cn.vuejs.org/) · [Element Plus](https://element-plus.org/) · [EF Core](https://learn.microsoft.com/ef/core/) · [MySQL](https://www.mysql.com/) · [JWT](https://jwt.io/)

---

## 功能一览

| 端 | 功能 |
|----|------|
| 小程序（工人端） | 微信一键登录、扫码报工、扫码查产品/设备、产量/质量统计、设备点检（月度日历报表）、保养提醒、工艺流转卡 |
| Web（运营后台） | 车间/员工/产品/设备主数据管理、报工/质检记录查询、产量/质量统计看板、保养计划与提醒生成、工艺流转卡管理 |
| 后端 | JWT 角色权限隔离（Admin/Worker/Inspector）、微信登录（code 换 openid + 首次绑定）、RESTful API、Swagger 文档 |

## 技术栈

- **后端**：ASP.NET Core 8 Web API · EF Core Code First · MySQL（兼容 SQLite）· JWT 鉴权 · Swagger
- **小程序**：UniApp (Vue3) · 微信小程序/H5 双端构建
- **Web 后台**：Vue3 + TypeScript + Element Plus + ECharts + Pinia
- **部署**：局域网 0.0.0.0 绑定 / 外网 HTTPS（cloudflared 隧道）

## 快速启动

### 1. 后端 API

```bash
dotnet run --project src/ManagementCNCWorkshop.Api
# 打开 http://localhost:5219/swagger
# 数据库：默认 MySQL（127.0.0.1:3306 root/root），可在 appsettings.json 切换 SQLite
```

### 2. 运营后台（Web）

```bash
cd src/ManagementCNCWorkshop.Web
npm install
npm run dev
# 打开 http://localhost:5173
```

### 3. 小程序（H5 / 微信开发者工具）

```bash
cd src/ManagementCNCWorkshop.MiniApp
npm install
npm run build:h5      # 构建 H5，产物在 dist/build/h5
npm run build:mp-weixin  # 构建微信小程序，产物在 dist/build/mp-weixin
```

## 演示账号

| 角色 | 工号 | 密码 | 用途 |
|------|------|------|------|
| 管理员 | E900 | admin123 | 运营后台（Web） |
| 操作工 | E001 / E002 | 123456 | 工人端小程序 |
| 质检员 | E003 | 123456 | 工人端小程序 |
| 编程技术员 | E004 | 123456 | 工人端小程序（点检） |

## 项目结构

```
cnc/
├── src/
│   ├── ManagementCNCWorkshop.Api/     # ASP.NET Core 8 后端（Controllers/Models/Services/Data）
│   ├── ManagementCNCWorkshop.Web/     # Vue3 + Element Plus 运营后台
│   └── ManagementCNCWorkshop.MiniApp/ # UniApp 工人端（微信小程序 + H5）
└── scripts/                           # 启动/部署/外网暴露脚本
```

## 核心 API

| 模块 | 说明 | 示例 |
|------|------|------|
| 认证 | 账号密码登录、微信登录、微信绑定 | `POST /api/auth/login`、`/api/auth/wechat-login`、`/api/auth/wechat-bind` |
| 工人端 | 扫码报工、基础查询、统计 | `/api/worker/work-reports/scan`、`/api/worker/stats/*` |
| 点检 | 模板、提交、月度报表 | `/api/worker/equipment-inspections/report` |
| 运营后台 | 主数据、报表、保养 | `/api/admin/master/*`、`/api/admin/stats/*` |

完整接口见 Swagger（启动后端后访问 `/swagger`）。

---

## 架构总览

```
┌───────────────────┐     ┌─────────────────────────────┐
│  工人端（手机）      │     │  运营后台（PC 浏览器）        │
│  UniApp 小程序/H5   │     │  Vue 3 + Element Plus       │
│  扫码报工 / 点检     │     │  主数据 / 统计 / 报表        │
│  保养提醒 / 统计     │     │  保养计划 / 流转卡管理       │
└─────────┬─────────┘     └──────────────┬──────────────┘
          │                              │
          ▼                              ▼
┌──────────────────────────────────────────────────────┐
│                ASP.NET Core 8 后端 API                 │
│  认证: POST /api/auth/login → JWT                     │
│  微信登录: code → openid → 绑定/自动登录               │
│  工人端: /api/worker/*   [角色 Worker/Inspector]       │
│  运营后台: /api/admin/*  [角色 Admin]                  │
│  MySQL（兼容 SQLite）                                 │
└──────────────────────────────────────────────────────┘
```

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

## 数据表设计（核心表）

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
| `EquipmentInspections` / `InspectionItems` | 设备点检 | InspectDate, Shift, Status, ItemNo, Result |
| `ProcessFlows` / `ProcessCards` / `CardSteps` | 工艺流转 | FlowName, CardNo, CurrentStepNo, Status, StepNo, StepName |

> 建表方式：EF Core Code First，启动时自动创建并增量补列（兼容 MySQL / SQLite）。

## 注意事项

- 演示账号密码为开发环境使用；生产部署请替换 JWT 密钥、配置正式微信 AppID / AppSecret（当前 `Wechat.DebugMode` 调试模式仅用于本地联调）。
- 微信登录已实现：`/api/auth/wechat-login`（code 换 openid）与 `/api/auth/wechat-bind`（首次绑定工号）。

## 后续可扩展

- 多租户（多工厂数据隔离）、字段/点检模板配置化
- CNC 设备数据采集（FOCAS / OPC-UA / DTU 自动上报产量）
- 微信订阅消息 / 短信提醒
- 运营后台：数据编辑/删除、权限角色管理、导出报表
