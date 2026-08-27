using ManagementCNCWorkshop.Api.Data;
using ManagementCNCWorkshop.Api.Models;
using ManagementCNCWorkshop.Api.Models.Dtos;
using Microsoft.EntityFrameworkCore;

namespace ManagementCNCWorkshop.Api.Services;

/// <summary>
/// 把流转卡+工序执行记录，结合扫码报工累计数量，
/// 组装成「产品工序进度总览」（含每道工序完成量、完工标识）。
/// </summary>
public static class ProcessCardProgressBuilder
{
    public static async Task<List<ProcessCardProgressDto>> BuildAsync(
        AppDbContext db, IReadOnlyList<ProcessCard> cards)
    {
        if (cards.Count == 0) return new List<ProcessCardProgressDto>();

        var cardIds = cards.Select(c => c.Id).ToList();

        // 报工数量按 流转卡+工序 汇总（实际完成量的来源）
        var sums = await db.WorkReports
            .Where(r => r.ProcessCardId.HasValue && cardIds.Contains(r.ProcessCardId.Value))
            .GroupBy(r => new { r.ProcessCardId, r.ProcessStepNo })
            .Select(g => new
            {
                g.Key.ProcessCardId,
                g.Key.ProcessStepNo,
                Qty = g.Sum(r => r.Quantity),
                QtyQ = g.Sum(r => r.QualifiedQty),
                QtyD = g.Sum(r => r.DefectQty)
            })
            .ToListAsync();

        var map = sums.ToDictionary(
            k => (k.ProcessCardId ?? 0, k.ProcessStepNo ?? 0),
            v => (v.Qty, v.QtyQ, v.QtyD));

        return cards.Select(card =>
        {
            var steps = card.CardSteps.OrderBy(s => s.StepNo).Select(s =>
            {
                map.TryGetValue((card.Id, s.StepNo), out var sum);
                return new StepProgressDto
                {
                    StepNo = s.StepNo,
                    StepName = s.StepName,
                    Status = s.Status,
                    StatusText = StepStatusText(s.Status),
                    Finished = s.Status == "Completed",
                    CompletedQty = sum.Qty,
                    QualifiedQty = sum.QtyQ,
                    DefectQty = sum.QtyD,
                    MachineNo = s.MachineNo,
                    WorkDate = s.WorkDate?.ToString("yyyy-MM-dd"),
                    Shift = s.Shift,
                    OperatorName = s.Operator?.Name,
                    InspectorName = s.Inspector?.Name,
                    Remark = s.Remark
                };
            }).ToList();

            var finished = card.Status == "Completed";
            var totalCompleted = steps.Sum(s => s.CompletedQty);
            var totalQualified = steps.Sum(s => s.QualifiedQty);
            var totalDefect = steps.Sum(s => s.DefectQty);

            return new ProcessCardProgressDto
            {
                CardId = card.Id,
                CardCode = card.Code,
                ProcessFlowId = card.ProcessFlowId,
                FlowName = card.ProcessFlow?.Name,
                ProductId = card.ProductId,
                ProductName = card.Product?.Name,
                ProductSpec = card.Product?.Specification,
                Quantity = card.Quantity,
                MaterialSpec = card.MaterialSpec,
                SurfaceTreatment = card.SurfaceTreatment,
                Status = card.Status,
                Finished = finished,
                StatusText = finished ? "已完工" : "加工中",
                CurrentStepNo = card.CurrentStepNo,
                TotalSteps = steps.Count,
                CompletedSteps = steps.Count(s => s.Finished),
                TotalCompletedQty = totalCompleted,
                TotalQualifiedQty = totalQualified,
                TotalDefectQty = totalDefect,
                CreatedAt = card.CreatedAt,
                StartedAt = card.StartedAt,
                CompletedAt = card.CompletedAt,
                Remark = card.Remark,
                Steps = steps
            };
        }).ToList();
    }

    public static string StepStatusText(string status) =>
        status == "Completed" ? "已完成" :
        status == "Running" ? "进行中" : "未开始";
}
