#!/bin/zsh
# CNC 工人端小程序（uni-app）开发脚本
# 用法：
#   zsh scripts/run-miniapp.sh dev        # H5 开发预览
#   zsh scripts/run-miniapp.sh build      # H5 打包
#   zsh scripts/run-miniapp.sh mp         # 微信小程序打包（产物 dist/build/mp-weixin）
#   zsh scripts/run-miniapp.sh mp-dev     # 微信小程序开发预览

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
MINIAPP_DIR="$PROJECT_ROOT/src/ManagementCNCWorkshop.MiniApp"

# 项目自带 node（系统未安装全局 node/npm 时使用）
export PATH="$PROJECT_ROOT/.tools/bin:$PATH"
# 跳过 uni-app 版本检查（沙箱/受限环境需 CI=true，正常终端也可用）
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
