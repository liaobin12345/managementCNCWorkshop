using Microsoft.AspNetCore.Mvc;

namespace ManagementCNCWorkshop.Api.Controllers;

/// <summary>健康检查：确认 API 服务是否正常运行</summary>
[ApiController]
[Route("api/health")]
[Tags("系统")]
public class HealthController : ControllerBase
{
    /// <summary>服务健康检查</summary>
    /// <returns>服务名称、状态和当前 UTC 时间</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Get() => Ok(new
    {
        name = "Management CNC Workshop",
        status = "ok",
        time = DateTime.UtcNow
    });
}
