#!/usr/bin/env python3
"""验证精车段不再承担翻面倒角的大吃刀"""
import json, sys, urllib.request

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

pts = [
    {'seq': 1, 'type': 'face', 'x': 34, 'z': 0, 'note': ''},
    {'seq': 2, 'type': 'step', 'x': 34, 'z': -15, 'note': ''},
    {'seq': 3, 'type': 'arc', 'x': 44, 'z': -20, 'note': 'R5', 'arcR': 5, 'arcDir': 'ccw'},
    {'seq': 4, 'type': 'step', 'x': 44, 'z': -35, 'note': ''},
    {'seq': 5, 'type': 'step', 'x': 36, 'z': -35, 'note': ''},
    {'seq': 6, 'type': 'step', 'x': 36, 'z': -50, 'note': ''},
    {'seq': 7, 'type': 'step', 'x': 30, 'z': -50, 'note': ''},
    {'seq': 8, 'type': 'step', 'x': 30, 'z': -90, 'note': ''},
]

p = req('/programming/programs', {'partName': 'finish-cut-check', 'material': '45#钢', 'datum': 'right_face', 'source': 'manual'}, token=token)
pid = p['id']
req(f'/programming/programs/{pid}/points', pts, 'PUT', token)
req(f'/programming/programs/{pid}', {'partName': 'finish-cut-check', 'material': '45#钢', 'toolPost': 'turret', 'stockDia': 48}, 'PUT', token)
req(f'/programming/programs/{pid}/confirm', {}, token=token)
g = req(f'/programming/programs/{pid}/generate', {}, token=token)
code = g['program']['gcode']

# 按工序段拆程序1：找到 FINISH 段，检查大吃刀只允许在粗车段
prog1 = code.split('PROGRAM 2 OF 2')[0]
secs = prog1.split('(OP')
finish_sec = next((s for s in secs if 'FINISH FACE' in s), '')
rough_sec = next((s for s in secs if 'OD ROUGH' in s), '')

checks = [
    ('程序1是 BACK 侧', 'PROGRAM 1 OF 2 - BACK SIDE' in code),
    ('粗车段 Z 向超精车边界（车到 Z-55.5）', 'Z-55.5' in rough_sec),
    ('粗车段给棒料棱边倒角（X48 → Z-55.7）', 'X48 Z-55.7' in rough_sec),
    ('精车段倒角仍在（X43.6 → X44 Z-55.2，只收余量）', 'X43.6' in finish_sec and 'X44 Z-55.2' in finish_sec),
]

ok = True
for name, passed in checks:
    print(('PASS ' if passed else 'FAIL ') + name)
    ok = ok and passed

sys.exit(0 if ok else 1)
