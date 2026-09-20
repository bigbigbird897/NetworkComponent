using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Common.GlobalHelper;
using Common.LocalEntity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NetworkComponent.Controllers
{
    /// <summary>
    /// 后端运行配置（appsettings.json）查看与修改接口。
    /// 保存成功后后端以退出码 42 退出，由外层 WPF 监测到后自动重新拉起，实现"改配置即重启"。
    /// </summary>
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class ConfigController : ControllerBase
    {
        private readonly IHostEnvironment _env;
        private readonly ILogger<ConfigController> _logger;

        /// <summary>通知外层壳程序"请重启后端"的退出码</summary>
        public const int RestartExitCode = 42;

        public ConfigController(IHostEnvironment env, ILogger<ConfigController> logger)
        {
            _env = env;
            _logger = logger;
        }

        private string ConfigPath => Path.Combine(_env.ContentRootPath, "appsettings.json");

        /// <summary>读取当前 appsettings.json 的完整内容（对象形式）</summary>
        [HttpGet]
        public ApiUnifiedReturnStructure<object> Get()
        {
            try
            {
                if (!System.IO.File.Exists(ConfigPath))
                    return ApiReturnHelper.ServerError(null, "未找到 appsettings.json：" + ConfigPath);

                var json = System.IO.File.ReadAllText(ConfigPath);
                // appsettings.json 常带 // 注释和尾逗号，用容错选项解析，避免严格模式报错
                using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                });
                return ApiReturnHelper.Success((object)doc.RootElement.Clone());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "读取 appsettings.json 失败");
                return ApiReturnHelper.ServerError(null, "读取失败：" + ex.Message);
            }
        }

        /// <summary>保存 appsettings.json（请求体为完整 JSON），随后退出以便重启加载新配置</summary>
        [HttpPost]
        public ApiUnifiedReturnStructure<object> Save([FromBody] JsonElement body)
        {
            try
            {
                // 校验是合法 JSON 再写盘，避免写坏配置
                var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
                var json = JsonSerializer.Serialize(body, options);
                System.IO.File.WriteAllText(ConfigPath, json);
                _logger.LogInformation("appsettings.json 已更新，请求重启后端。");

                // 先把响应发回去，稍作延迟再退出（退出码 42 = 请求外层重启）
                _ = Task.Run(async () =>
                {
                    await Task.Delay(600);
                    Environment.Exit(RestartExitCode);
                });

                return ApiReturnHelper.Success(null, "已保存，后端即将重启以应用新配置。");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存 appsettings.json 失败");
                return ApiReturnHelper.ServerError(null, "保存失败：" + ex.Message);
            }
        }
    }
}
