using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Common.GlobalHelper;
using Common.LocalEntity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NetworkComponent.Controllers
{
    /// <summary>
    /// 日志查看：读取 Serilog 按天滚动的日志文件（logs\nc-YYYYMMDD.log）。
    /// 支持按日期筛选、查看内容、单个/批量删除。
    /// </summary>
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class LogController : ControllerBase
    {
        private readonly IHostEnvironment _env;
        private readonly ILogger<LogController> _logger;

        public LogController(IHostEnvironment env, ILogger<LogController> logger)
        {
            _env = env;
            _logger = logger;
        }

        /// <summary>日志固定目录（程序运行目录\logs）</summary>
        private string LogsDir => Path.Combine(_env.ContentRootPath, "logs");

        /// <summary>把用户给的文件名解析成 logs 目录内安全路径；非法返回 null</summary>
        private string? SafePath(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return null;
            if (name.Contains("..") || name.Contains('/') || name.Contains('\\') || name.Contains(":")) return null;
            var full = Path.GetFullPath(Path.Combine(LogsDir, name));
            var root = Path.GetFullPath(LogsDir);
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return null;
            return full;
        }

        /// <summary>列出日志文件；startDate/endDate 形如 yyyy-MM-dd，按日期范围（含首尾）筛选，任一为空表示该侧不限</summary>
        [HttpGet]
        public ApiUnifiedReturnStructure<object> GetList([FromQuery] string? startDate, [FromQuery] string? endDate)
        {
            try
            {
                if (!Directory.Exists(LogsDir))
                    return ApiReturnHelper.Success((object)new List<object>());

                DateTime? from = null, to = null;
                if (!string.IsNullOrWhiteSpace(startDate))
                {
                    if (!DateTime.TryParse(startDate, out var df))
                        return ApiReturnHelper.ServerError(null, "开始日期格式应为 yyyy-MM-dd：" + startDate);
                    from = df.Date;
                }
                if (!string.IsNullOrWhiteSpace(endDate))
                {
                    if (!DateTime.TryParse(endDate, out var dt))
                        return ApiReturnHelper.ServerError(null, "结束日期格式应为 yyyy-MM-dd：" + endDate);
                    to = dt.Date;
                }

                var result = Directory.EnumerateFiles(LogsDir, "nc-*.log", SearchOption.TopDirectoryOnly)
                    .Select(p => new FileInfo(p))
                    .Where(f =>
                    {
                        // 无任何日期条件则不过滤
                        if (from == null && to == null) return true;
                        // 文件名形如 nc-20260919.log
                        var stem = Path.GetFileNameWithoutExtension(f.Name); // nc-20260919
                        if (stem != null && stem.StartsWith("nc-") &&
                            DateTime.TryParseExact(stem.Substring(3), "yyyyMMdd",
                                System.Globalization.CultureInfo.InvariantCulture,
                                System.Globalization.DateTimeStyles.None, out var fileDate))
                        {
                            if (from != null && fileDate.Date < from.Value.Date) return false;
                            if (to != null && fileDate.Date > to.Value.Date) return false;
                            return true;
                        }
                        // 解析不出日期的文件：无范围条件才显示
                        return from == null && to == null;
                    })
                    .OrderByDescending(f => f.Name)
                    .Select(f => new
                    {
                        name = f.Name,
                        size = f.Length,
                        modified = f.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
                    });
                return ApiReturnHelper.Success((object)result.ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "读取日志列表失败");
                return ApiReturnHelper.ServerError(null, "读取失败：" + ex.Message);
            }
        }

        /// <summary>查看单个日志内容（默认返回末尾最多 maxLines 行，避免超大文件拖垮前端）</summary>
        [HttpGet]
        public ApiUnifiedReturnStructure<object> Content([FromQuery] string name, [FromQuery] int maxLines = 500)
        {
            try
            {
                var path = SafePath(name);
                if (path == null || !System.IO.File.Exists(path))
                    return ApiReturnHelper.ServerError(null, "日志文件不存在或名称非法：" + name);

                var lines = System.IO.File.ReadAllLines(path);
                var take = Math.Max(0, Math.Min(maxLines, lines.Length));
                var body = string.Join(Environment.NewLine, lines, lines.Length - take, take);
                return ApiReturnHelper.Success((object)new
                {
                    name,
                    totalLines = lines.Length,
                    returnedLines = take,
                    content = body
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "读取日志内容失败 {Name}", name);
                return ApiReturnHelper.ServerError(null, "读取失败：" + ex.Message);
            }
        }

        /// <summary>批量删除日志（body 为文件名数组），单个也走这里</summary>
        [HttpPost]
        public ApiUnifiedReturnStructure<object> Delete([FromBody] LogDeleteInput input)
        {
            try
            {
                if (input?.Names == null || input.Names.Count == 0)
                    return ApiReturnHelper.ServerError(null, "未选择要删除的日志");

                var ok = new List<string>();
                var fail = new List<string>();
                foreach (var n in input.Names)
                {
                    var path = SafePath(n);
                    if (path != null && System.IO.File.Exists(path))
                    {
                        try { System.IO.File.Delete(path); ok.Add(n); }
                        catch (Exception ex) { fail.Add(n + "(" + ex.Message + ")"); }
                    }
                    else fail.Add(n + "(不存在)");
                }
                _logger.LogInformation("删除日志：成功 {Ok}，失败 {Fail}", ok, fail);
                return ApiReturnHelper.Success((object)new { ok, fail },
                    fail.Count > 0 ? $"已删除 {ok.Count} 个，{fail.Count} 个失败" : $"已删除 {ok.Count} 个日志");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除日志失败");
                return ApiReturnHelper.ServerError(null, "删除失败：" + ex.Message);
            }
        }
    }

    /// <summary>日志批量删除入参</summary>
    public class LogDeleteInput
    {
        public List<string>? Names { get; set; }
    }
}
