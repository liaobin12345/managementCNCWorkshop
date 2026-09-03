#!/bin/bash
# ============================================================
# 配置 Claude Code → DeepSeek（本地代理方式）
# 运行后，Claude Code 会通过本地代理调用 DeepSeek API
# ============================================================

echo "=============================================="
echo "  配置 Claude Code → DeepSeek"
echo "=============================================="
echo ""

# 检查 claude 是否可用
if ! command -v claude >/dev/null 2>&1; then
    echo "⚠️ 当前 Terminal 找不到 claude 命令（可能 PATH 没配）"
    echo "   但没关系，配置照写。"
fi

# 写入 ~/.claude/settings.json
mkdir -p ~/.claude

python3 << 'PYEOF'
import json, os

cfg_path = os.path.expanduser("~/.claude/settings.json")
cfg = {}
if os.path.exists(cfg_path):
    try:
        with open(cfg_path, "r", encoding="utf-8") as f:
            cfg = json.load(f)
    except Exception:
        cfg = {}

cfg.setdefault("env", {})
cfg["env"]["ANTHROPIC_BASE_URL"] = "http://127.0.0.1:3456"
cfg["env"]["ANTHROPIC_AUTH_TOKEN"] = "deepseek-local-proxy"
cfg["env"]["ANTHROPIC_MODEL"] = "claude-sonnet-4-20250514"
cfg["env"]["ANTHROPIC_SMALL_FAST_MODEL"] = "claude-3-5-haiku-20241022"

with open(cfg_path, "w", encoding="utf-8") as f:
    json.dump(cfg, f, ensure_ascii=False, indent=2)
print("✅ 配置已写入 ~/.claude/settings.json")
print("   ANTHROPIC_BASE_URL = http://127.0.0.1:3456")
print("   （所有请求会通过本地代理转发到 DeepSeek）")
PYEOF

echo ""
echo "=============================================="
echo "  配置完成！"
echo "=============================================="
echo ""
echo "使用方式："
echo "  1. 启动代理："
echo "     bash /Users/liaobin/Desktop/cnc/scripts/start-claude-deepseek.sh"
echo ""
echo "  2. 或者一键启动（代理 + Claude Code 一起）："
echo "     bash /Users/liaobin/Desktop/cnc/scripts/start-claude-deepseek.sh"
echo ""
echo "注：代理启动后，即使直接在终端里运行 claude 也能正常使用"
echo "   （因为 ~/.claude/settings.json 已配置好代理地址）"
echo ""