#!/bin/bash
# ============================================================
# 正确安装 Claude Code（npm 方式，国内可用）+ Node.js
# 使用方法：在 Mac 自带终端（Terminal.app）里执行
#   bash /Users/liaobin/Desktop/cnc/scripts/install-claude-code.sh
# ============================================================

set -e

echo "=============================================="
echo "  安装 Claude Code（npm 方式）+ Node.js"
echo "=============================================="
echo ""

# ---------- 1. 安装 Node.js（如果没有） ----------
if command -v node >/dev/null 2>&1; then
    echo "✅ 检测到系统已有 Node.js: $(node --version)"
else
    echo "▶ 未检测到 Node.js，正在下载 Node.js v24.20.0 ..."
    ARCH=$(uname -m)
    if [ "$ARCH" = "arm64" ]; then
        NODE_FILE=node-v24.20.0-darwin-arm64.tar.gz
    else
        NODE_FILE=node-v24.20.0-darwin-x64.tar.gz
    fi
    curl -fsSL "https://nodejs.org/dist/v24.20.0/$NODE_FILE" -o /tmp/node.tar.gz
    echo "▶ 解压到 ~/.node ..."
    mkdir -p ~/.node
    tar -xzf /tmp/node.tar.gz -C ~/.node --strip-components=1
    rm /tmp/node.tar.gz
    export PATH="$HOME/.node/bin:$PATH"
    if ! grep -q '\.node/bin' ~/.zshrc 2>/dev/null; then
        echo "" >> ~/.zshrc
        echo "# Node.js" >> ~/.zshrc
        echo 'export PATH="$HOME/.node/bin:$PATH"' >> ~/.zshrc
    fi
    echo "✅ Node.js $(node --version) 安装完成"
fi

# ---------- 2. 安装 Claude Code（npm 全局） ----------
echo ""
echo "▶ 正在通过 npm 安装 Claude Code ..."
npm install -g @anthropic-ai/claude-code
echo "✅ Claude Code 安装完成"

# ---------- 3. 配置 PATH（npm 全局 bin） ----------
echo ""
echo "▶ 配置 npm 全局命令到 PATH ..."
NPM_PREFIX=$(npm prefix -g)
if ! grep -q 'npm-prefix-global' ~/.zshrc 2>/dev/null; then
    echo "" >> ~/.zshrc
    echo "# npm global bin (# npm-prefix-global)" >> ~/.zshrc
    echo "export PATH=\"$NPM_PREFIX/bin:\$PATH\"" >> ~/.zshrc
fi
export PATH="$NPM_PREFIX/bin:$PATH"

echo ""
echo "=============================================="
echo "  ✅ 安装完成！"
echo "=============================================="
echo ""
echo "验证：打开新的终端窗口后运行"
echo "  claude --version"
echo ""
echo "下一步：配置第三方 API（跳过 claude.ai 登录）"
echo "  我会给你一个配置脚本，跟着跑就行。"
echo ""
