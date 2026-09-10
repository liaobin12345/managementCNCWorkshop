using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ManagementCNCWorkshop.Api.Models.Dtos;

namespace ManagementCNCWorkshop.Api.Services.Programming;

/// <summary>
/// DXF 轮廓/特征解析器。读取 LINE / ARC / LWPOLYLINE / POLYLINE 实体：
///   1. 按图层名与线型把实体分为 实线轮廓 / 虚线隐藏线 / 中心线，中心线直接丢弃；
///   2. 实线串成连续外轮廓，采样成坐标点（右端面归零 Z0，Y 为半径）；
///   3. 虚线水平线（上下对称成对）识别为内孔（Ø、通/盲孔）；
///   4. TEXT / MTEXT / DIMENSION 文字标注：M20×1.5 → 外螺纹点（查粗牙螺距表），
///      3×1 → 沉割槽备注，C1 / 2×45° → 倒角备注与 Chamfer 字段；
///   5. 识别不到对应的标注会放进 Warnings，不阻断解析。
/// </summary>
public class DxfContourParser
{
    private const double Eps = 0.01; // 端点容差（mm）

    // ── 对称全轮廓 → 半剖折叠（镜像识别）阈值 ──
    private const double SymAxisBand = 0.5;     // 判定"明显高于/低于轴"的半带宽
    private const int    SymBelowMin = 3;        // 轴下沉点 >=3 才认为存在下瓣
    private const double SymAboveRatio = 0.6;    // 上瓣端点至少为下瓣的 0.6 倍
    private const double SymExcursionRatio = 1.25; // 上/下相对轴的极差幅值比须 ≤25% 偏差

    private readonly record struct P2(double X, double Y);

    private sealed class Seg
    {
        public P2 S;
        public P2 E;
        public double R;      // 0 = 直线段
        public string Dir = ""; // cw / ccw（圆弧段）
        public string Layer = "";
        public string LineType = "";
        public string Kind = "solid"; // solid / hidden / center
    }

    private sealed record Txt(string Text, double X, double Y);

    // 常用米制粗牙螺距表
    private static readonly Dictionary<int, decimal> CoarsePitch = new()
    {
        [3] = 0.5m, [4] = 0.7m, [5] = 0.8m, [6] = 1m, [7] = 1m, [8] = 1.25m, [9] = 1.25m,
        [10] = 1.5m, [11] = 1.5m, [12] = 1.75m, [14] = 2m, [16] = 2m, [18] = 2.5m,
        [20] = 2.5m, [22] = 2.5m, [24] = 3m, [27] = 3m, [30] = 3.5m, [33] = 3.5m, [36] = 4m, [39] = 4m,
        [42] = 4.5m, [45] = 4.5m, [48] = 5m, [52] = 5m, [56] = 5.5m, [60] = 5.5m,
    };

    private static readonly Regex ReMetricThread =
        new(@"(?<![A-Za-z0-9.])M\s*(\d{1,3}(?:\.\d+)?)\s*(?:[×xX*]\s*(\d{1,2}(?:\.\d+)?))?(?![\d.])",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ReTrapThread =
        new(@"(?<![A-Za-z0-9.])Tr\s*(\d{2,3}(?:\.\d+)?)\s*[×xX]\s*(\d{1,2}(?:\.\d+)?)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ReGroove =
        new(@"(?<![A-Za-z0-9.])(\d{1,2}(?:\.\d+)?)\s*[×xX]\s*(\d{1,2}(?:\.\d+)?)(?![\d.])",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ReChamfer =
        new(@"(?<![A-Za-z0-9.])C\s*(\d{1,2}(?:\.\d+)?)(?![\d.])",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public DxfParseResponse Parse(string? fileName, byte[] content)
    {
        try
        {
            var (segs, texts) = ParseEntities(content);
            if (segs.Count(s => s.Kind != "center") == 0 && texts.Count == 0)
                return Fail("未在 DXF 中找到可识别的实体（LINE / ARC / POLYLINE / TEXT）");

            var solid = segs.Where(s => s.Kind == "solid").ToList();
            var hidden = segs.Where(s => s.Kind == "hidden").ToList();

            if (solid.Count == 0)
                return Fail("未找到实线轮廓：请检查图纸图层是否都被识别为中心线/虚线");

            var warnings = new List<string>();
            var symAxis = EstimateAxis(solid);
            var isSym = HasMirroredLobe(solid, symAxis);
            var feed = isSym ? FoldToUpperHalf(solid, symAxis) : solid;
            if (isSym)
                warnings.Add("检测到完整对称轮廓（上/下两瓣），已自动折叠为半剖处理——请核对轴向与直径");

            var chain = Chain(feed);
            if (chain.Count < 2)
                return Fail("无法串联成连续轮廓：请确认图纸只含外轮廓线，且相邻线段首尾相接");

            var points = BuildPoints(chain);
            var features = new List<string>();

            // xMax = 链条最右点（右端面基准）
            var (firstSeg, firstRev) = chain[0];
            var xMax = (firstRev ? firstSeg.E : firstSeg.S).X;
            var zLeftMin = points.Min(p => p.Z); // 轮廓最左（零件左端）

            // ── 内孔：虚线水平线上下成对 ──
            var bores = DetectBores(hidden, xMax, zLeftMin, warnings);
            foreach (var b in bores)
            {
                features.Add(b.Note!);
                points.Add(new CncContourPointDto
                {
                    Type = "bore", X = b.X, Z = b.Z, Note = b.Note,
                    Confidence = 0.9m, Source = "dxf",
                });
            }

            // ── 文字标注：螺纹 / 槽 / 倒角 ──
            RecognizeTexts(texts, points, xMax, warnings, features);

            // Seq 重排
            for (var i = 0; i < points.Count; i++)
                points[i].Seq = i + 1;

            var msg = $"识别到外轮廓 {points.Count(p => p.Type is "face" or "step" or "arc" or "chamfer")} 点";
            if (features.Count > 0) msg += "，" + string.Join("、", features);
            msg += "（已按右端面归零 Z0，请核对轴向方向与直径）";

            return new DxfParseResponse
            {
                Success = true,
                Message = msg,
                Warnings = warnings,
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

    // ───────────────────── 对称全轮廓 → 半剖折叠（镜像识别） ─────────────────────

    /// <summary>零件旋转轴估计：取全部实线端点 Y 幅值中点。</summary>
    private static double EstimateAxis(List<Seg> solid)
    {
        double yLo = double.MaxValue, yHi = double.MinValue;
        foreach (var s in solid)
        {
            yLo = Math.Min(yLo, Math.Min(s.S.Y, s.E.Y));
            yHi = Math.Max(yHi, Math.Max(s.S.Y, s.E.Y));
        }
        return (yLo + yHi) / 2;
    }

    /// <summary>判别是否为"对称全轮廓"（上下镜像两瓣）。半剖图只含单个轴邻居点，返回 false → 不折叠。</summary>
    private static bool HasMirroredLobe(List<Seg> solid, double axis)
    {
        int below = 0, above = 0;
        double belowMax = 0, aboveMax = 0;
        foreach (var s in solid)
        {
            foreach (var y in new[] { s.S.Y, s.E.Y })
            {
                if (y < axis - SymAxisBand) { below++; belowMax = Math.Max(belowMax, axis - y); }
                else if (y > axis + SymAxisBand) { above++; aboveMax = Math.Max(aboveMax, y - axis); }
            }
        }
        if (below < SymBelowMin) return false;                      // 无足够轴下沉点 → 单瓣半剖
        if (above < SymAboveRatio * below) return false;            // 上瓣太少
        // 上/下相对轴的极差幅值须接近（对称）；差很大则不是对半镜面
        if (aboveMax <= 0 || belowMax <= 0) return false;
        var ratio = Math.Max(aboveMax, belowMax) / Math.Min(aboveMax, belowMax);
        return ratio <= SymExcursionRatio;
    }

    /// <summary>折叠成半剖：只保留上半加工包络。捕获轴跨接缝段（端面），把压到轴下的一端夹回轴 Y。</summary>
    private static List<Seg> FoldToUpperHalf(List<Seg> solid, double axis)
    {
        var kept = new List<Seg>();
        foreach (var s in solid)
        {
            var top = Math.Max(s.S.Y, s.E.Y);
            var low = Math.Min(s.S.Y, s.E.Y);
            if (low >= axis - SymAxisBand)
            {
                kept.Add(s);            // 整段在上半包络（O.D./台阶/槽/倒角/含贴在轴上的点）
            }
            else if (top > axis + SymAxisBand)
            {
                // 跨轴接缝（端面/回程）：保留并把压到轴下的一端夹回轴，避免在轴下造伪径
                var copy = new Seg { S = s.S, E = s.E, R = s.R, Dir = s.Dir, Kind = s.Kind };
                if (copy.S.Y < axis) copy.S = copy.S with { Y = axis };
                if (copy.E.Y < axis) copy.E = copy.E with { Y = axis };
                kept.Add(copy);
            }
            // else 整段在轴下方 = 冗余下瓣 → 丢弃
        }
        return kept;
    }

    // ───────────────────── 实体解析 ─────────────────────

    private static (List<Seg> Segs, List<Txt> Texts) ParseEntities(byte[] content)
    {
        string text;
        try { text = Encoding.UTF8.GetString(content); }
        catch { text = Encoding.Latin1.GetString(content); }

        var lines = text.Replace("\r", "").Split('\n');
        // 游标式读取：组码行 + 值行成对消费；空行/杂项跳过并自动重新同步
        var pairs = new List<(int Code, string Val)>();
        for (var i = 0; i < lines.Length; i++)
        {
            if (!int.TryParse(lines[i].Trim(), out var c)) continue;
            if (i + 1 >= lines.Length) break;
            pairs.Add((c, lines[i + 1].Trim()));
            i++;
        }

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
        var texts = new List<Txt>();
        List<P2>? poly = null;      // POLYLINE 顶点累积
        List<double>? polyBulge = null;
        string polyKind = "solid";

        foreach (var (type, props) in entities)
        {
            var kind = ClassifyKind(props);
            switch (type)
            {
                case "LINE": TryLine(props, kind, segs); break;
                case "ARC": TryArc(props, kind, segs); break;
                case "LWPOLYLINE": TryLwPolyline(props, kind, segs); break;
                case "POLYLINE":
                    poly = new List<P2>();
                    polyBulge = new List<double>();
                    polyKind = kind;
                    break;
                case "VERTEX":
                    if (poly is not null)
                    {
                        var vx = Num(props, 10);
                        var vy = Num(props, 20);
                        if (vx is not null && vy is not null)
                        {
                            poly.Add(new P2(vx.Value, vy.Value));
                            polyBulge!.Add(Num(props, 42) ?? 0);
                        }
                    }
                    break;
                case "SEQEND":
                    if (poly is not null && poly.Count >= 2)
                    {
                        for (var i = 0; i < poly.Count - 1; i++)
                            AddPolySeg(poly[i], poly[i + 1], polyBulge![i], polyKind, segs);
                    }
                    poly = null;
                    polyBulge = null;
                    break;
                case "TEXT":
                case "MTEXT":
                case "DIMENSION":
                    TryText(type, props, texts);
                    break;
            }
        }
        return (segs, texts);
    }

    /// <summary>按图层名 + 线型分类：中心线 / 虚线（隐藏线）/ 实线</summary>
    private static string ClassifyKind(List<(int C, string V)> p)
    {
        var layer = Str(p, 8);
        var lt = Str(p, 6);
        var s = (layer + " " + lt).ToUpperInvariant();

        if (Regex.IsMatch(s, "CENTER|CENT|DASHDOT|AXIS|点划|中心|轴线"))
            return "center";
        if (Regex.IsMatch(s, "HIDDEN|DASH|虚线|隐藏"))
            return "hidden";
        return "solid";
    }

    private static void TryText(string type, List<(int C, string V)> p, List<Txt> texts)
    {
        var raw = Str(p, 1);
        if (string.IsNullOrWhiteSpace(raw) || raw.Trim() == "<>")
            return;

        var txt = CleanMText(raw);
        if (string.IsNullOrWhiteSpace(txt))
            return;

        // 定位点：优先对齐点 11/21，其次插入点 10/20
        var x = Num(p, 11) ?? Num(p, 10);
        var y = Num(p, 21) ?? Num(p, 20);
        if (x is null || y is null) return;
        texts.Add(new Txt(txt, x.Value, y.Value));
    }

    /// <summary>清理 MTEXT 控制码（\P 换行、字体/颜色/对齐等）</summary>
    private static string CleanMText(string raw)
    {
        var s = raw;
        s = s.Replace("\\P", " ").Replace("\\~", " ").Replace("\\%", "");
        s = Regex.Replace(s, @"\{|\}", "");
        s = Regex.Replace(s, @"\\[ACFfHhQTWwLlOoKk][^;]*;?", "");
        s = Regex.Replace(s, @"\\[SdpX]", " ");
        return s.Trim();
    }

    private static void TryLine(List<(int C, string V)> p, string kind, List<Seg> segs)
    {
        var x1 = Num(p, 10); var y1 = Num(p, 20);
        var x2 = Num(p, 11); var y2 = Num(p, 21);
        if (x1 is null || y1 is null || x2 is null || y2 is null) return;
        var s = new P2(x1.Value, y1.Value);
        var e = new P2(x2.Value, y2.Value);
        if (Dist(s, e) < 1e-6) return; // 跳过零长度段（不参与轮廓）
        segs.Add(new Seg { S = s, E = e, Kind = kind });
    }

    private static void TryArc(List<(int C, string V)> p, string kind, List<Seg> segs)
    {
        var cx = Num(p, 10); var cy = Num(p, 20); var r = Num(p, 40);
        var a1 = Num(p, 50); var a2 = Num(p, 51);
        if (cx is null || cy is null || r is null || a1 is null || a2 is null) return;
        var rad = Math.PI / 180.0;
        var s = new P2(cx.Value + r.Value * Math.Cos(a1.Value * rad),
                       cy.Value + r.Value * Math.Sin(a1.Value * rad));
        var e = new P2(cx.Value + r.Value * Math.Cos(a2.Value * rad),
                       cy.Value + r.Value * Math.Sin(a2.Value * rad));
        segs.Add(new Seg { S = s, E = e, R = r.Value, Dir = "ccw", Kind = kind });
    }

    private static void TryLwPolyline(List<(int C, string V)> p, string kind, List<Seg> segs)
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
            AddPolySeg(pts[i], pts[i + 1], bulges[i], kind, segs);
    }

    private static void AddPolySeg(P2 a, P2 b, double bulge, string kind, List<Seg> segs)
    {
        if (Math.Abs(bulge) < 1e-6)
        {
            segs.Add(new Seg { S = a, E = b, Kind = kind });
            return;
        }
        var theta = 4 * Math.Atan(bulge);
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var chord = Math.Sqrt(dx * dx + dy * dy);
        if (chord < 1e-6) return; // 跳过零长度段
        if (Math.Abs(Math.Sin(theta / 2)) < 1e-9) return;
        var r = chord / (2 * Math.Sin(Math.Abs(theta) / 2));
        segs.Add(new Seg { S = a, E = b, R = r, Dir = bulge > 0 ? "ccw" : "cw", Kind = kind });
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

    // ───────────────────── 内孔（虚线隐藏线） ─────────────────────

    private sealed record Bore(decimal X, decimal Z, string Note);

    /// <summary>虚线水平线：上下成对（±同 Y）→ 内孔。返回孔径/孔底 Z/通盲。</summary>
    private static List<Bore> DetectBores(List<Seg> hidden, double xMax, decimal zLeftMin, List<string> warnings)
    {
        // 只看水平虚线（|ΔY| 近 0）
        var cands = hidden
            .Where(s => s.R <= 0 && Math.Abs(s.S.Y - s.E.Y) < 0.05 && Math.Abs(s.S.X - s.E.X) > 1.5)
            .Select(s => (Y: Math.Round((s.S.Y + s.E.Y) / 2, 1), Xa: Math.Min(s.S.X, s.E.X), Xb: Math.Max(s.S.X, s.E.X)))
            .Where(c => Math.Abs(c.Y) > 1.0) // 过中心线附近的忽略
            .ToList();

        var bores = new List<Bore>();
        var usedKeys = new HashSet<double>();

        foreach (var g in cands.GroupBy(c => c.Y).OrderByDescending(g => g.Count()))
        {
            var y = Math.Abs(g.Key);
            if (usedKeys.Contains(y)) continue;
            // 上下成对：同 |Y| 有正负两支
            var mirror = cands.Where(c => Math.Abs(Math.Abs(c.Y) - y) < 0.05).ToList();
            if (mirror.Select(c => Math.Sign(c.Y)).Distinct().Count() < 2) continue;

            var dia = Dec(2 * y);
            var xMin = mirror.Min(c => c.Xa);
            var xMaxLocal = mirror.Max(c => c.Xb);
            var zFar = Dec(xMin - xMax);            // 孔最深处（离右端面最远）
            var zNear = Dec(xMaxLocal - xMax);      // 孔口
            var len = (double)(zNear - zFar);
            if (dia < 4 || len < 8) continue;       // 太小的不当内孔

            // 通孔：孔延伸到零件左端
            var through = Math.Abs(zFar - zLeftMin) < 3;
            var note = through
                ? $"内孔 Ø{dia} 通孔"
                : $"内孔 Ø{dia} 盲孔深{Fmt(len)}";

            bores.Add(new Bore(dia, zFar, note));
            usedKeys.Add(y);
        }

        return bores;
    }

    // ───────────────────── 文字标注识别 ─────────────────────

    private sealed record CylSection(decimal Dia, decimal ZLeft, decimal ZRight);

    private static void RecognizeTexts(List<Txt> texts, List<CncContourPointDto> points,
        double xMax, List<string> warnings, List<string> features)
    {
        var profile = points.Where(p => p.Type is "face" or "step" or "arc" or "chamfer").ToList();

        // 圆柱段：相邻同直径 step 点对
        var cyls = new List<CylSection>();
        for (var i = 0; i + 1 < profile.Count; i++)
        {
            var a = profile[i];
            var b = profile[i + 1];
            if (a.Type == "step" && b.Type == "step" && Math.Abs(a.X - b.X) < 0.05m)
                cyls.Add(new CylSection(a.X, Math.Min(a.Z, b.Z), Math.Max(a.Z, b.Z)));
        }

        var threaded = new HashSet<CylSection>();

        foreach (var t in texts)
        {
            var zT = Dec(t.X - xMax);

            // ── 螺纹 M / Tr ──
            var m = ReMetricThread.Match(t.Text);
            string? thText = null;
            decimal major = 0, pitch = 0;
            if (m.Success)
            {
                major = Dec(double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));
                pitch = m.Groups[2].Success
                    ? Dec(double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture))
                    : CoarsePitch.TryGetValue((int)Math.Round(major), out var cp) ? cp : 1.5m;
                thText = $"M{Fmt((double)major)}×{Fmt((double)pitch)}";
            }
            else
            {
                var tr = ReTrapThread.Match(t.Text);
                if (tr.Success)
                {
                    major = Dec(double.Parse(tr.Groups[1].Value, CultureInfo.InvariantCulture));
                    pitch = Dec(double.Parse(tr.Groups[2].Value, CultureInfo.InvariantCulture));
                    thText = $"Tr{Fmt((double)major)}×{Fmt((double)pitch)}";
                }
            }

            if (thText is not null)
            {
                var cands = cyls.Where(c => Math.Abs(c.Dia - major) <= 1m && c.ZRight - c.ZLeft >= 2m)
                    .OrderBy(c => Math.Min(Math.Abs(zT - c.ZLeft), Math.Abs(zT - (c.ZLeft + c.ZRight) / 2)))
                    .Where(c => !threaded.Contains(c))
                    .ToList();
                if (cands.Count > 0)
                {
                    var sec = cands[0];
                    threaded.Add(sec);
                    // 螺纹点插到该圆柱段左端点之后
                    var idx = points.FindIndex(p => p.Type == "step" && p.X == sec.Dia && p.Z == sec.ZLeft);
                    var tp = new CncContourPointDto
                    {
                        Type = "thread", X = sec.Dia, Z = sec.ZLeft, ThreadPitch = pitch,
                        Note = thText, Confidence = 0.9m, Source = "dxf",
                    };
                    if (idx >= 0) points.Insert(idx + 1, tp);
                    else points.Add(tp);
                    features.Add($"{thText} 螺纹（Ø{sec.Dia} 段 Z{Fmt((double)sec.ZRight)}~{Fmt((double)sec.ZLeft)}）");
                    continue;
                }
                warnings.Add($"螺纹标注 {thText}：轮廓中未找到 Ø{Fmt((double)major)}±1 圆柱段，请手动补螺纹点");
                continue;
            }

            // ── 沉割槽 宽×深 ──
            var gm = ReGroove.Match(t.Text);
            if (gm.Success)
            {
                var w = double.Parse(gm.Groups[1].Value, CultureInfo.InvariantCulture);
                var d = double.Parse(gm.Groups[2].Value, CultureInfo.InvariantCulture);
                if (w <= 10 && d > 0 && d <= 6)
                {
                    var note = $"槽{Fmt(w)}×{Fmt(d)}";
                    // 找槽谷：比两侧低的最低点，Z 距文字最近
                    // 找槽谷：直径下潜的低洼带（平底槽槽底为一段等径顶点，需容忍平底）
                    var bestDip = -1;
                    var bestZ = double.MaxValue;
                    for (var b = 1; b < profile.Count - 1; b++)
                    {
                        // 向右扩出同一槽底（等径连续顶点）
                        var L = b;
                        var floor = profile[b].X;
                        while (b + 1 < profile.Count - 1
                               && Math.Abs(profile[b + 1].X - floor) < 0.05m) b++;
                        var R = b;
                        // 槽底必须严格低于两侧墙体（外圈回上更大的直径才算沉割槽）
                        if (profile[L].X >= profile[L - 1].X || profile[R].X >= profile[R + 1].X)
                            continue;
                        var zMid = (double)(profile[L].Z + profile[R].Z) / 2;
                        var dzMid = Math.Abs(zMid - (double)zT);
                        if (dzMid < bestZ) { bestZ = dzMid; bestDip = L; }
                    }
                    if (bestDip >= 0 && bestZ <= 15)
                    {
                        profile[bestDip].Note = string.IsNullOrEmpty(profile[bestDip].Note)
                            ? note : profile[bestDip].Note + " " + note;
                        features.Add(note);
                        continue;
                    }
                    warnings.Add($"槽标注 {note}：轮廓中未找到对应沉割槽");
                    continue;
                }
            }

            // ── 倒角 C n / n×45 ──
            var cm = ReChamfer.Match(t.Text);
            decimal? cVal = null;
            if (cm.Success)
            {
                cVal = Dec(double.Parse(cm.Groups[1].Value, CultureInfo.InvariantCulture));
            }
            else
            {
                var cm2 = ReGroove.Match(t.Text);
                if (cm2.Success && cm2.Groups[2].Value == "45")
                    cVal = Dec(double.Parse(cm2.Groups[1].Value, CultureInfo.InvariantCulture));
            }
            if (cVal is > 0 and <= 8)
            {
                // 找 45° 斜线段终点（|ΔX/2| ≈ |ΔZ|）
                CncContourPointDto? diag = null;
                var best = double.MaxValue;
                for (var i = 0; i + 1 < profile.Count; i++)
                {
                    var a = profile[i];
                    var b = profile[i + 1];
                    var dz = Math.Abs((double)(b.Z - a.Z));
                    var dr = Math.Abs((double)(b.X - a.X) / 2);
                    if (dz < 0.2 || dr < 0.2) continue;
                    if (Math.Abs(dz - dr) / Math.Max(dz, dr) < 0.2 && dz <= 8)
                    {
                        var dd = Math.Abs((double)(b.Z - zT));
                        if (dd < best) { best = dd; diag = b; }
                    }
                }
                if (diag is not null && best <= 15)
                {
                    diag.Chamfer = cVal;
                    diag.Note = string.IsNullOrEmpty(diag.Note) ? $"C{Fmt((double)cVal)}" : diag.Note;
                    diag.Type = "chamfer";
                    features.Add($"C{Fmt((double)cVal)} 倒角");
                    continue;
                }
            }
        }
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

    private static string Str(List<(int C, string V)> p, int code)
    {
        for (var i = p.Count - 1; i >= 0; i--)
            if (p[i].C == code) return p[i].V;
        return "";
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
