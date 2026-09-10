#!/bin/bash
# 探测 Vibe 助手 api-worker (:9182) 的模型列表与路由线索
# 用法: bash ~/Desktop/cnc/scripts/probe-vibe-worker.sh

echo "== 1. health =="
curl -s -m 3 http://127.0.0.1:9182/health
echo ""

echo "== 2. /v1/models (Bearer) =="
curl -s -m 3 -H "Authorization: Bearer local-backend" http://127.0.0.1:9182/v1/models | head -c 3000
echo ""

echo "== 3. /v1/models (x-api-key, Anthropic 风格) =="
curl -s -m 3 -H "x-api-key: local-backend" -H "anthropic-version: 2023-06-01" http://127.0.0.1:9182/v1/models | head -c 3000
echo ""

echo "== 4. 根路径（看服务自述） =="
curl -s -m 3 http://127.0.0.1:9182/ | head -c 1500
echo ""

echo "== 5. worker 目录里有什么（找配置/路由表） =="
W=$(ps -p 1152 -o args= 2>/dev/null | grep -o '/[^ ]*api-worker' | head -1)
echo "worker dir: $W"
if [ -n "$W" ] && [ -d "$W" ]; then
  ls -la "$W" 2>/dev/null | head -n 30
fi
