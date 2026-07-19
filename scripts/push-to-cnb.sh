#!/bin/zsh
# 推送项目到 CNB 仓库
# 用法：zsh scripts/push-to-cnb.sh
#
# 推送前请先在 CNB 创建访问令牌：
# 1. 登录 https://cnb.cool
# 2. 个人设置 → 访问令牌 → 新建，勾选 repo-code:rw
# 3. 推送时 用户名填 cnb，密码填令牌

set -e

PROJECT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$PROJECT_ROOT"

REMOTE_URL="https://cnb.cool/liaobin1986/managementCNCWorkshop"

echo "==> 初始化 Git 仓库..."
if [ ! -d .git ]; then
  git init -b main
fi

echo "==> 配置远程仓库..."
if git remote get-url origin >/dev/null 2>&1; then
  git remote set-url origin "$REMOTE_URL"
else
  git remote add origin "$REMOTE_URL"
fi

echo "==> 添加文件..."
git add .

if git diff --cached --quiet; then
  echo "没有新改动需要提交"
else
  git commit -m "$(cat <<'EOF'
Initial commit: CNC 车间管理系统后端 V1

ASP.NET Core 8 + SQLite，含扫码报工、产量/质量统计、设备保养接口。
EOF
)"
fi

echo ""
echo "==> 推送到 CNB..."
echo "    远程地址: $REMOTE_URL"
echo "    用户名: cnb"
echo "    密码: 你的 CNB 访问令牌（不是登录密码）"
echo ""

git push -u origin main

echo ""
echo "完成！仓库地址: $REMOTE_URL"
