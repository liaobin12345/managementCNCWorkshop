#!/bin/zsh
# 一次性修复 ~/.local 目录权限（需要输入 Mac 登录密码）
# 用法：zsh scripts/fix-nuget-permissions.sh

set -e

echo "修复 ~/.local 目录所有权（需要 sudo 密码）..."
sudo chown -R "$(whoami):staff" "$HOME/.local"
mkdir -p "$HOME/.local/share"
chmod 755 "$HOME/.local/share"

echo "完成！现在可以直接执行："
echo "  cd $(dirname "$0")/.."
echo "  dotnet restore"
