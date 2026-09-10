#!/bin/bash
# ============================================================
# Claude Code 后端一键切换：Vibe助手(api-worker) / DeepSeek官方
# 用法：
#   bash switch-claude-backend.sh vibe       # 走 Vibe助手 9182
#   bash switch-claude-backend.sh deepseek   # 走 DeepSeek 3456 代理
#   bash switch-claude-backend.sh status     # 看当前指向
# 只改本终端会话环境变量 + settings.json 兜底，不杀任何进程
# ============================================================
set -e

CN_DIR="/Users/liaobin/.claude"
SETTINGS="$CN_DIR/settings.json"
CNC_DIR="/Users/liaobin/Desktop/cnc"
NODE_BIN="$CNC_DIR/.tools/node/bin/node"
PROXY_JS="$CNC_DIR/.tools/claude-deepseek-proxy.js"
PROXY_PORT=3456
DEEPSEEK_API_KEY="${DEEPSEEK_API_KEY:-}"

blue()  { printf "\033[36m%s\033[0m\n" "$1"; }
green() { printf "\033[32m%s\033[0m\n" "$1"; }
red()   { printf "\033[31m%s\033[0m\n" "$1"; }

current_base() {
  python3 - "$SETTINGS" <<'PY'
import json, sys
try:
    with open(sys.argv[1]) as f:
        print(json.load(f).get("env", {}).get("ANTHROPIC_BASE_URL", ""))
except Exception:
    print("")
PY
}

# ---------- status ----------
if [ "${1:-}" = "status" ] || [ -z "${1:-}" ]; then
  BASE=$(current_base)
  MODEL=$(python3 - "$SETTINGS" <<'PY'
import json, sys
try:
    with open(sys.argv[1]) as f:
        print(json.load(f).get("env", {}).get("ANTHROPIC_MODEL", ""))
except Exception:
    print("")
PY
)
  VIBE_UP=0; DS_UP=0
  curl -s -m 2 http://127.0.0.1:9182/health >/dev/null 2>&1 && VIBE_UP=1
  curl -s -m 2 "http://127.0.0.1:$PROXY_PORT/health" >/dev/null 2>&1 && DS_UP=1
  echo "=============================================="
  case "$BASE" in
    *9182)
      green "★ 当前生效后端: Vibe助手 api-worker (:9182)"
      blue "  模型: ${MODEL:-未设置}  ← 注意这是 Vibe 池子里的模型名"
      [ "$VIBE_UP" = 1 ] && green "  服务状态: 在线 ✓" || red "  服务状态: 离线 ✗ (打开 Vibe助手 app)"
      [ "$DS_UP" = 1 ] && blue "  备用后端 DeepSeek官方代理(:$PROXY_PORT): 在线待命" || blue "  备用后端 DeepSeek官方代理(:$PROXY_PORT): 未启动"
      ;;
    *3456)
      green "★ 当前生效后端: DeepSeek 官方 API (本地代理 :$PROXY_PORT)"
      blue "  模型: ${MODEL:-未设置} (底层 deepseek-chat)"
      [ "$DS_UP" = 1 ] && green "  服务状态: 在线 ✓" || red "  服务状态: 离线 ✗ (重新跑 cc-ds)"
      [ "$VIBE_UP" = 1 ] && blue "  备用后端 Vibe助手(:9182): 在线待命" || blue "  备用后端 Vibe助手(:9182): 未启动"
      ;;
    "")
      red  "★ 当前生效后端: 未配置 (settings.json 无 ANTHROPIC_BASE_URL)"
      ;;
    *)
      blue "★ 当前生效后端: $BASE（其它）"
      ;;
  esac
  echo "=============================================="
  exit 0
fi

# ---------- deepseek: 确保 3456 代理在线 ----------
ensure_proxy() {
  if curl -s -m 2 "http://127.0.0.1:$PROXY_PORT/health" | grep -q "cnc-claude-proxy"; then
    green "✅ DeepSeek 代理已在运行 (:$PROXY_PORT)"
    return
  fi
  OLD_PID=$(lsof -ti "tcp:$PROXY_PORT" 2>/dev/null || true)
  [ -n "$OLD_PID" ] && kill -9 $OLD_PID 2>/dev/null || true
  [ -z "$DEEPSEEK_API_KEY" ] && { red "❌ 未设置 DEEPSEEK_API_KEY，请先 export DEEPSEEK_API_KEY=sk-... 再运行"; exit 1; }
  blue "▶ 启动 DeepSeek 协议代理 (:$PROXY_PORT)..."
  DEEPSEEK_API_KEY="$DEEPSEEK_API_KEY" PROXY_PORT="$PROXY_PORT" \
    nohup "$NODE_BIN" "$PROXY_JS" > /tmp/claude-proxy.log 2>&1 &
  for i in $(seq 1 20); do
    curl -s -m 2 "http://127.0.0.1:$PROXY_PORT/health" >/dev/null 2>&1 && break
    sleep 0.5
  done
  curl -s -m 2 "http://127.0.0.1:$PROXY_PORT/health" | grep -q cnc-claude-proxy \
    && green "✅ 代理已启动" || { red "❌ 代理启动失败，看 /tmp/claude-proxy.log"; exit 1; }
}

# ---------- settings.json 写 env ----------
write_settings() { # $1=base_url $2=model $3=small_model
  python3 - "$SETTINGS" "$1" "$2" "$3" <<'PY'
import json, os, sys
path, base, model, small = sys.argv[1:5]
try:
    with open(path) as f:
        cfg = json.load(f)
except Exception:
    cfg = {}
env = cfg.get("env", {})
if base:
    env["ANTHROPIC_BASE_URL"] = base
    env["ANTHROPIC_AUTH_TOKEN"] = "local-backend"
    env["ANTHROPIC_MODEL"] = model
    env["ANTHROPIC_SMALL_FAST_MODEL"] = small
else:  # 清空，交还给 Vibe助手/Claude 默认
    for k in ("ANTHROPIC_BASE_URL", "ANTHROPIC_AUTH_TOKEN", "ANTHROPIC_MODEL", "ANTHROPIC_SMALL_FAST_MODEL"):
        env.pop(k, None)
cfg["env"] = env
os.makedirs(os.path.dirname(path), exist_ok=True)
with open(path, "w") as f:
    json.dump(cfg, f, indent=2, ensure_ascii=False)
PY
}

case "${1:-}" in
  vibe)
    curl -s -m 2 http://127.0.0.1:9182/health >/dev/null 2>&1 || { red "❌ Vibe助手 api-worker(:9182) 未运行，先打开 Vibe助手"; exit 1; }
    write_settings "http://127.0.0.1:9182" "deepseek-v4-flash" "deepseek-v4-flash"
    green "✅ 已切到 Vibe助手 api-worker (http://127.0.0.1:9182)"
    blue "   模型: deepseek-v4-flash（worker 还提供 claude-opus-4-6 / gpt-5.4，可在 /model 手动验证）"
    ;;
  deepseek)
    ensure_proxy
    write_settings "http://127.0.0.1:$PROXY_PORT" "claude-haiku-4-5-20251001" "claude-haiku-4-5-20251001"
    green "✅ 已切到 DeepSeek 官方 (经 127.0.0.1:$PROXY_PORT 协议代理)"
    blue "   底层模型: deepseek-chat，/model 里选 Haiku/Sonnet/Opus 均可"
    ;;
  off)
    write_settings "" "" ""
    green "✅ 已清除 settings.json 里的后端覆盖（Vibe助手下次激活会重新写入）"
    ;;
  *)
    red "用法: bash $0 {vibe|deepseek|status|off}"
    exit 1
    ;;
esac

echo ""
blue "──────── 当前状态 ────────"
bash "$0" status
echo ""
blue "新开终端直接跑 claude 即可生效；当前已开的 Claude Code 会话需退出重进。"
