#!/bin/bash
# ============================================================
# 一键启动 Claude Code（通过 DeepSeek 代理）
# 用法：
#   bash /Users/liaobin/Desktop/cnc/scripts/start-claude-deepseek.sh
# ============================================================
set -e

TOOLS_DIR="/Users/liaobin/Desktop/cnc/.tools"
DEEPSEEK_API_KEY="${DEEPSEEK_API_KEY:-sk-f84bae525d4f4aa0b8fc87fae35abb89}"
PROXY_PORT="${PROXY_PORT:-3456}"

# ---------- 1. 找 Node ----------
NODE_BIN=""
if command -v node >/dev/null 2>&1; then
    NODE_BIN="$(command -v node)"
elif [ -x "$TOOLS_DIR/node/bin/node" ]; then
    NODE_BIN="$TOOLS_DIR/node/bin/node"
else
    echo "❌ 找不到 Node.js。请先运行安装脚本或检查 PATH。"
    exit 1
fi
export PATH="$(dirname "$NODE_BIN"):$TOOLS_DIR/npm-global/bin:$PATH"

# ---------- 2. 启动代理（如果没在跑，或跑的是旧版本） ----------
HEALTH=$(curl -s -m 2 "http://127.0.0.1:${PROXY_PORT}/health" 2>/dev/null)
if echo "$HEALTH" | grep -q "cnc-claude-proxy-2"; then
    echo "✅ 代理已在运行（v2）"
else
    # 端口被占用但不是我方 v2 代理，清理掉
    OLD_PID=$(lsof -ti "tcp:${PROXY_PORT}" 2>/dev/null)
    if [ -n "$OLD_PID" ]; then
        echo "⚠️ 发现端口 ${PROXY_PORT} 被旧代理占用 (PID $OLD_PID)，正在清理..."
        kill -9 $OLD_PID 2>/dev/null || true
        sleep 1
    fi
    echo "▶ 启动协议转换代理（端口 ${PROXY_PORT}）..."
    DEEPSEEK_API_KEY="$DEEPSEEK_API_KEY" PROXY_PORT="$PROXY_PORT" \
        nohup "$NODE_BIN" "$TOOLS_DIR/claude-deepseek-proxy.js" \
        > "$TMPDIR/claude-proxy.log" 2>&1 &
    PROXY_PID=$!
    # 等待就绪
    for i in $(seq 1 20); do
        if curl -s -m 2 "http://127.0.0.1:${PROXY_PORT}/health" >/dev/null 2>&1; then
            break
        fi
        sleep 0.5
    done
    echo "   ✅ 代理已启动 (PID $PROXY_PID)"
fi

# ---------- 3. 配置环境变量并启动 Claude Code ----------
export ANTHROPIC_BASE_URL="http://127.0.0.1:${PROXY_PORT}"
export ANTHROPIC_AUTH_TOKEN="deepseek-local-proxy"
export ANTHROPIC_MODEL="claude-sonnet-4-20250514"
export ANTHROPIC_SMALL_FAST_MODEL="claude-3-5-haiku-20241022"

echo ""
echo "=============================================="
echo "  🎉 正在启动 Claude Code（DeepSeek 后端）"
echo "  API Key: ${DEEPSEEK_API_KEY:0:8}..."
echo "=============================================="
echo ""

exec claude "$@"
