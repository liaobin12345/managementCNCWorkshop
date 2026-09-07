#!/usr/bin/env python3
"""验证：参数留空提交后旧值被清掉，材料库参数真正生效"""
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

# 建程序（45 钢）+ 坐标
p = req('/programming/programs', {
    'partName': 'mat-param-verify', 'material': '45#钢', 'datum': 'right_face',
    'source': 'manual'}, token=token)
pid = p['id']
req(f'/programming/programs/{pid}/points', [
    {'seq': 1, 'type': 'face', 'x': 34, 'z': 0, 'note': ''},
    {'seq': 2, 'type': 'step', 'x': 34, 'z': -15, 'note': ''},
    {'seq': 3, 'type': 'step', 'x': 44, 'z': -15, 'note': ''},
    {'seq': 4, 'type': 'step', 'x': 44, 'z': -35, 'note': ''},
], 'PUT', token)

# 先提交一遍带旧默认值的参数（模拟老数据：rpm=800 feed=0.2 切深=2）
req(f'/programming/programs/{pid}', {
    'partName': 'mat-param-verify', 'material': '45#钢', 'toolPost': 'turret',
    'stockDia': 48, 'rpm': 800, 'feed': 0.2, 'perCutDepth': 2}, 'PUT', token)

# 再提交参数全空（模拟新版向导留空=自动；棒料直径必填保留）
req(f'/programming/programs/{pid}', {
    'partName': 'mat-param-verify', 'material': '45#钢', 'toolPost': 'turret',
    'stockDia': 48, 'rpm': None, 'feed': None, 'perCutDepth': None}, 'PUT', token)

# 确认 + 生成
req(f'/programming/programs/{pid}/confirm', {}, token=token)
g = req(f'/programming/programs/{pid}/generate', {}, token=token)
code = g['program']['gcode']

checks = [
    ('材料命中 45钢',      'VC ROUGH 180' in code),
    ('转速自动换算(180m/min@D34)', 'S1685' in code),
    ('切深=材料ap 2.0',    'G71 U2 R0.5' in code),
    ('无残留旧rpm S800',   'S800 M03' not in code),
]
ok = True
for name, passed in checks:
    print(('PASS ' if passed else 'FAIL ') + name)
    ok = ok and passed

m = re.search(r'\(CUT PARAMS: ([^)]+)\)', code)
print('头注释:', m.group(1) if m else '(无)')
sys.exit(0 if ok else 1)
