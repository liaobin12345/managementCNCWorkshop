#!/usr/bin/env python3
"""打印程序1精车段，检查倒角是否生成"""
import json, urllib.request

def req(path, data=None, method=None, token=None):
    headers = {'Content-Type': 'application/json'}
    if token:
        headers['Authorization'] = 'Bearer ' + token
    r = urllib.request.Request('http://localhost:5219/api' + path,
        data=json.dumps(data).encode() if data is not None else b'',
        headers=headers,
        method=method or ('POST' if data is not None else 'GET'))
    return json.load(urllib.request.urlopen(r, timeout=10))

tok = req('/auth/login', {'employeeNo': 'E004', 'password': '123456'})
token = tok.get('token') or tok.get('accessToken')
progs = req('/programming/programs', token=token)
items = progs if isinstance(progs, list) else progs.get('items', progs.get('data', []))
pid = [x for x in items if x.get('partName') == 'finish-cut-check'][-1]['id']
g = req(f'/programming/programs/{pid}', token=token)
code = g['gcode'] if 'gcode' in g else g['program']['gcode']
p1 = code.split('PROGRAM 2 OF 2')[0]
i = p1.find('FINISH')
print(p1[i:i+1000])
