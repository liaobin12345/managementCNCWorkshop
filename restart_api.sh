#!/bin/bash
# 重启 CNC API (:5219) 并确保用最新编译产物
kill $(lsof -ti tcp:5219 -sTCP:LISTEN) 2>/dev/null
sleep 1
cd /Users/liaobin/Desktop/cnc/src/ManagementCNCWorkshop.Api || exit 1
dotnet build ManagementCNCWorkshop.Api.csproj -v q --nologo | tail -2
ASPNETCORE_URLS="http://0.0.0.0:5219" ASPNETCORE_ENVIRONMENT="Development" \
  nohup ./bin/Debug/net8.0/ManagementCNCWorkshop.Api > /tmp/cnc_api.log 2>&1 &
sleep 4
curl -s -m 5 http://localhost:5219/api/health && echo " <- API OK"
