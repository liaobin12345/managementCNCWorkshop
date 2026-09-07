#!/bin/zsh
# 数控车编程助手（独立 UniApp 项目）开发脚本
# 用法：
#   zsh scripts/run-programming-app.sh dev        # H5 开发预览
#   zsh scripts/run-programming-app.sh build      # H5 打包
#   zsh scripts/run-programming-app.sh mp         # 微信小程序打包（产物 dist/build/mp-weixin）
#   zsh scripts/run-programming-app.sh mp-dev     # 微信小程序开发预览

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
MINIAPP_DIR="$PROJECT_ROOT/src/ManagementCNCWorkshop.ProgrammingApp"

export PATH="$PROJECT_ROOT/.tools/bin:$PATH"
export CI=true

cd "$MINIAPP_DIR"

case "${1:-dev}" in
  dev)    shift; npm run dev:h5 "$@";;
  build)  shift; npm run build:h5 "$@";;
  mp)     shift; npm run build:mp-weixin "$@";;
  mp-dev) shift; npm run dev:mp-weixin "$@";;
  *)
    echo "用法: $0 [dev|build|mp|mp-dev]"
    exit 1
    ;;
esac
