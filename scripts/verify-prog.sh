#!/bin/zsh
# 验证 /prog H5 部署 + 后端启动（用法：zsh scripts/verify-prog.sh）
API_DIR="$HOME/Desktop/cnc/src/ManagementCNCWorkshop.Api"

lsof -ti tcp:5219 | xargs kill -9 2>/dev/null
cd "$API_DIR" || exit 1

export ASPNETCORE_URLS="http://0.0.0.0:5219"
export ASPNETCORE_ENVIRONMENT="Development"
export Database__Provider=sqlite
export ConnectionStrings__DefaultConnection="Data Source=$API_DIR/workshop.db"

dotnet run --no-launch-profile > /tmp/cnc-api-verify.log 2>&1 &

# 最多等 40 秒直到端口可访问
for i in {1..40}; do
  curl -s -m 2 -o /dev/null http://localhost:5219/swagger/index.html && break
  sleep 1
done

echo "prog index: $(curl -s -m 3 -o /dev/null -w '%{http_code}' http://localhost:5219/prog/index.html)"
echo "prog js:    $(curl -s -m 3 -o /dev/null -w '%{http_code}' http://localhost:5219/prog/assets/index-Dozd_xn9.js)"
echo "---index head---"
curl -s -m 3 http://localhost:5219/prog/index.html | head -c 400
echo ""
echo "---api---"
curl -s -m 3 http://localhost:5219/api/programming/post-profiles | head -c 300
echo ""
