#!/bin/zsh
# 停止占用 5219 端口的 API 进程
# 用法：zsh scripts/stop.sh

PORT=5219
PIDS=($(lsof -ti :$PORT 2>/dev/null))

if [ ${#PIDS[@]} -eq 0 ]; then
  echo "端口 $PORT 上没有运行中的 API"
  exit 0
fi

echo "正在停止端口 $PORT 上的进程: ${PIDS[*]}"
kill ${PIDS[@]} 2>/dev/null
sleep 1

if lsof -i :$PORT >/dev/null 2>&1; then
  echo "普通停止失败，尝试强制停止..."
  kill -9 ${PIDS[@]} 2>/dev/null
fi

if lsof -i :$PORT >/dev/null 2>&1; then
  echo "仍无法释放端口，请手动检查: lsof -i :$PORT"
  exit 1
fi

echo "已停止，端口 $PORT 已释放"
