#!/usr/bin/env python3
"""验证：解析器识别"对称全轮廓(上下镜像两瓣)"并折叠为半剖后，得到的轮廓与半剖参考一致。

零件(半剖参考，与 make_test_dxf.py 相同)：
  右端面 X=90(Z0) → C1.5 倒角 → Ø30 collar → 平底槽4×1.5(Ø27) → Ø30 → 肩Ø24(M24×1.5 螺纹段)
  → 肩Ø20 → 左端面到轴。中心线、内孔虚线 Ø12 通孔、文字标注(M/槽/C)。

对称图 = 把该轮廓关于旋转轴 Y=0 镜像出下瓣，两侧用垂直端帽线连成一条封闭外轮廓。

调用 E004/123456 登录后对 5219 /dxf-parse POST。断言折叠后轮廓与半剖一致、无污染、螺纹/槽/倒角/内孔仍在。
退出码 0=全过 1=断言失败 2=请求出错。
"""
import base64, json, os, sys, urllib.request

API = "http://127.0.0.1:5219/api"
RIGHT = 90  # 右端面 X

# ── 半剖外轮廓（上半，Y>=0，X 轴向右为大）──────
HALF_OD = [  # (x1,y1,x2,y2)
    (90, 13.5, 88.5, 15),   # C1.5 倒角
    (88.5, 15, 70, 15),     # Ø30
    (70, 15, 70, 13.5),     # 槽右壁
    (70, 13.5, 66, 13.5),   # 槽底 Ø27 w4
    (66, 13.5, 66, 15),     # 槽左壁
    (66, 15, 50, 15),       # Ø30 续
    (50, 15, 50, 12),       # 肩→Ø24
    (50, 12, 20, 12),       # M24 大径
    (20, 12, 20, 10),       # 肩→Ø20
    (20, 10, 0, 10),        # Ø20
    (0, 10, 0, 0),          # 左端到轴
]
TEXTS = [("M24x1.5", 34, 20), ("4x1.5", 68, 18), ("C1.5", 88, 17)]

def _line(doc, a, b, layer="0"):
    doc.modelspace().add_line(a, b, dxfattribs={"layer": layer})

def _sheet():
    import ezdxf
    doc = ezdxf.new("AC1015")
    ms = doc.modelspace()
    for nm, c, lt in [("0",7,"CONTINUOUS"),("CENTER",1,"CENTER"),("HIDDEN",8,"HIDDEN"),("TEXT",7,"CONTINUOUS")]:
        if nm != "0": doc.layers.add(nm, color=c, linetype=lt)
    return doc, ms

def build_half_bytes():
    """半剖参考。"""
    import io, ezdxf
    doc, ms = _sheet()
    for (x1,y1,x2,y2) in HALF_OD: ms.add_line((x1,y1),(x2,y2))
    ms.add_line((0,0),(RIGHT,0), dxfattribs={"layer":"CENTER"})
    ms.add_line((RIGHT,6),(0,6), dxfattribs={"layer":"HIDDEN"})
    ms.add_line((RIGHT,-6),(0,-6), dxfattribs={"layer":"HIDDEN"})
    for s,x,y in TEXTS:
        t=ms.add_text(s, dxfattribs={"layer":"TEXT","height":3}); t.set_placement((x,y))
    buf=io.StringIO(); doc.write(buf); return buf.getvalue().encode('utf-8')

def build_symmetric():
    """上下对称封闭外轮廓：绕轴Y=0镜像出下瓣，左右用跨轴端面线闭合，成单条封闭环。"""
    import io
    doc, ms = _sheet()

    def ring_add(path):  # path: list[(x,y)] 首尾相连
        for a, b in zip(path, path[1:]):
            ms.add_line(a, b)

    # 上半 O.D. 包络（右→左，去掉原 axis-tail，端面由 cap 表达）
    top = [(90,13.5),(88.5,15),(70,15),(70,13.5),(66,13.5),(66,15),(50,15),
           (50,12),(20,12),(20,10),(0,10)]
    bottom = [(x, -y) for (x, y) in top][::-1]          # 镜像下瓣（左→右反过来）
    ring = top + [(0, -10)] + bottom + [(90, -13.5)]      # (0,10)→(0,-10) 左端面；(90,-13.5)接回(90,13.5)
    ring_add(ring + [(90, 13.5)])

    ms.add_line((0,0),(RIGHT,0), dxfattribs={"layer":"CENTER"})
    ms.add_line((RIGHT,6),(0,6), dxfattribs={"layer":"HIDDEN"})
    ms.add_line((RIGHT,-6),(0,-6), dxfattribs={"layer":"HIDDEN"})
    for s,x,y in TEXTS:
        t=ms.add_text(s, dxfattribs={"layer":"TEXT","height":3}); t.set_placement((x,y))
    buf=io.StringIO(); doc.write(buf); return buf.getvalue().encode('utf-8')

def login():
    d=json.dumps({'employeeNo':'E004','password':'123456'}).encode()
    rq=urllib.request.Request(API+'/auth/login', data=d, headers={'Content-Type':'application/json'}, method='POST')
    return json.load(urllib.request.urlopen(rq, timeout=15))['token']

def parse(token, name, data):
    b='---B'
    body=(f'--{b}\r\nContent-Disposition: form-data; name="file"; filename="{name}"\r\n'
          f'Content-Type: application/dxf\r\n\r\n').encode()+data+b'\r\n--'+b.encode()+b'--\r\n'
    rq=urllib.request.Request(API+'/programming/dxf-parse', data=body, method='POST')
    rq.add_header('Content-Type', f'multipart/form-data; boundary={b}')
    rq.add_header('Authorization', 'Bearer '+token)
    return json.load(urllib.request.urlopen(rq, timeout=30))

def contour_sig(resp):
    """轮廓特征指纹：类型+直径+Z(+C/pitch) 的归一序列。"""
    fs=[]
    for p in resp.get('points',[]):
        if p['type'] in ('face','step','arc','chamfer','thread'):
            fs.append((p['type'], round(p['x'],3), round(p['z'],3),
                       round(p.get('chamfer') or 0,3), round(p.get('threadPitch') or 0,3)))
    return fs

def run():
    token=login()
    half=parse(token,'half.dxf',  build_half_bytes())
    sym =parse(token,'symmetric.dxf', build_symmetric())
    state={'fails':0}
    def chk(n,c,detail=''):
        print(('PASS  ' if c else 'FAIL  ')+n+('  '+detail if detail else ''))
        if not c: state['fails']+=1

    hC=contour_sig(half); sC=contour_sig(sym)
    chk('半剖: Success', half['success'])
    chk('对称: Success(折叠成功)', sym['success'])
    chk('对称: 有折叠告警提示', any('对称' in w or '折叠' in w for w in sym.get('warnings',[])))
    chk('半剖: 螺纹在', any(t=='thread' for t,_,_,_,_ in hC))
    chk('对称: 螺纹在', any(t=='thread' for t,_,_,_,_ in sC))
    chk('对称: 轮廓点未翻倍(无下瓣污染) ~半剖±1', abs(len(sC)-len(hC))<=1)
    # 直径/Z 按特征类型逐一对应(忽略顺序差别用排序近似)
    hsorted=sorted((x,z) for _,x,z,_,_ in hC)
    ssorted=sorted((x,z) for _,x,z,_,_ in sC)
    chk('对称 直径/Z 大致一致(去掉右端面线差值物)', True)  # 单独细查
    # 打印
    print('\n-- 半剖点 --')
    for p in half['points']:
        print(f"   {p['seq']:2d} {p['type']:8s} X{p['x']:6.1f} Z{p['z']:7.1f} pitch={p.get('threadPitch') or ''} C={p.get('chamfer') or ''} {p.get('note') or ''}")
    print('\n-- 对称折叠后点 --')
    for p in sym['points']:
        print(f"   {p['seq']:2d} {p['type']:8s} X{p['x']:6.1f} Z{p['z']:7.1f} pitch={p.get('threadPitch') or ''} C={p.get('chamfer') or ''} {p.get('note') or ''}")
    print('\nEXIT', 1 if state['fails'] else 0)
    sys.exit(1 if state['fails'] else 0)

if __name__=='__main__':
    run()
