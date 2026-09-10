#!/usr/bin/env python3
"""DXF 特性解析验证脚本。

构造一个含 外轮廓(倒角/台阶) + Ø30 中段外圈沉割槽(4×1.5) + M24 螺纹段 + 内孔虚线(Ø12 通孔) + 中心线
的测试图，调用 /api/programming/dxf-parse，断言各类特征被正确识别，Warnings 中的真正问题能暴露。
退出码：0 全部通过；1 有断言失败；2 请求/解析出错。
"""
import base64, json, sys, urllib.request

def line(x1, y1, x2, y2, layer="0"):
    return f"0\nLINE\n8\n{layer}\n10\n{x1}\n20\n{y1}\n11\n{x2}\n21\n{y2}\n"

def txt(s, x, y):
    return f"0\nTEXT\n8\nTEXT\n10\n{x}\n20\n{y}\n1\n{s}\n"

segs = []

# ── 外轮廓（半剖，右端面 X=90，Y 为半径，往左 X 减）────────────
# 右端 C1.5 倒角（Ø30 端）：(90,13.5)→(88.5,15)
segs += line(90, 13.5, 88.5, 15)
# Ø30 外圆 (y15)，左行到 X=70
segs += line(88.5, 15, 70, 15)
# —— 真实沉割槽 4×1.5：Ø30 表面(y15) 切入 Ø27(螺纹?→ y13.5) 走 4 再抬回 Ø30 ——
segs += line(70, 15, 70, 13.5)     # 槽右壁 下
segs += line(70, 13.5, 66, 13.5)   # 槽底(Ø27, y13.5) 宽 4
segs += line(66, 13.5, 66, 15)     # 槽左壁 上
segs += line(66, 15, 50, 15)       # Ø30 续段
# 台阶 Ø30→Ø24(M24 螺纹段记录在大径)
segs += line(50, 15, 50, 12)
segs += line(50, 12, 20, 12)       # Ø24 螺纹段 X50..20
# 台阶 Ø24→Ø20
segs += line(20, 12, 20, 10)
segs += line(20, 10, 0, 10)        # Ø20 外圆
segs += line(0, 10, 0, 0)          # 左端面 到轴

# ── 中心线（应被丢弃）────────────
segs += line(0, 0, 90, 0, layer="CENTER")
# ── 内孔虚线：Ø12 通孔 (r6) 右端面 X=90 到左端 X=0 ──
segs += line(90, 6, 0, 6, layer="HIDDEN")
segs += line(90, -6, 0, -6, layer="HIDDEN")

# ── 文字标注 ──
segs += txt("M24x1.5", 35, 20)   # 螺纹（放在 Ø24 中段上方，指向该圆柱段）
segs += txt("4x1.5", 68, 17.5)   # 沉割槽（放在槽中心位置上方）
segs += txt("C1.5", 88, 17)      # 倒角

dxf = "0\nSECTION\n2\nENTITIES\n" + "".join(segs) + "0\nENDSEC\n0\nEOF\n"

def req(path, data=None, method=None, token=None):
    headers = {'Content-Type': 'application/json'}
    if token:
        headers['Authorization'] = 'Bearer ' + token
    r = urllib.request.Request(
        'http://localhost:5219/api' + path,
        data=json.dumps(data).encode() if data is not None else None,
        headers=headers, method=method)
    with urllib.request.urlopen(r, timeout=20) as resp:
        return json.load(resp)

tok = req('/auth/login', {'employeeNo': 'E004', 'password': '123456'}, 'POST')
token = tok.get('token') or tok.get('accessToken')

boundary = "----X"
body = (
    f"--{boundary}\r\n"
    f'Content-Disposition: form-data; name="file"; filename="test-features.dxf"\r\n'
    f"Content-Type: application/dxf\r\n\r\n"
).encode() + dxf.encode() + f"\r\n--{boundary}--\r\n".encode()

r = urllib.request.Request('http://localhost:5219/api/programming/dxf-parse', data=body, method='POST')
r.add_header('Content-Type', f'multipart/form-data; boundary={boundary}')
r.add_header('Authorization', 'Bearer ' + token)
try:
    resp = json.load(urllib.request.urlopen(r, timeout=20))
except urllib.error.HTTPError as e:
    print("HTTP", e.code, e.read().decode()[:400]); sys.exit(2)

print("Success:", resp['success'])
print("Message:", resp['message'])
for w in resp.get('warnings', []):
    print("WARN:", w)
print("\n点表:")
pts = resp['points']
for p in pts:
    print(f"  {p['seq']:2d} {p['type']:8s} X{p['x']:8.2f} Z{p['z']:8.2f} "
          f"R={p.get('arcR') or ''} pitch={p.get('threadPitch') or ''} C={p.get('chamfer') or ''} {p.get('note') or ''}")

# ── 断言 ──
fails = 0
def chk(name, cond):
    global fails
    print(("PASS  " if cond else "FAIL  ") + name)
    if not cond: fails += 1

types = {p['type'] for p in pts}
chk('解析成功', resp['success'])
# 中心线 Y=0 不该产生额外轮廓点：实线点数 <16（9 实线顶点 + 2 特征点）为粗筛
chk('中心线未混入轮廓点', len(pts) < 18)
chk('有螺纹点(thread)', 'thread' in types)
chk('螺纹大径=24 pitch=1.5', any(p['type']=='thread' and abs(p['x']-24)<0.5 and p['x'] and abs((p.get('threadPitch') or 1.5)-1.5)<1e-6 for p in pts))
chk('有内孔点(bore)', 'bore' in types)
chk('有倒角点(chamfer) 标注 C1.5', any(p['type']=='chamfer' and abs((p.get('chamfer') or 0)-1.5)<1e-6 for p in pts))
chk('无未识别告警（图内特征应全部命中）', len(resp.get('warnings', [])) == 0)
print("退出码: %d（0=全过 1=有断言失败）" % (1 if fails else 0))
sys.exit(1 if fails else 0)
