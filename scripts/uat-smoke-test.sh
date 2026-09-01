#!/bin/bash
# UAT / 上线前冒烟测试：对部署地址做全面的端到端接口检查
#
# 用法：
#   bash scripts/uat-smoke-test.sh                    # 默认测本机 http://localhost:5219/api
#   bash scripts/uat-smoke-test.sh https://your-url/api
#
# 说明：共约 60 项检查，覆盖 认证/微信登录/工人端/点检/报工/运营后台/权限隔离。
set -e
B="${1:-http://localhost:5219/api}"
PASS=0; FAIL=0; FAILS=""
chk() { local name="$1" expect="$2" actual="$3" extra="${4:-}"
  if [[ "$actual" =~ $expect ]]; then PASS=$((PASS+1)); printf "  PASS  %-46s %s%s\n" "$name" "$actual" "$extra";
  else FAIL=$((FAIL+1)); FAILS="$FAILS\n    - $name (要 $expect，得 $actual)"; printf "  FAIL  %-46s %s%s\n" "$name" "$actual" "$extra"; fi; }
jget() { python3 -c "import sys,json;d=json.load(sys.stdin);print($1)"; }
H() { echo "== $1 =="; }

H "认证"
R=$(curl -s -o /dev/null -w "%{http_code}" "$B/health"); chk "GET /health 匿名" 200 "$R"
TG=$(curl -s -X POST "$B/auth/login" -H "Content-Type: application/json" -d '{"employeeNo":"E900","password":"admin123"}' | jget "d['token']"); chk "login E900(管理员)" 200 "$([ -n "$TG" ]&&echo 200||echo 000)"
T1=$(curl -s -X POST "$B/auth/login" -H "Content-Type: application/json" -d '{"employeeNo":"E001","password":"123456"}' | jget "d['token']"); chk "login E001(操作工)" 200 "$([ -n "$T1" ]&&echo 200||echo 000)"
T3=$(curl -s -X POST "$B/auth/login" -H "Content-Type: application/json" -d '{"employeeNo":"E003","password":"123456"}' | jget "d['token']"); chk "login E003(质检员)" 200 "$([ -n "$T3" ]&&echo 200||echo 000)"
T4=$(curl -s -X POST "$B/auth/login" -H "Content-Type: application/json" -d '{"employeeNo":"E004","password":"123456"}' | jget "d['token']"); chk "login E004(技术员)" 200 "$([ -n "$T4" ]&&echo 200||echo 000)"
R=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$B/auth/login" -H "Content-Type: application/json" -d '{"employeeNo":"E900","password":"bad"}'); chk "错误密码→401" 401 "$R"
R=$(curl -s -X POST "$B/auth/wechat-login" -H "Content-Type: application/json" -d '{"code":"smoke-code"}'); chk "wechat-login(已绑定→token)" 200 "$(echo "$R" | jget "200 if d.get('token') else '000'")"
R=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$B/auth/wechat-bind" -H "Content-Type: application/json" -d '{"openid":"x","employeeNo":"E004","password":"bad"}'); chk "wechat-bind 错密码→401" 401 "$R"

H "工人端 (E001)"
R=$(curl -s -o /dev/null -w "%{http_code}" "$B/worker/me" -H "Authorization: Bearer $T1"); chk "me" 200 "$R"
for ep in workshops products equipments; do
  R=$(curl -s -o /dev/null -w "%{http_code}" "$B/worker/$ep" -H "Authorization: Bearer $T1"); chk "GET /worker/$ep" 200 "$R"
done
QR=$(curl -s "$B/admin/master/products" -H "Authorization: Bearer $TG" | jget "d[0]['qrCode']"); R=$(curl -s -o /dev/null -w "%{http_code}" "$B/worker/products/by-qrcode?code=$QR" -H "Authorization: Bearer $T1"); chk "products/by-qrcode" 200 "$R"
EQ=$(curl -s "$B/admin/master/equipments" -H "Authorization: Bearer $TG" | jget "d[0]['qrCode']"); R=$(curl -s -o /dev/null -w "%{http_code}" "$B/worker/equipments/by-qrcode?code=$EQ" -H "Authorization: Bearer $T1"); chk "equipments/by-qrcode" 200 "$R"
for ep in summary production/daily quality/process quality/daily equipment/daily; do
  R=$(curl -s -o /dev/null -w "%{http_code}" "$B/worker/stats/$ep" -H "Authorization: Bearer $T1"); chk "stats/$ep" 200 "$R"
done
for ep in work-reports/my equipment-time/recent maintenance/reminders/pending process/flows equipment-inspections/template "equipment-inspections/report?yearMonth=$(date +%Y-%m)&shift=Day"; do
  R=$(curl -s -o /dev/null -w "%{http_code}" "$B/worker/$ep" -H "Authorization: Bearer $T1"); chk "$ep" 200 "$R"
done

H "写操作闭环"
PID=$(curl -s "$B/admin/master/products" -H "Authorization: Bearer $TG" | jget "d[0]['id']"); WID=$(curl -s "$B/admin/master/workshops" -H "Authorization: Bearer $TG" | jget "d[0]['id']")
R=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$B/worker/work-reports/scan" -H "Content-Type: application/json" -H "Authorization: Bearer $T1" -d "{\"productId\":$PID,\"workshopId\":$WID,\"quantity\":3,\"qualifiedQty\":3}"); chk "POST 报工scan" 200 "$R"
EQID=$(curl -s "$B/admin/master/equipments" -H "Authorization: Bearer $TG" | jget "d[0]['id']")
TPL=$(curl -s "$B/worker/equipment-inspections/template" -H "Authorization: Bearer $T4")
ITEMS=$(echo "$TPL" | python3 -c "import sys,json;d=json.load(sys.stdin);d=d if isinstance(d,list) else [];print(json.dumps([{'itemNo':x['itemNo'],'status':'Ok'} for x in d]))")
R=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$B/worker/equipment-inspections" -H "Content-Type: application/json" -H "Authorization: Bearer $T4" -d "{\"equipmentId\":$EQID,\"inspectDate\":\"$(date +%F)\",\"shift\":\"Day\",\"items\":$ITEMS}"); chk "POST 点检提交(技术员)" 200 "$R"
R=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$B/worker/equipment-inspections" -H "Content-Type: application/json" -H "Authorization: Bearer $T1" -d "{\"equipmentId\":$EQID,\"inspectDate\":\"$(date +%F)\",\"shift\":\"Day\",\"items\":$ITEMS}"); chk "POST 点检(操作工→403)" 403 "$R"
R=$(curl -s -o /dev/null -w "%{http_code}" -X POST "$B/admin/maintenance/reminders/generate" -H "Authorization: Bearer $TG"); chk "POST 保养提醒生成" 200 "$R"

H "运营后台 (E900)"
for ep in master/workshops master/employees master/products master/equipments work-reports quality maintenance/plans maintenance/reminders/pending stats/production/daily stats/production/weekly stats/production/monthly quality/stats/daily quality/stats/weekly quality/stats/monthly process-flows process-cards "equipment-inspections/report?yearMonth=$(date +%Y-%m)"; do
  R=$(curl -s -o /dev/null -w "%{http_code}" "$B/admin/$ep" -H "Authorization: Bearer $TG"); chk "$ep" 200 "$R"
done

H "权限隔离"
R=$(curl -s -o /dev/null -w "%{http_code}" "$B/admin/master/workshops" -H "Authorization: Bearer $T1"); chk "操作工访问admin→403" 403 "$R"
R=$(curl -s -o /dev/null -w "%{http_code}" "$B/worker/me"); chk "无token→401" 401 "$R"

echo ""; echo "==================="; echo "通过 $PASS / 共 $((PASS+FAIL))"
if [ -n "$FAILS" ]; then printf "失败:%b\n" "$FAILS"; exit 1; fi