# 项目交接笔记（NOTES）

> 给 AI 助手 / 任何人快速接手用：新对话里先读本文件和 README.md。

## 项目一句话
CNC 车间管理系统：UniApp 小程序（工人端）+ Vue3 Web（运营后台）+ ASP.NET Core 8 后端 + MySQL，面向中小 CNC 加工厂的车间数字化系统。

## 当前运行状态（2026-08）
- 后端跑在 **5219**（dev-api-watch，cwd=`src/ManagementCNCWorkshop.Api`），MySQL（本地 root/root），SQLite 兜底
- 外网 UAT 隧道（cloudflared Quick Tunnel，**进程断即失效、重启换新地址**）：
  - 运营后台：`https://<隧道域名>/login`
  - 小程序 H5：`https://<隧道域名>/h5/index.html`（H5 构建部署在 `src/ManagementCNCWorkshop.Api/wwwroot/h5/`，API 走同源相对 `/api`）
- 小程序 H5 构建配置：manifest.json `h5.router.base = "/h5/"`；`src/config.js` 条件编译：H5 用 `/api`，小程序用局域网 `http://192.168.101.242:5219/api`

## 关键配置
- 微信登录：`appsettings.json` 的 `Wechat.DebugMode=true`（调试模式，任意 code 返回 `DebugOpenId=oX-debug-openid-001`），**AppId/AppSecret 为空，待用户提供**
- JWT Secret：`cnc-workshop-v1-demo-...`（**上线前必须换生产密钥**）
- 演示账号：E900/admin123（管理员）、E001/123456（操作工）、E003/123456（质检）、E004/123456（技术员）

## 已完成
- **多租户/数据隔离（2026-09）**：
  - 以「车间（Workshop）」为租户边界：所有业务实体（员工/产品/设备/报工/质检/保养/工艺/流转卡/点检/设备时间）按 `WorkshopId` 隔离
  - 实现方式：`TenantContext`（scoped，从 JWT `WorkshopId` 声明解析）+ `AppDbContext` 全局查询过滤器（`ITenantScoped` 接口 + `HasQueryFilter`）+ 保存时自动补全/强制车间
  - 角色策略：**Admin = 平台运营者（可见全部车间，用于开通租户/分配账号）；Worker/Inspector/Programmer = 车间用户（严格只读/写自己车间）**
  - JWT 已带 `WorkshopId` 声明；`/api/worker/workshops` 对车间用户只返回自己车间
  - 历史库兼容：启动时自动为老表补 `WorkshopId` 列、回填到第一个车间、把全局唯一索引迁移为「车间内唯一」（员工工号/产品编码/设备编码/工艺编号/流转卡编号均按车间唯一）
  - 数据回填与索引迁移逻辑在 `Program.cs` 底部 `Ensure*Tenant*Async` 函数
  - **注意**：登录仍是「工号+密码」，工号在车间内唯一（两个车间可以都有 E001，登录时按工号查询会取第一个匹配，后续可加车间编码登录作为 P1）
- 微信登录闭环：`/api/auth/wechat-login`（code→openid，需绑定时返回 needsBind）、`/api/auth/wechat-bind`（绑定工号）、已绑定自动登录；Employees 表自动补 `WeChatOpenId` 列
- 设备点检：模板、提交、月度日历报表（今日列高亮、31 号留白）、技术员可提交、操作工 403
- 登录页：卡片垂直居中（absolute top:50% + translateY）、按钮文字 flex 居中（替代 line-height 偏移问题）
- UAT 冒烟测试：`scripts/uat-smoke-test.sh` 48/48 通过
- 简历已重写为全栈方向（桌面 `廖斌个人简历_完整版.md`），GitHub 链接已加
- 代码已发布 GitHub：`https://github.com/liaobin12345/managementCNCWorkshop`（远程名 `github`，分支 `cnc-develop` → GitHub `main`；CNB 远程名 `origin`）

## 待办（上线前）
1. 用户提供微信小程序 **AppID / AppSecret** → 写入两处 appsettings、`DebugMode=false`、manifest.json 填 appid
2. 换 **JWT 生产密钥**（appsettings.json）
3. 后端重启以加载 `UseDefaultFiles`（`/h5/` 短地址可用，当前需 `/h5/index.html`）
4. 产品化规划：多租户、点检/报工字段可配置、CNC 设备数据采集（FOCAS/OPC-UA）、微信订阅消息

## 常用命令
```bash
# 启动后端（开发）
zsh scripts/dev-api-watch.sh
# 正式版（publish 目录，绑定 0.0.0.0:5219）
zsh scripts/start-server.sh
# 小程序构建（mp 输出到 dist/build/mp-weixin；H5 输出到 dist/build/h5）
cd src/ManagementCNCWorkshop.MiniApp && npx uni build -p mp-weixin
cd src/ManagementCNCWorkshop.MiniApp && npx uni build -p h5
# H5 部署（用户导入 dist/dev 时需同步）：
#   cp -R dist/build/h5 <后端 wwwroot>/h5
# 冒烟测试
bash scripts/uat-smoke-test.sh
# 外网临时隧道（用户本机跑，需 cloudflared）
zsh scripts/uat-expose.sh 5219
```

## Git 双远程流程（方案 B，SourceTree）
- 本地分支 `cnc-develop`，远程：`github`（GitHub main）+ `origin`（CNB）
- 每次开发完：Commit → Push 到 `github`（cnc-develop→main）→ Push 到 `origin`（cnc-develop→main）
- 命令版：
```bash
git add . && git commit -m "说明"
git push github cnc-develop:main
git push origin cnc-develop:main
```
