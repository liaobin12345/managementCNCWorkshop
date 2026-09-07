using System.Globalization;
using System.Text;
using ManagementCNCWorkshop.Api.Models.Dtos;

namespace ManagementCNCWorkshop.Api.Services.Programming;

/// <summary>
/// DXF 轮廓解析器。
/// 读取 DXF 文本中的 LINE / ARC / LWPOLYLINE 实体，把线段按端点首尾串联成一条
/// 连续轮廓，再采样成坐标点。约定：
///   - 图纸 X 轴 = 零件轴向（右为正），Y 轴 = 半径（直径 = 2 × Y）；
///   - 自动以"最右侧（X 最大）"为右端面并把该点归零为 Z0；
///   - 只取外轮廓连续链，尺寸线/中心线等会被尽可能跳过。
/// 圆弧段保留半径与方向（cw/ccw），供 G 代码生成使用。
/// </summary>
public class DxfContourParser
{
    private const double Eps = 0.01; // 端点容差（mm）

    private readonly record struct P2(double X, double Y);

    private sealed class Seg
    {
        public P2 S;
        public P2 E;
        public double R;      // 0 = 直线段
        public string Dir = ""; // cw / ccw（圆弧段）
    }

    public DxfParseResponse Parse(string? fileName, byte[] content)
    {
        try
        {
            var segs = ParseSegments(content);
            if (segs.Count == 0)
                return Fail("未在 DXF 中找到 LINE / ARC / LWPOLYLINE 实体");

            var chain = Chain(segs);
            if (chain.Count < 2)
                return Fail("无法串联成连续轮廓：请确认图纸只含外轮廓线，且相邻线段首尾相接");

            var points = BuildPoints(chain);
            if (points.Count < 2)
                return Fail("轮廓点太少，请检查图纸比例或只保留外轮廓");

            return new DxfParseResponse
            {
                Success = true,
                Message = $"识别到 {points.Count} 个轮廓点（已按右端面归零 Z0，请核对轴向方向与直径）",
                Points = points,
            };
        }
        catch (Exception ex)
        {
            return Fail("DXF 解析失败：" + ex.Message);
        }
    }

    private static DxfParseResponse Fail(string msg) =>
        new() { Success = false, Message = msg, Points = new List<CncContourPointDto>() };

    // ───────────────────── 实体解析 ─────────────────────

    private static List<Seg> ParseSegments(byte[] content)
    {
        string text;
        try { text = Encoding.UTF8.GetString(content); }
        catch { text = Encoding.Latin1.GetString(content); }

        var lines = text.Replace("\r", "").Split('\n');
        var pairs = new List<(int Code, string Val)>();
        for (var i = 0; i + 1 < lines.Length; i += 2)
        {
            if (int.TryParse(lines[i].Trim(), out var c))
                pairs.Add((c, lines[i + 1].Trim()));
        }

        // 按 ENTITIES 段切分成实体（code==0 即实体边界，code==2 是段名）
        var entities = new List<(string Type, List<(int C, string V)>)>();
        List<(int C, string V)>? cur = null;
        string curType = "";
        var inEntities = false;

        foreach (var (code, val) in pairs)
        {
            if (code == 0)
            {
                if (cur is not null && inEntities) entities.Add((curType, cur));
                cur = null;
                curType = val;
                continue;
            }
            if (code == 2)
            {
                if (val == "ENTITIES") inEntities = true;
                else if (val == "ENDSEC") inEntities = false;
                continue;
            }
            if (inEntities)
            {
                cur ??= new List<(int, string)>();
                cur.Add((code, val));
            }
        }
        if (cur is not null && inEntities) entities.Add((curType, cur));

        var segs = new List<Seg>();
        foreach (var (type, props) in entities)
        {
            switch (type)
            {
                case "LINE": TryLine(props, segs); break;
                case "ARC": TryArc(props, segs); break;
                case "LWPOLYLINE": TryLwPolyline(props, segs); break;
            }
        }
        return segs;
    }

    private static void TryLine(List<(int C, string V)> p, List<Seg> segs)
    {
        var x1 = Num(p, 10); var y1 = Num(p, 20);
        var x2 = Num(p, 11); var y2 = Num(p, 21);
        if (x1 is null || y1 is null || x2 is null || y2 is null) return;
        var s = new P2(x1.Value, y1.Value);
        var e = new P2(x2.Value, y2.Value);
        if (Dist(s, e) < 1e-6) return; // 跳过零长度段（不参与轮廓）
        segs.Add(new Seg { S = s, E = e });
    }

    private static void TryArc(List<(int C, string V)> p, List<Seg> segs)
    {
        var cx = Num(p, 10); var cy = Num(p, 20); var r = Num(p, 40);
        var a1 = Num(p, 50); var a2 = Num(p, 51);
        if (cx is null || cy is null || r is null || a1 is null || a2 is null) return;
        var rad = Math.PI / 180.0;
        var s = new P2(cx.Value + r.Value * Math.Cos(a1.Value * rad),
                       cy.Value + r.Value * Math.Sin(a1.Value * rad));
        var e = new P2(cx.Value + r.Value * Math.Cos(a2.Value * rad),
                       cy.Value + r.Value * Math.Sin(a2.Value * rad));
        segs.Add(new Seg { S = s, E = e, R = r.Value, Dir = "ccw" });
    }

    private static void TryLwPolyline(List<(int C, string V)> p, List<Seg> segs)
    {
        var pts = new List<P2>();
        var bulges = new List<double>();
        P2? cur = null;
        var bulge = 0.0;
        foreach (var (code, val) in p)
        {
            if (code == 10 && cur is null) cur = new P2(NumS(val), 0);
            else if (code == 20 && cur is { } pt) { cur = pt with { Y = NumS(val) }; }
            else if (code == 10 && cur is { })
            {
                pts.Add(cur.Value);
                bulges.Add(bulge);
                cur = new P2(NumS(val), 0);
                bulge = 0.0;
            }
            else if (code == 42) bulge = NumS(val);
        }
        if (cur is { } last) { pts.Add(last); bulges.Add(bulge); }
        if (pts.Count < 2) return;

        for (var i = 0; i < pts.Count - 1; i++)
            AddPolySeg(pts[i], pts[i + 1], bulges[i], segs);
    }

    private static void AddPolySeg(P2 a, P2 b, double bulge, List<Seg> segs)
    {
        if (Math.Abs(bulge) < 1e-6)
        {
            segs.Add(new Seg { S = a, E = b });
            return;
        }
        var theta = 4 * Math.Atan(bulge);
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var chord = Math.Sqrt(dx * dx + dy * dy);
        if (chord < 1e-6) return; // 跳过零长度段
        if (Math.Abs(bulge) >= 1e-6 && Math.Abs(Math.Sin(theta / 2)) < 1e-9) return;
        var r = chord / (2 * Math.Sin(Math.Abs(theta) / 2));
        segs.Add(new Seg { S = a, E = b, R = r, Dir = bulge > 0 ? "ccw" : "cw" });
    }

    // ───────────────────── 轮廓串联 ─────────────────────

    private static List<(Seg Seg, bool Reversed)> Chain(List<Seg> segs)
    {
        var used = new bool[segs.Count];
        // 起点：选 X 最大（轴向最右）、并列时 Y 最大（外圆上缘）
        P2 start = default;
        var startIdx = -1;
        var startIsEnd = false;
        for (var k = 0; k < segs.Count; k++)
        {
            foreach (var (pt, isEnd) in new[] { (segs[k].S, false), (segs[k].E, true) })
            {
                if (startIdx < 0 || pt.X > start.X + Eps || (Math.Abs(pt.X - start.X) < Eps && pt.Y > start.Y))
                {
                    start = pt; startIdx = k; startIsEnd = isEnd;
                }
            }
        }
        if (startIdx < 0) return new List<(Seg, bool)>();

        var result = new List<(Seg, bool)>();
        result.Add((segs[startIdx], startIsEnd)); // 起点在该段 E 则反向遍历
        used[startIdx] = true;
        var cur = startIsEnd ? segs[startIdx].S : segs[startIdx].E;

        while (true)
        {
            var bestK = -1;
            var bestIsEnd = false;
            double bestD = Eps + 0.5;
            for (var k = 0; k < segs.Count; k++)
            {
                if (used[k]) continue;
                var dS = Dist(cur, segs[k].S);
                var dE = Dist(cur, segs[k].E);
                if (dS < bestD) { bestD = dS; bestK = k; bestIsEnd = false; }
                if (dE < bestD) { bestD = dE; bestK = k; bestIsEnd = true; }
            }
            if (bestK < 0) break;
            used[bestK] = true;
            result.Add((segs[bestK], bestIsEnd));
            cur = bestIsEnd ? segs[bestK].S : segs[bestK].E;
            if (Dist(cur, start) < Eps) break; // 闭合
        }
        return result;
    }

    // ───────────────────── 采样坐标点 ─────────────────────

    private static List<CncContourPointDto> BuildPoints(List<(Seg Seg, bool Reversed)> chain)
    {
        // 链条物理起点 = 第一段遍历方向的起点
        var (firstSeg, firstRev) = chain[0];
        var firstPt = firstRev ? firstSeg.E : firstSeg.S;
        var xMax = firstPt.X; // 右端面基准（起刀选的是 X 最大点）
        var pts = new List<CncContourPointDto>();
        var seq = 1;

        CncContourPointDto Make(P2 pt, string type, double? r, string? dir, string? note)
        {
            var z = pt.X - xMax; // 右端面归零
            var dia = 2 * pt.Y;  // Y 为半径
            return new CncContourPointDto
            {
                Seq = seq,
                Type = type,
                X = Dec(dia),
                Z = Dec(z),
                ArcR = r is null ? null : Dec(r.Value),
                ArcDir = dir,
                Note = note,
                Confidence = 1m,
                Verified = false,
                Manual = false,
                Source = "dxf",
            };
        }

        foreach (var (seg, rev) in chain)
        {
            var s = rev ? seg.E : seg.S;
            var e = rev ? seg.S : seg.E;
            if (seq == 1)
            {
                pts.Add(Make(s, "face", null, null, "端面"));
                seq++;
            }
            var isArc = seg.R > 0;
            if (isArc)
            {
                var dir = rev ? Flip(seg.Dir) : seg.Dir;
                pts.Add(Make(e, "arc", seg.R, dir, $"R{Fmt(seg.R)}"));
            }
            else
            {
                pts.Add(Make(e, "step", null, null, null));
            }
            seq++;
        }
        return pts;
    }

    private static string Flip(string d) => d == "cw" ? "ccw" : "cw";

    // ───────────────────── 工具 ─────────────────────

    private static double? Num(List<(int C, string V)> p, int code)
    {
        for (var i = p.Count - 1; i >= 0; i--)
            if (p[i].C == code && double.TryParse(p[i].V, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                return v;
        return null;
    }

    private static double NumS(string v) =>
        double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var r) ? r : 0;

    private static double Dist(P2 a, P2 b)
    {
        var dx = a.X - b.X; var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static decimal Dec(double v) => decimal.Round((decimal)v, 3);

    private static string Fmt(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
}