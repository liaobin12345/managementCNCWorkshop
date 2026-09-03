#!/bin/zsh
# 启动运营后台（Vue3 Web 管理端）
#
# 用法：zsh scripts/start-web.sh
# 打开 http://localhost:5173 （手机同 Wi-Fi 可用 http://电脑IP:5173）
#
# 说明：本机未安装全局 node/npm 时，自动使用项目自带的工具链（.tools/bin）。
#       改过 src/ManagementCNCWorkshop.Web 里的代码会自动热更新。

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

# 项目自带 node/npm（系统未安装全局工具时使用）
export PATH="$PROJECT_ROOT/.tools/bin:$PATH"

cd "$PROJECT_ROOT/src/ManagementCNCWorkshop.Web"

echo "=============================================="
echo "  运营后台（Web 管理端）"
echo "  地址: http://localhost:5173"
echo "  本机 IP 查看: 系统设置 -> Wi-Fi -> 详细信息"
echo "  Ctrl+C 停止"
echo "=============================================="

exec npm run dev