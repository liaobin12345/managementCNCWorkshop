using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ManagementCNCWorkshop.Api.Controllers.Admin;

/// <summary>运营后台文件上传：设备/产品的现场照片</summary>
[ApiController]
[Route("api/admin/upload")]
[Authorize(Roles = "Admin")]
[Tags("运营后台-文件上传")]
public class AdminUploadController(IWebHostEnvironment env) : ControllerBase
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif"];
    private const long MaxFileSize = 5 * 1024 * 1024;

    /// <summary>上传图片，返回可访问的相对 URL</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upload(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "请选择要上传的图片文件" });
        if (file.Length > MaxFileSize)
            return BadRequest(new { message = "图片大小不能超过 5MB" });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return BadRequest(new { message = "仅支持 jpg / jpeg / png / webp / gif 格式图片" });

        var subDir = DateTime.Now.ToString("yyyyMM");
        var uploadRoot = Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "uploads", subDir);
        Directory.CreateDirectory(uploadRoot);

        var fileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(uploadRoot, fileName);
        await using (var stream = System.IO.File.Create(fullPath))
        {
            await file.CopyToAsync(stream);
        }

        return Ok(new { url = $"/uploads/{subDir}/{fileName}" });
    }
}
