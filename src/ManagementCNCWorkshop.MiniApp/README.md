# CNC 车间管理系统 · 工人端（UniApp）

基于 **UniApp（Vue3 + Vite）** 开发的工人端小程序，对接本项目后端 API，界面参照 `CNC车间管理系统原型.html`。

## 功能页面

| 页面 | 说明 |
|---|---|
| 登录 | 工号 + 密码登录（JWT） |
| 工作台 | 今日产量/合格率/不良、快捷入口、待办保养提醒、近 7 日产量趋势 |
| 扫码报工 | 扫产品/设备二维码（`PROD:xxx` / `EQ:xxx`），填写数量提交报工 |
| 产量统计 | 日/周/月产量，按产品分组 + 柱状图 |
| 质量追踪 | 质检录入（质检员）/ 抽检合格率报表（日/周/月） |
| 设备保养 | 待处理提醒 / 保养计划 |
| 数据字典 | 车间 / 产品 / 设备列表 |

## 快速开始

### 方式一：HBuilderX（推荐小程序开发）

1. 打开 HBuilderX → 文件 → 导入 → 从本地目录导入 → 选择 `src/ManagementCNCWorkshop.MiniApp` 文件夹
2. 配置后端地址：修改 `src/config.js` 中的 `BASE_URL`
3. 运行 → 运行到浏览器 / 运行到小程序模拟器 → 微信开发者工具

### 方式二：命令行（Vite）

```bash
cd src/ManagementCNCWorkshop.MiniApp
npm install

# H5 浏览器预览
npm run dev:h5

# 微信小程序（产物在 dist/build/mp-weixin，用微信开发者工具导入）
npm run build:mp-weixin

# 打包 H5 正式版
npm run build:h5
```

> 构建时若沙箱/CI 环境报 `uv_interface_addresses` 错误，先执行 `export CI=true` 再构建。

## 后端地址配置

`src/config.js` 中修改 `BASE_URL`：

```js
export const BASE_URL = 'http://localhost:5219/api'
```

- **H5 本地开发**：保持 `http://localhost:5219/api`
- **真机 / 微信开发者工具调试**：改成电脑局域网 IP，如 `http://192.168.1.100:5219/api`
  （后端 `scripts/start-server.sh` 已绑定 `0.0.0.0`，局域网设备可直接访问）

## 演示账号

| 角色 | 工号 | 密码 |
|---|---|---|
| 操作工 | E001 | 123456 |
| 质检员 | E003 | 123456 |

## 对接的后端接口

| 方法 | 路径 | 说明 |
|---|---|---|
| POST | `/api/auth/login` | 登录 |
| GET | `/api/worker/me` | 当前用户 |
| GET | `/api/worker/workshops` | 车间列表 |
| GET | `/api/worker/products` / `/api/worker/products/by-qrcode` | 产品列表 / 扫码查产品 |
| GET | `/api/worker/equipments` / `/api/worker/equipments/by-qrcode` | 设备列表 / 扫码查设备 |
| POST | `/api/worker/work-reports/scan` | 扫码报工 |
| GET | `/api/worker/stats/summary` | 今日概览 |
| GET | `/api/worker/stats/production/daily\|weekly\|monthly` | 产量统计 |
| GET | `/api/worker/stats/production/trend` | 产量趋势 |
| POST | `/api/worker/quality` | 质检录入（质检员） |
| GET | `/api/worker/stats/quality/daily\|weekly\|monthly` | 质量统计（质检员） |
| GET | `/api/worker/maintenance/reminders/pending` | 待处理保养提醒 |
| GET | `/api/worker/maintenance/plans` | 保养计划 |

## 目录结构

```
src/ManagementCNCWorkshop.MiniApp/
├── package.json / vite.config.js / index.html
├── src/
│   ├── main.js               # 入口（uni-app 约定）
│   ├── App.vue               # 全局样式
│   ├── pages.json            # 页面与 tabBar 配置
│   ├── manifest.json         # 应用配置（Appid 需自行填写）
│   ├── uni.scss              # 全局 SCSS 变量
│   ├── config.js             # 后端地址配置
│   ├── api/                  # 请求封装 + 接口定义
│   ├── utils/format.js       # 格式化/登录守卫工具
│   ├── components/BarChart.vue
│   └── pages/                # 各业务页面
```

> 注：`manifest.json` 中的微信小程序 `appid` 为空，发布到微信需在微信公众平台申请后填入；本地开发可在微信开发者工具中勾选"不校验合法域名"。
