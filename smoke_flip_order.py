#!/usr/bin/env python3
"""验证 test-step-shaft 轮廓的翻面顺序：先车长圆柱端，第二道夹圆柱"""
import json, re, sys, urllib.request

BASE = 'http://localhost:5219/api'

def req(path, data=None, method=None, token=None):
    headers = {'Content-Type': 'application/json'}
    if token:
        headers['Authorization'] = 'Bearer ' + token
    r = urllib.request.Request(
        BASE + path,
        data=json.dumps(data).encode() if data is not None else b'',
        headers=headers,
        method=method or ('POST' if data is not None else 'GET'))
    with urllib.request.urlopen(r, timeout=20) as resp:
        return json.load(resp)

tok = req('/auth/login', {'employeeNo': 'E004', 'password': '123456'})
token = tok.get('token') or tok.get('accessToken')

# DXF 实际轮廓（右端面基准，右→左）：Ø34×15 → R5 → Ø44×15 → Ø36×15 → Ø30×40，总长90
pts = [
    {'seq': 1, 'type': 'face',  'x': 34, 'z': 0,    'note': ''},
    {'seq': 2, 'type': 'step',  'x': 34, 'z': -15,  'note': ''},
    {'seq': 3, 'type': 'arc',   'x': 44, 'z': -20,  'note': 'R5', 'arcR': 5, 'arcDir': 'ccw'},
    {'seq': 4, 'type': 'step',  'x': 44, 'z': -35,  'note': ''},
    {'seq': 5, 'type': 'step',  'x': 36, 'z': -35,  'note': ''},
    {'seq': 6, 'type': 'step',  'x': 36, 'z': -50,  'note': ''},
    {'seq': 7, 'type': 'step',  'x': 30, 'z': -50,  'note': ''},
    {'seq': 8, 'type': 'step',  'x': 30, 'z': -90,  'note': ''},
]

p = req('/programming/programs', {
    'partName': 'flip-order-verify', 'material': '45#钢', 'datum': 'right_face',
    'source': 'manual'}, token=token)
pid = p['id']
req(f'/programming/programs/{pid}/points', pts, 'PUT', token)
req(f'/programming/programs/{pid}', {
    'partName': 'flip-order-verify', 'material': '45#钢', 'toolPost': 'turret',
    'stockDia': 48}, 'PUT', token)
req(f'/programming/programs/{pid}/confirm', {}, token=token)
g = req(f'/programming/programs/{pid}/generate', {}, token=token)
code = g['program']['gcode']

# 分成两个程序（标记行是 "(========== PROGRAM x OF 2 - XXX SIDE ==========)"）
marker = 'PROGRAM 2 OF 2'
idx = code.find(marker)
if idx < 0:
    print('FAIL 未找到 PROGRAM 2 OF 2 标记，gcode 头 500 字:')
    print(code[:500])
    sys.exit(1)
first = code[:idx]
second = code[idx:]

checks = [
    ('程序1是 BACK 侧（先车长圆柱端）', 'PROGRAM 1 OF 2 - BACK SIDE' in first),
    ('程序1夹持注释为棒料Ø48', 'GRIP STOCK DIA 48' in first),
    ('程序1给出伸出量提示', 'STICKOUT' in first),
    ('程序1轮廓 Ø30→Ø36→Ø44角', 'X30' in first and 'X36' in first and 'X44' in first),
    ('程序1分界处棒料倒角（边界Z-55，倒角到Z-55.2/X47.6）', 'Z-55.2' in first and 'X47.6' in first),
    ('程序2是 FRONT 侧', 'PROGRAM 2 OF 2 - FRONT SIDE' in second),
    ('程序2夹持 Ø30 成品圆柱', 'GRIP FINISHED DIA 30' in second),
    ('程序2含 Ø44 与 R5 圆弧', 'X44' in second and ('G02' in second or 'G03' in second)),
    ('程序2端面倒角起点 X33.5', 'X33.5' in second),
]
ok = True
for name, passed in checks:
    print(('PASS ' if passed else 'FAIL ') + name)
    ok = ok and passed

print('\n--- 程序1 头 8 行 ---')
print('\n'.join(first.strip().splitlines()[:8]))
sys.exit(0 if ok else 1)
