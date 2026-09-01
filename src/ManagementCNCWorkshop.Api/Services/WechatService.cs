using System.Text.Json;

namespace ManagementCNCWorkshop.Api.Services;

/// <summary>微信 AppId/AppSecret 未配置时抛出</summary>
public class WechatNotConfiguredException : Exception
{
    public WechatNotConfiguredException(string message) : base(message) { }
}

/// <summary>微信小程序服务端能力：jscode2session 换取 openid</summary>
/// <remarks>
/// 依赖 appsettings.json 的 Wechat:AppId / Wechat:AppSecret 配置；
/// 未配置时抛出 <see cref="WechatNotConfiguredException"/>。
/// </remarks>
public class WechatService(HttpClient http, IConfiguration config)
{
    /// <summary>用登录 code 换取微信 openid</summary>
    public async Task<string> ExchangeCodeForOpenIdAsync(string code)
    {
        // 调试模式：不调微信接口，直接用固定 openid（仅本地/无 AppID 环境联调用，上线务必关闭）
        if (config.GetValue<bool>("Wechat:DebugMode"))
        {
            var debugOpenId = config["Wechat:DebugOpenId"];
            if (!string.IsNullOrWhiteSpace(debugOpenId))
                return debugOpenId;
        }

        var appId = config["Wechat:AppId"] ?? string.Empty;
        var appSecret = config["Wechat:AppSecret"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(appSecret))
            throw new WechatNotConfiguredException("未配置 Wechat:AppId / Wechat:AppSecret");

        var url = "https://api.weixin.qq.com/sns/jscode2session"
            + $"?appid={Uri.EscapeDataString(appId)}"
            + $"&secret={Uri.EscapeDataString(appSecret)}"
            + $"&js_code={Uri.EscapeDataString(code)}"
            + "&grant_type=authorization_code";

        using var resp = await http.GetAsync(url);
        var body = await resp.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        if (root.TryGetProperty("errcode", out var err) && err.GetInt32() != 0)
        {
            var errmsg = root.TryGetProperty("errmsg", out var msg) ? msg.GetString() : "未知错误";
            throw new InvalidOperationException($"微信接口返回错误（errcode={err.GetInt32()}）{errmsg}");
        }

        if (!root.TryGetProperty("openid", out var openIdProp) || string.IsNullOrWhiteSpace(openIdProp.GetString()))
            throw new InvalidOperationException($"微信接口未返回 openid：{body}");

        return openIdProp.GetString()!;
    }
}
