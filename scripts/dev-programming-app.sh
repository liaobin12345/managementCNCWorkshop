#!/bin/zsh
# 数控车编程助手：一条命令启动「后端 API + H5 前端」调试环境
# 用法：zsh scripts/dev-programming-app.sh
#
# 启动后：
#   - 后端 API : http://localhost:5219/swagger
#   - H5 前端  : http://localhost:5174   （登录后即可新建程序 / 上传 DXF 测试）
#   - Ctrl+C   : 同时停掉后端与前端
#
# 数据库默认 SQLite（workshop.db 在 Api 项目目录），无需额外配置 MySQL。

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
API_DIR="$PROJECT_ROOT/src/ManagementCNCWorkshop.Api"
APP_DIR="$PROJECT_ROOT/src/ManagementCNCWorkshop.ProgrammingApp"

export PATH="$PROJECT_ROOT/.tools/bin:$PATH"
export DOTNET_USE_POLLING_FILE_WATCHER=true
export CI=true

# 1) 自动释放端口上的旧进程
for PORT in 5219 5174; do
  PIDS=$(lsof -ti tcp:$PORT -sTCP:LISTEN 2>/dev/null)
  if [ -n "$PIDS" ]; then
    echo "端口 $PORT 已被占用，停掉旧进程: $PIDS"
    kill -9 $PIDS 2>/dev/null
    sleep 1
  fi
done

# 2) 后台启动后端 API（SQLite）
echo "▶ 启动后端 API（SQLite） http://localhost:5219 ..."
(cd "$API_DIR" && ASPNETCORE_URLS="http://0.0.0.0:5219" Database__Provider=sqlite \
  ConnectionStrings__DefaultConnection="Data Source=$API_DIR/workshop.db" \
  dotnet run --no-launch-profile) > /tmp/cnc-api-dev.log 2>&1 &
API_PID=$!

# 3) 前台启动 H5 前端（Ctrl+C 时一并清理后端）
cleanup() {
  echo ""
  echo "⏹ 正在停止后端 (PID $API_PID)..."
  kill -9 $API_PID 2>/dev/null
}
trap cleanup INT TERM EXIT

echo "▶ 启动 H5 前端 http://localhost:5174 ..."
echo "=============================================="
echo "  后端 API : http://localhost:5219/swagger"
echo "  H5 前端  : http://localhost:5174"
echo "  测试账号 : E004 / 123456（编程技术员）"
echo "  Ctrl+C  停止"
echo "=============================================="
cd "$APP_DIR" && npm run dev:h5
