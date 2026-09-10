namespace ManagementCNCWorkshop.Api.Services.Programming;

public sealed record CncPostProfile(
    string Key,
    string DisplayName,
    string[] MatchKeywords,
    string InitBlock,
    string EndBlock,
    decimal SafeZTurret,
    decimal SafeZGang)
{
    public static readonly IReadOnlyList<CncPostProfile> All = new List<CncPostProfile>
    {
        new("fanuc0i", "Fanuc 0i-TF", new[] { "fanuc", "0i" }, "G99 G40 G21", "M05\nM30\n%", 150m, 10m),
        new("gsk980", "GSK 980TD", new[] { "gsk", "980" }, "G99 G40 G21", "M05\nM30\n%", 150m, 10m),
    };

    public static CncPostProfile Resolve(string? controlSystem)
    {
        if (!string.IsNullOrWhiteSpace(controlSystem))
        {
            var s = Normalize(controlSystem);
            foreach (var p in All)
            {
                foreach (var kw in p.MatchKeywords)
                {
                    if (s.Contains(Normalize(kw)))
                        return p;
                }
            }
            if (s.Contains("gsk") || s.Contains("980")) return All[1];
            if (s.Contains("fanuc")) return All[0];
        }
        return All[0];
    }

    private static string Normalize(string s) =>
        s.Trim().ToLowerInvariant().Replace(" ", "").Replace("-", "").Replace("_", "");
}
