#!/bin/bash
# ============================================================
# 一键修复 DeepSeek 代理 404 问题（需在用户自己的终端运行，
# Cursor 沙箱内无权杀掉旧代理进程）
# 用法: bash /Users/liaobin/Desktop/cnc/scripts/fix-deepseek-proxy.sh
# ============================================================
set -e

CNC_DIR="/Users/liaobin/Desktop/cnc"
NODE_BIN="$CNC_DIR/.tools/node/bin/node"
PROXY_JS="$CNC_DIR/.tools/claude-deepseek-proxy.js"
PROXY_PORT=3456
DEEPSEEK_API_KEY="${DEEPSEEK_API_KEY:?请先设置 DEEPSEEK_API_KEY 环境变量（export DEEPSEEK_API_KEY=sk-...）}"

green() { printf "\033[32m%s\033[0m\n" "$1"; }
red()   { printf "\033[31m%s\033[0m\n" "$1"; }
blue()  { printf "\033[36m%s\033[0m\n" "$1"; }

# 1. 杀掉旧代理（无论是不是 v2，都换成已修复的版本）
OLD_PID=$(lsof -ti "tcp:$PROXY_PORT" 2>/dev/null || true)
if [ -n "$OLD_PID" ]; then
  blue "▶ 结束旧代理进程 (PID $OLD_PID)..."
  kill -9 $OLD_PID 2>/dev/null || true
  sleep 1
fi

# 2. 启动已修复的代理（路由匹配已支持 ?beta=true 等 query 参数）
blue "▶ 启动修复版代理 (:$PROXY_PORT)..."
DEEPSEEK_API_KEY="$DEEPSEEK_API_KEY" PROXY_PORT="$PROXY_PORT" \
  nohup "$NODE_BIN" "$PROXY_JS" > /tmp/claude-proxy.log 2>&1 &
for i in $(seq 1 20); do
  curl -s -m 2 "http://127.0.0.1:$PROXY_PORT/health" >/dev/null 2>&1 && break
  sleep 0.5
done

# 3. 验证：健康检查 + 带 ?beta=true 的真实请求（Claude Code 实际请求路径）
if ! curl -s -m 2 "http://127.0.0.1:$PROXY_PORT/health" | grep -q cnc-claude-proxy; then
  red "❌ 代理启动失败，请查看 /tmp/claude-proxy.log"
  exit 1
fi
green "✅ 代理已启动 (:$PROXY_PORT)"

blue "▶ 验证 Claude Code 真实请求路径 (/v1/messages?beta=true)..."
RESP=$(curl -s -m 60 -X POST "http://127.0.0.1:$PROXY_PORT/v1/messages?beta=true" \
  -H "content-type: application/json" \
  -H "x-api-key: local-backend" \
  -H "anthropic-version: 2023-06-01" \
  -d '{"model":"claude-haiku-4-5-20251001","max_tokens":30,"messages":[{"role":"user","content":"回复OK"}]}')
echo "$RESP" | head -c 400
echo ""
if echo "$RESP" | grep -q '"role":"assistant"'; then
  green "✅ 链路完全打通！退出当前 Claude Code 会话重新进入即可正常对话"
else
  red "❌ 请求仍失败，请把上面的输出发给排查"
fi

# 4. 顺带清理沙箱实验遗留的 3457 端口进程（若有）
P357=$(lsof -ti tcp:3457 2>/dev/null || true)
[ -n "$P357" ] && kill -9 $P357 2>/dev/null || true

bash "$CNC_DIR/scripts/switch-claude-backend.sh" status
