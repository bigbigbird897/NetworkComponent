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
                if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
                {
                    if (!context.Request.Headers.TryGetValue("X-Api-Key", out var key)
                        || !string.Equals(key.ToString(), _settings.ApiKey, StringComparison.Ordinal))
                    {
                        await WriteForbidden(context, 40101, "缺少或错误的 X-Api-Key 请求头");
                        return;
                    }
                }

                // 3. 客户端 IP 白名单（配置了才校验）；回环地址(本机)一律放行
                if (_settings.IpWhitelist != null && _settings.IpWhitelist.Count > 0)
                {
                    var ipAddr = context.Connection.RemoteIpAddress;
                    var ip = ipAddr?.MapToIPv4().ToString();
                    if (ipAddr != null && !IPAddress.IsLoopback(ipAddr)
                        && !_settings.IpWhitelist.Contains(ip))
                    {
                        _logger.LogWarning("来自未授权 IP {Ip} 的访问被拒绝", ip);
                        await WriteForbidden(context, 40303, $"客户端 IP {ip} 不在白名单内");
                        return;
                    }
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
