using ManagementCNCWorkshop.Api.Models;
using ManagementCNCWorkshop.Api.Models.Dtos;

namespace ManagementCNCWorkshop.Api.Services.Programming;

// 工艺方案预判：在编程前根据轮廓几何给出加工方式建议。
// 与实际车间一致的分级规则：
//   - 直径下切且小径段较长（> 10mm，在台阶/法兰背后）→ 正向加工刀具干涉，
//     必须调头正反面两次加工：生成两个程序（正面夹毛坯车右端；反面垫铜皮夹已加工外圆）；
//   - 无长下切：一次装夹；短下切（≤ 10mm）按窄槽，切槽刀 G75 在精车前完成；
//   - 长径比偏大时提示顶持提高刚性。
//   - 精车一律为逐点坐标编程（G01/G02/G03），G71 仅用于粗车。
public static class ProcessPlanner
{
    private const decimal TurnAroundThreshold = 10m;
    private const decimal UndercutEps = 1m;

    public static ProcessPlanDto Build(CncProgram p, IReadOnlyList<CncContourPoint> pts)
    {
        var prof = pts
            .Where(x => x.Type is not ("thread" or "groove" or "bore" or "cutoff"))
            .OrderBy(x => x.Seq).ToList();

        var maxDia = prof.Count > 0 ? prof.Max(x => x.X) : 0m;
        var totalLen = prof.Count > 1 ? Math.Abs(prof[^1].Z - prof[0].Z) : 0m;
        var lwr = maxDia <= 0 ? 0m : Math.Round(totalLen / maxDia, 2);

        // 扫描下切：长 → 调头；短 → 窄槽
        var splitIdx = -1;
        var neckBigDia = 0m;
        var neckSmallDia = 0m;
        var neckLen = 0m;
        var grooveCount = 0;
        for (var i = 1; i < prof.Count; i++)
        {
            if (prof[i].X >= prof[i - 1].X - UndercutEps) continue;
            if (Math.Abs(prof[i].Z - prof[i - 1].Z) > 1m) continue;

            var end = prof[i].Z;
            for (var j = i + 1; j < prof.Count; j++)
            {
                if (prof[j].X != prof[i].X) break;
                end = prof[j].Z;
            }
            var len = Math.Abs(end - prof[i].Z);
            if (len > TurnAroundThreshold)
            {
                if (splitIdx < 0)
                {
                    splitIdx = i;
                    neckBigDia = prof[i - 1].X;
                    neckSmallDia = prof[i].X;
                    neckLen = Math.Round(len, 1);
                }
            }
            else
            {
                grooveCount++;
            }
        }

        var hasThread = pts.Any(x => x.Type == "thread");
        var hasGroove = grooveCount > 0;
        var chamferC = p.ChamferC ?? 0.2m;
        var t = p.ToolNo ?? 1;
        var tf = t + 1;
        var tg = t + 2;
        var tt = t + 3;
        var gw = p.GrooveWidth?.ToString("0.##") ?? "待设";

        var mode = "single";
        var modeText = "";
        var steps = new List<string>();
        var advice = new List<string>();

        if (splitIdx >= 0)
        {
            var gripDia = prof[0].X;
            mode = "double";
            modeText = "建议两次加工（正反面调头车，将生成 2 个程序）";

            steps.Add($"程序1（正面·右端 Z0）：平端面 → G71 粗车（T{t:D2}）→ 精车逐点坐标（T{tf:D2}）");
            if (hasGroove)
                steps.Add($"程序1：窄槽 G75（T{tg:D2} 切槽刀，刀宽 {gw} mm，精车前切）");
            if (hasThread)
                steps.Add($"程序1：螺纹 G76（T{tt:D2}）");
            steps.Add($"程序2（反面·调头）：垫铜皮夹已加工外圆 Φ{gripDia:0.##} → 平端面定总长 → G71 粗车 → 精车逐点坐标");

            advice.Add($"Φ{neckSmallDia:0.##} 段（Z 向长 {neckLen} mm）在 Φ{neckBigDia:0.##} 台阶背后，正向加工刀柄干涉，须调头分两个程序完成");
            advice.Add("调头装夹已加工外圆必须垫铜皮，防止夹伤表面");
            if (lwr > 3)
                advice.Add("长径比偏大，反面加工建议尾座/中心架辅助顶持");
        }
        else
        {
            if (hasGroove)
            {
                modeText = "可一次装夹完成（含窄槽）";
                steps.Add($"OP1 平端面 + OP2 G71 粗车（T{t:D2}）");
                steps.Add($"OP3 窄槽 G75（T{tg:D2} 切槽刀，刀宽 {gw} mm，精车前切，防切屑划伤精车面）");
                steps.Add($"OP4 精车逐点坐标（T{tf:D2}）");
                if (hasThread) steps.Add($"OP5 螺纹 G76（T{tt:D2}）");
            }
            else
            {
                modeText = "可一次装夹完成";
                steps.Add($"OP1 平端面 + OP2 G71 粗车（T{t:D2}）");
                steps.Add($"OP3 精车逐点坐标（T{tf:D2}）");
                if (hasThread) steps.Add($"OP4 螺纹 G76（T{tt:D2}）");
                if (lwr > 5)
                    advice.Add("长径比偏大，建议尾座顶持提高刚性");
            }
        }

        advice.Add("G71 仅用于粗车；精车为逐点坐标编程（G01/G02/G03），未使用 G70");
        advice.Add($"未标注倒角的凸角按 C{chamferC:0.##} 自动去毛刺（参数页可改）");
        if (hasGroove && (p.GrooveWidth is null or 0))
            advice.Add("请在参数页填写切槽刀刀宽，程序按刀宽分刀切槽");
        if (hasThread)
            advice.Add("螺纹底径已按 1.3×螺距估算，重要螺纹请按手册核对");
        advice.Add("首件务必单段/空运行，核对换刀点与 Z0 方向");

        return new ProcessPlanDto
        {
            Mode = mode,
            ModeText = modeText,
            Steps = steps,
            Advice = advice,
            HasGroove = hasGroove,
            HasThread = hasThread,
            TotalLen = Math.Round(totalLen, 2),
            MaxDia = maxDia,
            LwrRatio = lwr,
        };
    }
}
