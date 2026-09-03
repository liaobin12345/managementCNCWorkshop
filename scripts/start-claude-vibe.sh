#!/bin/bash
# ============================================================
# 通过 Vibe 助手（api-worker 9182）启动 Claude Code
# 用法：
#   bash /Users/liaobin/Desktop/cnc/scripts/start-claude-vibe.sh
# 说明：
#   - 需要 Vibe 助手 App 保持运行（api-worker 已启动）
#   - 模型默认 deepseek-v4-flash（便宜），可改 VIBE_MODEL 环境变量
# ============================================================
set -e

TOOLS_DIR="/Users/liaobin/Desktop/cnc/.tools"
WORKER_PORT="${VIBE_PORT:-9182}"
VIBE_MODEL="${VIBE_MODEL:-deepseek-v4-flash}"

echo "=============================================="
echo "  Claude Code (Vibe 助手) "
echo "=============================================="

# ---------- 1. 找 Node 和 claude ----------
NODE_BIN=""
if command -v node >/dev/null 2>&1; then
    NODE_BIN="$(command -v node)"
elif [ -x "$TOOLS_DIR/node/bin/node" ]; then
    NODE_BIN="$TOOLS_DIR/node/bin/node"
else
    echo "❌ 找不到 Node.js"
    exit 1
fi
export PATH="$(dirname "$NODE_BIN"):$TOOLS_DIR/npm-global/bin:$PATH"

# ---------- 2. 检查 api-worker ----------
HEALTH=$(curl -s -m 3 "http://127.0.0.1:${WORKER_PORT}/health" 2>/dev/null)
if [ -z "$HEALTH" ]; then
    echo "❌ 没检测到 api-worker (端口 ${WORKER_PORT})"
    echo "   请先打开 Vibe 助手 App，确认 api-worker 状态为「已启动」"
    exit 1
fi
echo "✅ api-worker 运行中 (端口 ${WORKER_PORT})"

# ---------- 3. 配置环境变量 ----------
export ANTHROPIC_BASE_URL="http://127.0.0.1:${WORKER_PORT}"
export ANTHROPIC_AUTH_TOKEN="vibe-local-token"
# 取消之前可能存在的 API key（防止走 DeepSeek 方案）
unset ANTHROPIC_API_KEY 2>/dev/null || true

echo "   API 地址: http://127.0.0.1:${WORKER_PORT}"
echo "   模型: ${VIBE_MODEL}"
echo ""
echo "   提示：启动后可输入 /model 切换模型"
echo "   可选模型: claude-opus-4-6 / deepseek-v4-flash / gpt-5.4"
echo "=============================================="
echo ""

# ---------- 4. 启动 Claude Code ----------
claude --model "$VIBE_MODEL" "$@"