#!/usr/bin/env python3
"""端到端自测：登录 → 建程序 → 存坐标 → 确认 → 生成，打印 G 代码"""
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
    try:
        with urllib.request.urlopen(r, timeout=20) as resp:
            return json.load(resp)
    except urllib.error.HTTPError as e:
        body = e.read().decode('utf-8', 'replace')
        print(f'!! {method or "POST"} {path} -> HTTP {e.code}')
        print('   返回:', body[:500])
        raise

def main():
    # 1. 登录（编程模块要求账号挂在具体车间下，优先编程技术员 E004）
    tok, wid = None, 0
    for creds in ({'employeeNo': 'E004', 'password': '123456'},
                  {'employeeNo': 'E001', 'password': '123456'},
                  {'employeeNo': 'E900', 'password': 'admin123'}):
        try:
            login = req('/auth/login', creds)
            tok = login.get('token') or login.get('accessToken')
            wid = (login.get('employee') or {}).get('workshopId') or 0
            if tok and wid > 0:
                print(f"登录 OK（工号 {creds['employeeNo']}，车间 {wid}）")
                break
            print(f"跳过 {creds['employeeNo']}：车间={wid}")
        except Exception as e:
            print('登录尝试失败:', e)
    if not tok or wid <= 0:
        print('没有带车间的可用账号，请检查种子数据')
        sys.exit(1)

    # 2. 建程序（模拟 test-step-shaft-simple.dxf：34 颈 + 44 法兰 + 32 尾）
    p = req('/programming/programs', {
        'partName': 'smoke-verify', 'drawingNo': '', 'material': '304',
        'datum': 'right_face', 'source': 'manual'}, token=tok)
    pid = p['id']
    print('建程序 id =', pid)

    # 3. 存坐标
    pts = [
        {'seq': 1, 'type': 'face', 'x': 34, 'z': 0,   'note': '端面', 'verified': False},
        {'seq': 2, 'type': 'step', 'x': 34, 'z': -15, 'note': '',     'verified': False},
        {'seq': 3, 'type': 'step', 'x': 44, 'z': -15, 'note': '',     'verified': False},
        {'seq': 4, 'type': 'step', 'x': 44, 'z': -35, 'note': '',     'verified': False},
        {'seq': 5, 'type': 'step', 'x': 32, 'z': -35, 'note': '',     'verified': False},
        {'seq': 6, 'type': 'step', 'x': 32, 'z': -60, 'note': '',     'verified': False},
    ]
    req(f'/programming/programs/{pid}/points', pts, 'PUT', tok)
    print('坐标已保存')

    # 3.5 参数（棒料直径必填）
    req(f'/programming/programs/{pid}', {
        'partName': 'smoke-verify', 'material': '304', 'toolPost': 'turret',
        'stockDia': 48, 'stockLen': 60.2}, 'PUT', tok)
    print('参数已保存（棒料 Ø48）')

    # 4. 确认
    c = req(f'/programming/programs/{pid}/confirm', {}, token=tok)
    print('确认后状态 =', c.get('status'))

    # 5. 生成
    g = req(f'/programming/programs/{pid}/generate', {}, token=tok)
    prog = g.get('program', {})
    print('生成后状态 =', prog.get('status'))
    print('===== G 代码 =====')
    print(prog.get('gcode', ''))

if __name__ == '__main__':
    main()
