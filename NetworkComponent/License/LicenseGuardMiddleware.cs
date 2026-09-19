using System.Net;
using System.Text.Json;
using Common.License;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace NetworkComponent.License
{
    /// <summary>
    /// 授权与接口访问控制中间件。
    /// 对所有业务请求依次校验：授权状态 → API Key → 客户端 IP 白名单。
    /// Swagger 与 /api/License/* 放行，便于查看授权状态。
    /// </summary>
    public class LicenseGuardMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly LicenseSettings _settings;
        private readonly ILogger<LicenseGuardMiddleware> _logger;

        public LicenseGuardMiddleware(RequestDelegate next, LicenseSettings settings, ILogger<LicenseGuardMiddleware> logger)
        {
            _next = next;
            _settings = settings;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, LicenseManager licenseManager)
        {
            var path = context.Request.Path;

            // 放行 Swagger 与授权状态查询接口
            if (path.StartsWithSegments("/swagger")
                || path.StartsWithSegments("/api/License"))
            {
                await _next(context);
                return;
            }

            // 只拦截业务 API
            if (path.StartsWithSegments("/api"))
            {
                // 1. 授权校验
                var status = licenseManager.Validate();
                if (!status.IsAllowed)
                {
                    await WriteForbidden(context, 40301, $"未授权：{status.Message}。本机机器码：{status.MachineCode}，请联系软件商。");
                    return;
                }

                // 2. API Key 校验（配置了才校验）
                //"ApiKey": "nc-prod-a8f3k2",
                //if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
                //{
                //    if (!context.Request.Headers.TryGetValue("X-Api-Key", out var key)
                //        || !string.Equals(key.ToString(), _settings.ApiKey, StringComparison.Ordinal))
                //    {
                //        await WriteForbidden(context, 40101, "缺少或错误的 X-Api-Key 请求头");
                //        return;
                //    }
                //}

                // 3. 来源访问范围控制（写死在代码中，客户无法通过配置放开）：
                //    - 本机回环(127.0.0.1/::1) 一律放行；
                //    - 局域网非回环地址，必须由签名 license 开启 AllowLanAccess 才放行（增值授权）。
                var ipAddr = context.Connection.RemoteIpAddress;
                bool isLoopback = ipAddr != null && IPAddress.IsLoopback(ipAddr);
                if (!isLoopback && !status.AllowLanAccess)
                {
                    var ip = ipAddr?.MapToIPv4().ToString();
                    _logger.LogWarning("来自局域网 IP {Ip} 的访问被拒绝（未授权 LAN 访问）", ip);
                    await WriteForbidden(context, 40304,
                        $"局域网访问未授权（来源 IP {ip}）。本机调用请使用 localhost；如需局域网其他设备调用，请联系软件商开通 LAN 访问授权。");
                    return;
                }
            }

            await _next(context);
        }

        private static async Task WriteForbidden(HttpContext context, int subCode, string message)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json; charset=utf-8";
            var payload = JsonSerializer.Serialize(new
            {
                code = subCode,
                msg = message,
                data = (object?)null,
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            });
            await context.Response.WriteAsync(payload);
        }
    }
}
