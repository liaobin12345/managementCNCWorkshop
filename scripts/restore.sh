#!/bin/zsh
# 修复 NuGet 权限问题并还原依赖
# 用法：在项目根目录执行  zsh scripts/restore.sh

set -e

PROJECT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$PROJECT_ROOT"

# 把 NuGet 缓存放到项目目录，绕过 ~/.local/share 权限问题
export XDG_DATA_HOME="$PROJECT_ROOT/.xdg"
export NUGET_PACKAGES="$PROJECT_ROOT/.nuget/packages"
export DOTNET_CLI_HOME="$PROJECT_ROOT/.dotnet-cli"

echo "==> 还原 NuGet 包..."
dotnet restore

echo "==> 编译项目..."
dotnet build

echo ""
echo "完成！启动命令："
echo "  dotnet run --project src/ManagementCNCWorkshop.Api"
