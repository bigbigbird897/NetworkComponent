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
    /// 记事本：把文本文件集中存放在后端固定目录 notes\ 下，提供列表/搜索/读取/保存/删除。
    /// 文件名只允许简单名称，禁止路径分隔符与 ".."，防止目录穿越。
    /// </summary>
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class NotesController : ControllerBase
    {
        private readonly IHostEnvironment _env;
        private readonly ILogger<NotesController> _logger;

        public NotesController(IHostEnvironment env, ILogger<NotesController> logger)
        {
            _env = env;
            _logger = logger;
        }

        /// <summary>记事本固定目录（程序运行目录\notes）</summary>
        private string NotesDir => Path.Combine(_env.ContentRootPath, "notes");

        /// <summary>把用户给的文件名解析成安全的完整路径；非法返回 null</summary>
        private string? SafePath(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            // 拒绝任何路径分隔符、父目录引用、绝对路径
            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return null;
            if (name.Contains("..") || name.Contains('/') || name.Contains('\\') || name.Contains(":")) return null;
            var full = Path.GetFullPath(Path.Combine(NotesDir, name));
            var root = Path.GetFullPath(NotesDir);
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return null;
            return full;
        }

        /// <summary>列出记事本文件，可按文件名关键字过滤</summary>
        [HttpGet]
        public ApiUnifiedReturnStructure<object> GetList([FromQuery] string? keyword)
        {
            try
            {
                Directory.CreateDirectory(NotesDir);
                var files = Directory.EnumerateFiles(NotesDir, "*", SearchOption.TopDirectoryOnly)
                    .Select(fi => new FileInfo(fi))
                    .Where(f => string.IsNullOrWhiteSpace(keyword) ||
                                f.Name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderByDescending(f => f.LastWriteTime)
                    .Select(f => new
                    {
                        name = f.Name,
                        size = f.Length,
                        modified = f.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
                    });
                return ApiReturnHelper.Success((object)files.ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "读取记事本列表失败");
                return ApiReturnHelper.ServerError(null, "读取失败：" + ex.Message);
            }
        }

        /// <summary>读取单个记事本内容</summary>
        [HttpGet]
        public ApiUnifiedReturnStructure<object> Get([FromQuery] string name)
        {
            try
            {
                var path = SafePath(name);
                if (path == null || !System.IO.File.Exists(path))
                    return ApiReturnHelper.ServerError(null, "文件不存在或名称非法：" + name);

                var content = System.IO.File.ReadAllText(path);
                return ApiReturnHelper.Success((object)new { name, content });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "读取记事本失败 {Name}", name);
                return ApiReturnHelper.ServerError(null, "读取失败：" + ex.Message);
            }
        }

        /// <summary>新建或保存记事本（name 为文件名，content 为全文）</summary>
        [HttpPost]
        public ApiUnifiedReturnStructure<object> Save([FromBody] NoteInput input)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(input?.Name))
                    return ApiReturnHelper.ServerError(null, "文件名不能为空");

                var path = SafePath(input.Name);
                if (path == null)
                    return ApiReturnHelper.ServerError(null, "文件名非法，禁止包含路径分隔符或 ..：" + input.Name);

                Directory.CreateDirectory(NotesDir);
                System.IO.File.WriteAllText(path, input.Content ?? string.Empty);
                _logger.LogInformation("记事本已保存：{Name}", input.Name);
                return ApiReturnHelper.Success(null, "已保存：" + input.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存记事本失败 {Name}", input?.Name);
                return ApiReturnHelper.ServerError(null, "保存失败：" + ex.Message);
            }
        }

        /// <summary>删除记事本</summary>
        [HttpPost]
        public ApiUnifiedReturnStructure<object> Delete([FromBody] NoteInput input)
        {
            try
            {
                var path = SafePath(input?.Name ?? string.Empty);
                if (path == null || !System.IO.File.Exists(path))
                    return ApiReturnHelper.ServerError(null, "文件不存在或名称非法：" + input?.Name);

                System.IO.File.Delete(path);
                _logger.LogInformation("记事本已删除：{Name}", input!.Name);
                return ApiReturnHelper.Success(null, "已删除：" + input.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除记事本失败 {Name}", input?.Name);
                return ApiReturnHelper.ServerError(null, "删除失败：" + ex.Message);
            }
        }
    }

    /// <summary>记事本保存/删除入参</summary>
    public class NoteInput
    {
        public string? Name { get; set; }
        public string? Content { get; set; }
    }
}
