#!/bin/zsh
# 后端热重启开发脚本（请在「你自己的终端」里运行，不要在编辑器沙箱里跑）
#
# 作用：监听后端源码，改动后自动重新编译并重启，免去手动 build。
#   - 端口固定为 5219（前后端配置已统一指向该端口）
#   - 绑定 0.0.0.0，同一 Wi-Fi 的真机/手机可通过 http://本机IP:5219 访问
#   - Ctrl+C 停止
#
# 用法：zsh scripts/dev-api-watch.sh
# 本机 IP 查看：系统设置 -> Wi-Fi -> 详细信息（局域网地址，如 192.168.x.x）

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

# 沙箱里才需要轮询监视器（且会触发无限构建），自己终端用原生监视器
unset DOTNET_USE_POLLING_FILE_WATCHER

cd "$PROJECT_ROOT/src/ManagementCNCWorkshop.Api"

# 先释放端口（本脚本请在自己终端运行，拥有杀进程权限）
PIDS=$(lsof -ti:5219 2>/dev/null)
if [ -n "$PIDS" ]; then
  echo "端口 5219 被占用，正在释放进程: $PIDS"
  kill -9 $PIDS 2>/dev/null
  sleep 1
fi

export ASPNETCORE_URLS="http://0.0.0.0:5219"
export ASPNETCORE_ENVIRONMENT="Development"

echo "=============================================="
echo "  后端热重启模式（dotnet watch）"
echo "  端口: http://localhost:5219"
echo "  修改后端代码后会自动重新编译并重启"
echo "  Ctrl+C 停止"
echo "=============================================="

exec dotnet watch run --no-launch-profile
