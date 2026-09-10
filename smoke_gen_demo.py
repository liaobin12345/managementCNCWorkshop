#!/usr/bin/env python3
"""复现"demo_step_shaft.dxf → 生成 G 代码"全链路，作为生成器迭代/回归基线。

流程与 H5 一致：解析 DXF → 创建程序(带 StockDia) → confirm → generate。
用法：python3 smoke_gen_demo.py [--part-name X] [--stock 32] [--save 输出.txt]
"""
import base64, json, os, sys, urllib.request, argparse

API = "http://127.0.0.1:5219/api"
DXF = os.path.expanduser("~/Desktop/demo_step_shaft.dxf")

def login():
    d = json.dumps({'employeeNo': 'E004', 'password': '123456'}).encode()
    rq = urllib.request.Request(API + '/auth/login', data=d,
                                headers={'Content-Type': 'application/json'}, method='POST')
    return json.load(urllib.request.urlopen(rq, timeout=15))['token']

def http(method, path, token, data=None, raw=None, ctype=None):
    headers = {}
    if token: headers['Authorization'] = 'Bearer ' + token
    body = raw
    if ctype: headers['Content-Type'] = ctype
    elif data is not None: headers['Content-Type'] = 'application/json'; body = json.dumps(data).encode()
    rq = urllib.request.Request(API + path, data=body, headers=headers, method=method)
    return json.load(urllib.request.urlopen(rq, timeout=30))

def parse_dxf(token):
    dxf = open(DXF, 'rb').read()
    b = '---B'
    body = (f'--{b}\r\nContent-Disposition: form-data; name="file"; filename="demo_step_shaft.dxf"\r\n'
            f'Content-Type: application/dxf\r\n\r\n').encode() + dxf + b'\r\n--' + b.encode() + b'--\r\n'
    return http('POST', '/programming/dxf-parse', token, ctype=f'multipart/form-data; boundary={b}', raw=body)

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--part', default='DEMO-DXF-AUTO')
    ap.add_argument('--stock', type=float, default=32)
    ap.add_argument('--material', default='45#')
    ap.add_argument('--save', default=None)
    a = ap.parse_args()

    tok = login()
    pr = parse_dxf(tok)
    if not pr.get('success'):
        print('DXF parse failed:', pr.get('message')); sys.exit(2)
    pts = []
    for p in pr['points']:
        it = {'seq': p['seq'], 'type': p['type'], 'x': p['x'], 'z': p['z'],
              'note': p.get('note') or None}
        if p.get('chamfer') is not None: it['chamfer'] = p['chamfer']
        if p.get('threadPitch') is not None: it['threadPitch'] = p['threadPitch']
        if p.get('arcR') is not None:
            it['arcR'] = p['arcR']; it['arcDir'] = p.get('arcDir')
        pts.append(it)

    prog = http('POST', '/programming/programs', tok, {
        'partName': a.part, 'stockDia': a.stock, 'material': a.material,
        'processMode': 'auto', 'points': pts})
    pid = prog['id']
    http('POST', f'/programming/programs/{pid}/confirm', tok)
    gen = http('POST', f'/programming/programs/{pid}/generate', tok)
    parts = gen.get('GcodeParts') or gen.get('program', {}).get('gcodeParts') or \
        gen.get('program', {}).get('parts') or []
    if not parts:
        parts = [gen['program']['gcode']]
    text = '\n\n'.join(parts)
    print(text)
    if a.save:
        with open(a.save, 'w') as f: f.write(text)
        print('\n[saved]', a.save, file=sys.stderr)
    sys.exit(0)

if __name__ == '__main__':
    main()
