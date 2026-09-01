#!/bin/bash
# 把本地 UAT（后端+运营后台，0.0.0.0:5219 同一进程）暴露到公网，生成一个 HTTPS 临时地址供外网联调。
#
# 用法：
#   bash scripts/uat-expose.sh                 # 默认暴露 http://localhost:5219
#   bash scripts/uat-expose.sh 5219            # 指定端口
#
# 说明：
#   - 采用 cloudflared Quick Tunnel（无需账号/备案，免费，一键 HTTPS）
#   - 若没有 cloudflared 则尝试 ngrok（需要你先 ngrok authtoken）
#   - 该脚本必须在【你的电脑】上运行（本仓库沙箱无外网出口，代劳不了）
#   - 隧道是临时的：脚本退出后地址失效，适合 UAT 联调用

set -e
PORT="${1:-5219}"
TARGET="http://localhost:$PORT"

echo "==> 目标服务: $TARGET"
echo "    请确认后端已通过 scripts/start-server.sh 或 dev-api-watch 在此端口运行"
echo ""

if command -v cloudflared >/dev/null 2>&1; then
  echo "==> 使用 cloudflared Quick Tunnel（地址见下方 trycloudflare.com）==>"
  cloudflared tunnel --url "$TARGET"
elif command -v ngrok >/dev/null 2>&1; then
  echo "==> 使用 ngrok（需先 ngrok authtoken <你的令牌>）==>"
  ngrok http "$PORT"
else
  echo "未找到 cloudflared 或 ngrok。请先安装其一："
  echo ""
  echo "  # 方式 A（推荐，推荐 brew 安装）："
  echo "  brew install cloudflared"
  echo ""
  echo "  # 方式 B：ngrok（需注册 https://ngrok.com 获取 authtoken）"
  echo "  brew install ngrok && ngrok authtoken <你的令牌>"
  echo ""
  echo "安装后重新运行： zsh scripts/uat-expose.sh $PORT"
  exit 1
fi