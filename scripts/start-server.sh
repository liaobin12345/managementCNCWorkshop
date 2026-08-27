#!/bin/zsh
# 启动正式版服务（前端页面 + 后端 API 同一个进程）
# 用法：zsh scripts/start-server.sh
#
# 注意：
# 1. 先停掉开发版后端（Ctrl+C），否则 5219 端口冲突
# 2. 绑定 0.0.0.0，同一局域网的电脑/手机可通过 http://本机IP:5219 访问
#    本机 IP 在「系统设置 -> Wi-Fi -> 详细信息」里查看

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

# 沙箱/受限环境下 OS 文件监视器不可用，改用轮询，避免 PhysicalFilesWatcher 崩溃
export DOTNET_USE_POLLING_FILE_WATCHER=true

# 必须切到 publish 目录再启动，否则程序会到当前目录找 appsettings.json
cd "$PROJECT_ROOT/publish"

exec ./ManagementCNCWorkshop.Api --urls "http://0.0.0.0:5219"
