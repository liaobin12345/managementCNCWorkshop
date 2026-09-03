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

# 自动释放 5219 旧进程（避免重复启动报 Address already in use）
PIDS=$(lsof -ti tcp:5219 -sTCP:LISTEN 2>/dev/null)
if [ -n "$PIDS" ]; then
  echo "端口 5219 已被占用，正在停掉旧进程: $PIDS"
  kill -9 $PIDS 2>/dev/null
  sleep 1
fi

# 必须切到 publish 目录再启动，否则程序会到当前目录找 appsettings.json
cd "$PROJECT_ROOT/publish"

exec ./ManagementCNCWorkshop.Api --urls "http://0.0.0.0:5219"
