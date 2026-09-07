using System.Globalization;
using System.Text;
using ManagementCNCWorkshop.Api.Models;

namespace ManagementCNCWorkshop.Api.Services.Programming;

/// <summary>
/// Fanuc 数控车程序生成引擎（按实际车间加工工艺）。
/// 规则：
///   1. G71 仅用于粗车；精车为逐点坐标编程（G01/G02/G03），不使用 G70；
///   2. 直径下切且小径段较长（> 10mm）→ 正向加工时刀具/刀柄与台阶干涉，拆成正反两道：
///      输出两个独立程序；反面程序重新以新右端面为 Z0，并注明垫铜皮夹持已加工外圆；
///   3. 小径段较短（≤ 10mm）→ 视为窄槽/退刀槽，切槽刀 G75，安排在精车前，防止切屑划伤精车面；
///      粗车轮廓直接跨过窄槽（G71 Type II），槽体由 G75 成形；
///   4. 未标注倒角的凸直角拐角（端面棱边、台阶肩面外缘）自动加去毛刺倒角，
///      默认 C0.2，程序参数 ChamferC 可调；用户显式标注的倒角/圆弧不做自动处理。
///   5. 刀架类型可选 刀塔(turret)/排刀(gang)：刀塔机每次换刀（T 调刀）前 Z 先退到 150 安全距离。
/// 输出为编程初稿，装夹方式、刀具参数与首件需上机前核对。
/// </summary>
public class FanucProgramGenerator
{
    /// <summary>小径段超过此 Z 向长度视为需调头，否则按窄槽 G75（mm）</summary>
    private const decimal TurnAroundThreshold = 10m;

    /// <summary>直径下切判定阈值（直径 mm）</summary>
    private const decimal UndercutEps = 1m;

    private sealed record Pt(decimal X, decimal Z, decimal? R, string? Dir, bool Marked, bool Hop = false);

    private sealed record ThreadOp(decimal MajorDia, decimal Pitch, decimal EndZ);

    /// <summary>ZLeft = 槽左壁（Z 较小），ZRight = 槽右壁/下切台阶（Z 较大）</summary>
    private sealed record GrooveOp(decimal FromX, decimal ToX, decimal ZLeft, decimal ZRight);

    private sealed class Plan
    {
        /// <summary>G71 粗车轮廓（净形状，跨槽连线，无倒角）</summary>
        public List<string> Rough { get; } = new();

        /// <summary>精车逐点路径（含倒角、跨槽安全移动）</summary>
        public List<string> Finish { get; } = new();

        public List<GrooveOp> Grooves { get; } = new();
        public List<ThreadOp> Threads { get; } = new();
    }

    public IReadOnlyList<string> GeneratePrograms(CncProgram p, IReadOnlyList<CncContourPoint> points)
    {
        static Pt ToPt(CncContourPoint x) =>
            new(x.X, x.Z, x.ArcR, x.ArcDir, x.Type is "chamfer" or "arc" || x.Chamfer is > 0);

        var prof = points
            .Where(x => x.Type is not ("thread" or "groove" or "bore" or "cutoff"))
            .OrderBy(x => x.Seq).Select(ToPt).ToList();

        if (prof.Count > 0 && prof[0].Z != 0m)
            prof.Insert(0, new Pt(prof[0].X, 0m, null, null, false));

        var threadPts = points.Where(x => x.Type == "thread").OrderBy(x => x.Seq).ToList();
        if (prof.Count < 2)
            return new[] { "(ERROR: PROFILE POINTS < 2 - CHECK CONTOUR POINTS)" };

        var splitIdx = -1;
        for (var i = 1; i < prof.Count; i++)
        {
            if (prof[i].X < prof[i - 1].X - UndercutEps &&
                Math.Abs(prof[i].Z - prof[i - 1].Z) <= 1m &&
                NeckLen(prof, i) > TurnAroundThreshold)
            {
                splitIdx = i;
                break;
            }
        }

        var no1 = p.Id % 4000 + 1;

        if (splitIdx < 0)
        {
            var th = threadPts.Select(x => new ThreadOp(x.X, x.ThreadPitch ?? 1.5m, x.Z)).ToList();
            var sb1 = new StringBuilder();
            sb1.AppendLine("(========== PROGRAM - SINGLE SETUP ==========)");
            sb1.AppendLine("(CLAMP: GRIP STOCK OD - ONE-SIDE MACHINING)");
            sb1.AppendLine("(DATUM: Z0 = RIGHT END FACE)");
            sb1.Append(Body(p, prof, th, no1, 0));
            return new[] { sb1.ToString() };
        }

        var front = prof.Take(splitIdx).ToList();
        var zLast = prof[^1].Z;
        var back = prof.Skip(splitIdx - 1).Reverse()
            .Select(x => new Pt(x.X, zLast - x.Z, x.R,
                x.Dir == "cw" ? "ccw" : x.Dir == "ccw" ? "cw" : x.Dir, x.Marked)).ToList();

        var boundaryZ = prof[splitIdx - 1].Z;
        var frontTh = threadPts.Where(x => x.Z >= boundaryZ)
            .Select(x => new ThreadOp(x.X, x.ThreadPitch ?? 1.5m, x.Z)).ToList();
        var backTh = threadPts.Where(x => x.Z < boundaryZ)
            .Select(x => new ThreadOp(x.X, x.ThreadPitch ?? 1.5m, zLast - x.Z)).ToList();

        var gripDia = front[0].X;
        var totalLen = prof[0].Z - zLast;

        var frontSb = new StringBuilder();
        frontSb.AppendLine("(========== PROGRAM 1 OF 2 - FRONT SIDE ==========)");
        frontSb.AppendLine("(CLAMP: GRIP STOCK OD - MACHINING RIGHT SIDE FIRST)");
        frontSb.AppendLine($"(SCOPE: RIGHT FACE Z0 + OD FROM Z0 TO Z{FmtZ(front[^1].Z)})");
        frontSb.AppendLine("(DATUM: Z0 = RIGHT END FACE)");
        frontSb.Append(Body(p, front, frontTh, no1, 1));

        var backSb = new StringBuilder();
        backSb.AppendLine("(========== PROGRAM 2 OF 2 - BACK SIDE ==========)");
        backSb.AppendLine($"(CLAMP: GRIP FINISHED DIA {FmtDia(gripDia)} WITH COPPER SHIM)");
        backSb.AppendLine($"(SCOPE: BACK FACE TO TOTAL LEN {FmtZ(totalLen)} + OD DIA {FmtDia(back.Max(x => x.X))})");
        backSb.AppendLine($"(DATUM: Z0 = NEW RIGHT FACE AFTER FACING, TOTAL LEN {FmtZ(totalLen)})");
        backSb.Append(Body(p, back, backTh, no1 + 1, 2));

        return new[] { frontSb.ToString(), backSb.ToString() };
    }

    public string Generate(CncProgram p, IReadOnlyList<CncContourPoint> points) =>
        string.Join("\n\n", GeneratePrograms(p, points));

    // ───────────── 单面程序体：端面 → G71 粗车 → G75 窄槽 → 逐点精车 → G76 螺纹 ─────────────

    private static string Body(CncProgram p, List<Pt> prof, List<ThreadOp> threads, int progNo, int sideNo)
    {
        var tool = p.ToolNo ?? 1;
        var roughTool = tool;
        var finishTool = tool + 1;
        var grooveTool = tool + 2;
        var threadTool = tool + 3;

        var stockDia = p.StockDia ?? prof.Max(x => x.X) + 4m;
        // 材料匹配：命中材料库则按行业经验参数生成（硬质合金刀具），程序参数优先
        var mat = CncMaterialDefaults.Find(p.Material);
        var cutDepth = p.PerCutDepth ?? mat?.ApRough ?? 2m;
        var roughFeed = p.Feed ?? mat?.FeedRough ?? 0.2m;
        var finishFeed = mat?.FeedFinish ?? 0.08m;
        var faceFeed = mat?.FeedFace ?? 0.12m;
        var grooveFeed = mat?.FeedGroove ?? 0.06m;
        var grooveWidth = p.GrooveWidth ?? 3m;
        var c = Math.Max(0m, p.ChamferC ?? 0.2m);
        // 转速：用户输入优先；否则按材料线速度与首段直径换算 n=1000·Vc/(π·D)，上限 3000 防超机床范围
        const decimal Pi = 3.1416m;
        var dia1 = Math.Max(prof[0].X, 1m);
        var rpm = p.Rpm ?? (mat is null ? 800m : Math.Min(3000m, Math.Round(1000m * mat.VcRough / (Pi * dia1), 0)));
        var finishRpm = mat is null ? Math.Round(rpm * 1.3m, 0) : Math.Min(3000m, Math.Round(1000m * mat.VcFinish / (Pi * dia1), 0));
        var grooveRpm = Math.Round(rpm / 2m, 0);
        var xAllow = (p.RoughAllowance ?? 0.25m) * 2m;
        // 精车退刀点：比最大外圆高 0.2（末段径向退出必须清开最大外圆）
        var finishClearX = prof.Max(x => x.X) + 0.4m;
        var startZ = 2m;
        var faceX = stockDia + 2m;
        // 刀架类型：turret 刀塔（每次换刀前 Z 须退到 ≥150 安全换刀距离）/ gang 排刀（无换刀动作）
        var isTurret = !string.Equals(p.ToolPost, "gang", StringComparison.OrdinalIgnoreCase);
        var safeZ = 150m;
        // 精车快速定位起点：粗车后起始段实际表面（净尺寸+实际余量≈0.2）+ 1mm，不再抬到毛坯面外
        var finishApproachX = prof[0].X + 0.2m + 1m;
        var faceAllowance = 0.1m;
        var controlSystem = string.IsNullOrWhiteSpace(p.ControlSystem) ? "FANUC" : p.ControlSystem.Trim();

        var plan = BuildPlan(prof, threads, c, roughFeed, finishFeed);
        var sb = new StringBuilder();

        sb.AppendLine($"O{progNo:D4}");
        sb.AppendLine($"(CONTROL:{Ascii(controlSystem)} MATERIAL:{Ascii(p.Material ?? "-")} PART:{Ascii(p.PartName)})");
        sb.AppendLine($"(STOCK DIA {FmtDia(stockDia)}  FINISH ALLOW {FmtR(p.RoughAllowance ?? 0.25m)}  UNMARKED CHAMFER C{FmtR(c)})");
        sb.AppendLine(mat is null
            ? "(CUT PARAMS: DEFAULT - PICK MATERIAL FOR OPTIMIZED VC/F)"
            : $"(CUT PARAMS: VC ROUGH {FmtR(mat.VcRough)} FINISH {FmtR(mat.VcFinish)}  F ROUGH {FmtR(roughFeed)} FINISH {FmtR(finishFeed)} FACE {FmtR(faceFeed)} GROOVE {FmtR(grooveFeed)})");
        sb.AppendLine(isTurret
            ? $"(TOOL POST: TURRET - RETRACT Z{FmtZ(safeZ)} BEFORE EACH TOOL INDEX)"
            : "(TOOL POST: GANG - NO TURRET INDEX)");
        sb.AppendLine("(G71 ROUGH ONLY - FINISH IS POINT-TO-POINT, NO G70)");
        sb.AppendLine("G99 G40 G21");
        sb.AppendLine();
        var op = 1;

        // ── OP1 平端面（反面程序此面即车到总长基准） ──
        sb.AppendLine($"(OP{op} - FACE, LEAVE {FmtR(faceAllowance)} FOR FINISH)");
        sb.AppendLine($"T{roughTool:D2}{roughTool:D2}");
        sb.AppendLine($"G97 S{rpm:0.#} M03");
        sb.AppendLine($"G00 X{FmtDia(faceX)} Z{FmtZ(startZ)}");
        sb.AppendLine($"G00 Z{FmtZ(faceAllowance)}");
        sb.AppendLine($"G01 X-0.2 F{faceFeed:0.##}");
        sb.AppendLine($"G00 X{FmtDia(faceX)}");
        sb.AppendLine($"G00 Z{FmtZ(startZ)}"); // 同一把刀接 G71，起点须在 Z2，不退换刀距离
        sb.AppendLine();
        op++;

        // ── OP2 G71 粗车 ──
        if (plan.Rough.Count > 0)
        {
            sb.AppendLine($"(OP{op} - OD ROUGH G71)");
            sb.AppendLine($"G71 U{cutDepth:0.#} R0.5");
            sb.AppendLine($"G71 P100 Q{100 + (plan.Rough.Count - 1) * 10} U{xAllow:0.###} W0 F{roughFeed:0.##}");
            var b = 100;
            foreach (var line in plan.Rough)
            {
                sb.AppendLine($"N{b:D3} {line}");
                b += 10;
            }
            sb.AppendLine($"G00 X{FmtDia(faceX)}");
            // 后续换刀：刀塔机退到安全换刀距离，排刀机只退 Z10
            sb.AppendLine(isTurret ? $"G00 Z{FmtZ(safeZ)}" : $"G00 Z{FmtZ(10m)}");
            sb.AppendLine();
            op++;
        }

        // ── OP3 窄槽 G75（精车前切，防切屑划伤精车面） ──
        foreach (var g in plan.Grooves)
        {
            var depth = (g.FromX - g.ToX) / 2m;
            var peeks = Math.Max(1, (int)Math.Ceiling(depth / cutDepth));
            var perPeekUm = (long)Math.Round(depth / peeks * 1000m);
            // 左刀尖对刀：从槽左壁起切，每次偏移刀宽-0.3 直至覆盖右壁
            var startP = g.ZLeft;
            var endP = Math.Max(g.ZLeft, g.ZRight - grooveWidth);
            var qShiftUm = (long)Math.Round(Math.Max(0.2m, grooveWidth - 0.3m) * 1000m);
            sb.AppendLine($"(OP{op} - GROOVE G75 BEFORE FINISH)");
            sb.AppendLine($"(CUT DIA {FmtDia(g.FromX)} -> {FmtDia(g.ToX)} AT Z{FmtZ(g.ZRight)}..Z{FmtZ(g.ZLeft)}  TOOL W{FmtR(grooveWidth)})");
            if (grooveWidth > g.ZRight - g.ZLeft + 0.01m)
                sb.AppendLine("(WARN: TOOL WIDER THAN GROOVE - CHECK)");
            sb.AppendLine($"T{grooveTool:D2}{grooveTool:D2}");
            sb.AppendLine($"G97 S{grooveRpm:0.#} M03");
            sb.AppendLine($"G00 X{FmtDia(g.FromX + 2)} Z{FmtZ(startP)}");
            sb.AppendLine("G75 R0.3");
            sb.AppendLine($"G75 X{FmtDia(g.ToX)} Z{FmtZ(endP)} P{perPeekUm} Q{qShiftUm} F{grooveFeed:0.##}");
            sb.AppendLine($"G00 X{FmtDia(g.FromX + 2)}");
            sb.AppendLine(isTurret ? $"G00 Z{FmtZ(safeZ)}" : $"G00 Z{FmtZ(10m)}");
            sb.AppendLine();
            op++;
        }

        // ── OP4 精车：先精车端面（收掉粗车留的 0.1，反面程序在此定总长），再逐点精车轮廓 ──
        if (plan.Finish.Count > 0)
        {
            sb.AppendLine($"(OP{op} - FINISH FACE + OD POINT-TO-POINT, UNMARKED CHAMFER C{FmtR(c)})");
            sb.AppendLine($"T{finishTool:D2}{finishTool:D2}");
            sb.AppendLine($"G97 S{finishRpm:0.#} M03");
            // 精车定位：G71 已清掉端面外圈毛坯，快移到粗车后实际表面+1mm 即可（不再抬到毛坯面外空走）
            // 随后 G00 Z0 + G01 X-0.2 收掉粗车留的 0.1 端面余量（反面程序在此定总长）
            sb.AppendLine($"G00 X{FmtDia(finishApproachX)} Z{FmtZ(startZ)}");
            sb.AppendLine("G00 Z0");
            sb.AppendLine($"G01 X-0.2 F{finishFeed:0.##}");
            sb.AppendLine("G00 Z0.5"); // 抬刀后径向退出，避免刀尖在已精车端面上拖划
            // 切完端面直接快移到倒角/轮廓起点（plan.Finish 首行为 G00 X），不再二次停靠
            foreach (var line in plan.Finish)
                sb.AppendLine(line);
            sb.AppendLine("G01 U0.1 W-0.5"); // 末端赶毛刺：出刀时斜向赶掉接刀毛刺
            sb.AppendLine($"G00 X{FmtDia(finishClearX)}");
            // 两次加工：正面程序在分界处给棒料外圆棱边倒角，翻面搬运不刮手
            if (sideNo == 1 && stockDia > prof[^1].X + UndercutEps)
            {
                var endZ = prof[^1].Z;
                sb.AppendLine($"G00 Z{FmtZ(endZ + 2)}");
                sb.AppendLine($"G00 X{FmtDia(faceX)}");
                sb.AppendLine($"G00 Z{FmtZ(endZ)}");
                sb.AppendLine($"G01 X{FmtDia(stockDia - 2 * c)} F{finishFeed:0.##}");
                sb.AppendLine($"G01 X{FmtDia(stockDia)} Z{FmtZ(endZ - c)} F0.03");
                sb.AppendLine($"G00 X{FmtDia(faceX)}");
            }
            sb.AppendLine(isTurret ? $"G00 Z{FmtZ(safeZ)}" : $"G00 Z{FmtZ(10m)}");
            sb.AppendLine();
            op++;
        }

        // ── OP5 螺纹 G76 ──
        foreach (var t in plan.Threads)
        {
            sb.AppendLine($"(OP{op} - THREAD G76)");
            sb.AppendLine($"(THREAD DIA {FmtDia(t.MajorDia)} PITCH {FmtR(t.Pitch)} TO Z{FmtZ(t.EndZ)})");
            sb.AppendLine($"T{threadTool:D2}{threadTool:D2}");
            sb.AppendLine($"G97 S{Math.Round(rpm / 2m, 0):0.#} M03");
            sb.AppendLine($"G00 X{FmtDia(t.MajorDia + 4)} Z{FmtZ(2m)}");
            var minorDia = t.MajorDia - 1.3m * t.Pitch;
            var heightUm = (long)Math.Round(0.65m * t.Pitch * 1000m);
            sb.AppendLine("G76 P010060 Q100 R0.05");
            sb.AppendLine($"G76 X{FmtDia(minorDia)} Z{FmtZ(t.EndZ)} P{heightUm} Q200 F{t.Pitch:0.###}");
            sb.AppendLine($"G00 X{FmtDia(faceX)}");
            sb.AppendLine(isTurret ? $"G00 Z{FmtZ(safeZ)}" : $"G00 Z{FmtZ(10m)}");
            sb.AppendLine();
            op++;
        }

        sb.AppendLine("(CHECK: TOOL OFFSETS / CHAMFERS / FIRST ARTICLE SINGLE-BLOCK)");
        sb.AppendLine("M05");
        sb.AppendLine("M30");
        sb.AppendLine("%");
        return sb.ToString();
    }

    // ───────────── 工艺分段 + 路径生成 ─────────────

    private static Plan BuildPlan(List<Pt> prof, List<ThreadOp> threads, decimal c,
        decimal roughFeed, decimal finishFeed)
    {
        var plan = new Plan();
        plan.Threads.AddRange(threads);

        var n = prof.Count;
        var p0 = prof[0];

        // ── G71 粗车轮廓：净形状、跨槽直线连接（不加倒角）。
        //    含窄槽 → 首块带 X+Z（Type II，容忍槽的下切）；纯单调轮廓 → Type I，兼容更老的系统。
        var profileMoves = new List<string>(); // j=1..n-1 的轮廓移动（供粗车/精车共用判断）
        var wps = new List<Pt>(); // 精车路径点（跨槽处标记 Hop）
        var prev = p0;
        var hasDip = false;

        // 精车起点：端面棱边倒角（给进贴面到端面上方 0.05 → 45° 落到轮廓）。
        // 靠近工件一律给进逼近：快移直落 Z0 有让刀/超程扎端面的风险。
        var startChamfer = c > 0 && n > 1 && prof[1].R is null && !prof[1].Marked &&
                           prof[1].X == p0.X && prof[1].Z < p0.Z;
        if (startChamfer)
        {
            // 倒角起点 X 取 45° 倒角线在 Z0.05 处的延伸点（净倒角几何不变，仍从端面棱边起切）
            plan.Finish.Add($"G00 X{FmtDia(p0.X - 2 * c - 0.1m)}");
            plan.Finish.Add($"G01 Z{FmtZ(p0.Z + 0.05m)} F0.1");
            plan.Finish.Add($"G01 X{FmtDia(p0.X)} Z{FmtZ(p0.Z - c)} F0.03");
        }
        else
        {
            plan.Finish.Add($"G00 X{FmtDia(p0.X)}");
            plan.Finish.Add($"G01 Z{FmtZ(p0.Z + 0.05m)} F0.1");
        }

        for (var j = 1; j < n; j++)
        {
            var cur = prof[j];

            // 圆弧
            if (cur.R is > 0)
            {
                var dir = string.Equals(cur.Dir, "cw", StringComparison.OrdinalIgnoreCase) ? "G02" : "G03";
                var arc = $"{dir} X{FmtDia(cur.X)} Z{FmtZ(cur.Z)} R{FmtR(cur.R.Value)}";
                profileMoves.Add(arc);
                wps.Add(cur);
                prev = cur;
                continue;
            }

            // 下切（能走到这里的必为短槽；长下切已分面）
            if (cur.X < prev.X - UndercutEps && Math.Abs(cur.Z - prev.Z) <= 1m)
            {
                hasDip = true;
                var zLeft = NeckEndZ(prof, j);
                plan.Grooves.Add(new GrooveOp(prev.X, cur.X, zLeft, cur.Z));

                // 粗车轮廓直线跨过槽（槽体由 G75 成形）
                var k = j;
                while (k + 1 < n && prof[k + 1].X == cur.X) k++;
                if (k + 1 < n)
                {
                    var after = prof[k + 1];
                    profileMoves.Add($"G01 X{FmtDia(after.X)} Z{FmtZ(after.Z)}");
                    wps.Add(after with { Hop = true });
                    prev = after;
                    j = k + 1;
                }
                else
                {
                    prev = cur; // 槽延伸到轮廓末端：轮廓止于槽前
                }
                continue;
            }

            var line = cur.X == prev.X
                ? $"G01 Z{FmtZ(cur.Z)}"
                : cur.Z == prev.Z
                    ? $"G01 X{FmtDia(cur.X)}"
                    : $"G01 X{FmtDia(cur.X)} Z{FmtZ(cur.Z)}";
            profileMoves.Add(line);
            wps.Add(cur);
            prev = cur;
        }

        // 粗车轮廓头：先 X 进到轮廓起点直径（Type I），Type II 需要首块同时含 Z
        if (hasDip)
        {
            plan.Rough.Add($"G00 X{FmtDia(p0.X)} Z{FmtZ(p0.Z)}"); // Type II
        }
        else
        {
            plan.Rough.Add($"G00 X{FmtDia(p0.X)}");               // Type I
            plan.Rough.Add($"G01 Z{FmtZ(p0.Z)} F{roughFeed:0.##}");
        }
        plan.Rough.AddRange(profileMoves);

        // ── 精车路径：凸直角拐角插倒角，末点径向外扩时补端部倒角，跨槽安全移动 ──
        var curX = p0.X;
        var curZ = p0.Z;
        var m = wps.Count;
        for (var k = 0; k < m; k++)
        {
            var w = wps[k];

            if (w.R is > 0)
            {
                var dir = string.Equals(w.Dir, "cw", StringComparison.OrdinalIgnoreCase) ? "G02" : "G03";
                plan.Finish.Add($"{dir} X{FmtDia(w.X)} Z{FmtZ(w.Z)} R{FmtR(w.R.Value)}");
                curX = w.X;
                curZ = w.Z;
                continue;
            }

            if (w.Hop)
            {
                if (w.X == curX)
                {
                    plan.Finish.Add($"G01 Z{FmtZ(w.Z)}");
                }
                else
                {
                    plan.Finish.Add($"G00 X{FmtDia(Math.Max(curX, w.X) + 2)}");
                    plan.Finish.Add($"G00 Z{FmtZ(w.Z)}");
                    plan.Finish.Add($"G01 X{FmtDia(w.X)}");
                }
                curX = w.X;
                curZ = w.Z;
                continue;
            }

            // 拐角倒角判断
            var inX = k == 0 ? p0.X : wps[k - 1].X;
            var inZ = k == 0 ? p0.Z : wps[k - 1].Z;
            var adjacentBlocked = w.Marked ||
                                  (k > 0 && (wps[k - 1].R is > 0 || wps[k - 1].Hop)) ||
                                  (k + 1 < m && (wps[k + 1].R is > 0 || wps[k + 1].Hop));

            decimal d1r = 0, d1z = 0, d2r = 0, d2z = 0;
            var consider = c > 0 && !adjacentBlocked;
            if (consider)
            {
                d1r = (w.X - inX) / 2m;
                d1z = w.Z - inZ;
                if (k + 1 < m)
                {
                    d2r = (wps[k + 1].X - w.X) / 2m;
                    d2z = wps[k + 1].Z - w.Z;
                }
                else
                {
                    if (d1z == 0 && d1r > 0) { d2r = 0; d2z = -1m; }
                    else consider = false;
                }
            }

            var len1 = Sqrt(d1r * d1r + d1z * d1z);
            var len2 = Sqrt(d2r * d2r + d2z * d2z);
            var cross = d1r * d2z - d1z * d2r;
            var isRightAngle =
                (Math.Abs(d1r) < 0.001m && Math.Abs(d2z) < 0.001m && Math.Abs(d1z) > 0.001m && Math.Abs(d2r) > 0.001m) ||
                (Math.Abs(d1z) < 0.001m && Math.Abs(d2r) < 0.001m && Math.Abs(d1r) > 0.001m && Math.Abs(d2z) > 0.001m);

            if (consider && cross < 0 && len1 > 0 && len2 > 0 && isRightAngle)
            {
                var aR = w.X / 2m - c * d1r / len1;
                var aZ = w.Z - c * d1z / len1;
                var bR = w.X / 2m + c * d2r / len2;
                var bZ = w.Z + c * d2z / len2;
                var aX = aR * 2m;
                var bX = bR * 2m;
                plan.Finish.Add(Ln(curX, curZ, aX, aZ));
                plan.Finish.Add(LnChamfer(aX, aZ, bX, bZ)); // 倒角段慢速 F0.03
                curX = bX;
                curZ = bZ;
            }
            else
            {
                plan.Finish.Add(Ln(curX, curZ, w.X, w.Z));
                curX = w.X;
                curZ = w.Z;
            }
        }

        // 进给字：倒角段生成时已挂慢速 F0.03，其余切削移动逐行显式挂精车进给（倒角后必须恢复，避免模态沿用慢速）
        for (var i = 2; i < plan.Finish.Count; i++)
        {
            var l = plan.Finish[i];
            if ((l.StartsWith("G01") || l.StartsWith("G02") || l.StartsWith("G03")) && !l.Contains(" F"))
                plan.Finish[i] = $"{l} F{finishFeed:0.##}";
        }

        return plan;
    }

    private static string Ln(decimal fx, decimal fz, decimal tx, decimal tz) =>
        fx == tx ? $"G01 Z{FmtZ(tz)}" :
        fz == tz ? $"G01 X{FmtDia(tx)}" :
        $"G01 X{FmtDia(tx)} Z{FmtZ(tz)}";

    /// <summary>倒角段：斜线移动挂慢速进给 F0.03（倒角面光洁度要求高）</summary>
    private static string LnChamfer(decimal fx, decimal fz, decimal tx, decimal tz) =>
        fx == tx ? $"G01 Z{FmtZ(tz)} F0.03" :
        fz == tz ? $"G01 X{FmtDia(tx)} F0.03" :
        $"G01 X{FmtDia(tx)} Z{FmtZ(tz)} F0.03";

    private static decimal Sqrt(decimal v) => (decimal)Math.Sqrt((double)v);

    /// <summary>下切段末端 Z（沿 X == prof[i].X 向左走直到 X 变化）</summary>
    private static decimal NeckEndZ(List<Pt> prof, int i)
    {
        var end = prof[i].Z;
        for (var j = i + 1; j < prof.Count; j++)
        {
            if (prof[j].X != prof[i].X) break;
            end = prof[j].Z;
        }
        return end;
    }

    private static decimal NeckLen(List<Pt> prof, int i) => Math.Abs(NeckEndZ(prof, i) - prof[i].Z);

    // ───────────── 数值格式化 ─────────────

    private static string FmtDia(decimal v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    private static string FmtZ(decimal v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    private static string FmtR(decimal v) => v.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Ascii(string s)
    {
        var sb = new StringBuilder();
        foreach (var ch in s)
            sb.Append(ch < 128 ? ch : '?');
        return sb.ToString();
    }
}
