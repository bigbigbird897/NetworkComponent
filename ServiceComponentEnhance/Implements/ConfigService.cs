using Common.GlobalHelper;
using Common.LocalEntity;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServiceComponentEnhance.Interfaces;
using System.Text.Json;

namespace ServiceComponentEnhance.Implements
{
    /// <summary>
    /// 后端运行配置服务实现：读取 / 保存 appsettings.json。
    /// 保存成功后后端以退出码 42 退出，由外层 WPF 监测到后自动重新拉起，实现"改配置即重启"。
    /// </summary>
    public class ConfigService : IConfigService
    {
        /// <summary>通知外层壳程序"请重启后端"的退出码</summary>
        public const int RestartExitCode = 42;

        private readonly IHostEnvironment _env;
        private readonly ILogger<ConfigService> _logger;

        public ConfigService(IHostEnvironment env, ILogger<ConfigService> logger)
        {
            _env = env;
            _logger = logger;
        }

        /// <summary>appsettings.json 完整路径（程序内容根目录下）</summary>
        private string ConfigPath => Path.Combine(_env.ContentRootPath, "appsettings.json");

        public ApiUnifiedReturnStructure<object> Get()
        {
            try
            {
                if (!File.Exists(ConfigPath))
                    return ApiReturnHelper.ServerError(null, "未找到 appsettings.json：" + ConfigPath);

                var json = File.ReadAllText(ConfigPath);
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

        public ApiUnifiedReturnStructure<object> Save(JsonElement body)
        {
            try
            {
                // 校验是合法 JSON 再写盘，避免写坏配置
                var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
                var json = JsonSerializer.Serialize(body, options);
                File.WriteAllText(ConfigPath, json);
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
