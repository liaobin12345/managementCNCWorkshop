namespace ManagementCNCWorkshop.Api.Models.Dtos;

public class DxfParseRequest
{
    public string? FileName { get; set; }
    public string? ContentBase64 { get; set; }
}

public class DxfParseResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<CncContourPointDto> Points { get; set; } = new();
}
