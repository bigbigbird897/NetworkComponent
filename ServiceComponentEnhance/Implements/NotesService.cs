using Common.GlobalHelper;
using Common.LocalEntity;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ServiceComponentEnhance.Interfaces;
using ServiceComponentEnhance.LocalEntity;

namespace ServiceComponentEnhance.Implements
{
    /// <summary>
    /// 记事本服务实现：文本文件集中存放在后端固定目录 notes\ 下，
    /// 提供列表/搜索、读取、保存、删除；文件名做了防目录穿越校验。
    /// </summary>
    public class NotesService : INotesService
    {
        private readonly IHostEnvironment _env;
        private readonly ILogger<NotesService> _logger;

        public NotesService(IHostEnvironment env, ILogger<NotesService> logger)
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

        public ApiUnifiedReturnStructure<object> GetList(string? keyword)
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

        public ApiUnifiedReturnStructure<object> Get(string name)
        {
            try
            {
                var path = SafePath(name);
                if (path == null || !File.Exists(path))
                    return ApiReturnHelper.ServerError(null, "文件不存在或名称非法：" + name);

                var content = File.ReadAllText(path);
                return ApiReturnHelper.Success((object)new { name, content });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "读取记事本失败 {Name}", name);
                return ApiReturnHelper.ServerError(null, "读取失败：" + ex.Message);
            }
        }

        public ApiUnifiedReturnStructure<object> Save(NoteInput? input)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(input?.Name))
                    return ApiReturnHelper.ServerError(null, "文件名不能为空");

                var path = SafePath(input.Name);
                if (path == null)
                    return ApiReturnHelper.ServerError(null, "文件名非法，禁止包含路径分隔符或 ..：" + input.Name);

                Directory.CreateDirectory(NotesDir);
                File.WriteAllText(path, input.Content ?? string.Empty);
                _logger.LogInformation("记事本已保存：{Name}", input.Name);
                return ApiReturnHelper.Success(null, "已保存：" + input.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存记事本失败 {Name}", input?.Name);
                return ApiReturnHelper.ServerError(null, "保存失败：" + ex.Message);
            }
        }

        public ApiUnifiedReturnStructure<object> Delete(NoteInput? input)
        {
            try
            {
                var path = SafePath(input?.Name ?? string.Empty);
                if (path == null || !File.Exists(path))
                    return ApiReturnHelper.ServerError(null, "文件不存在或名称非法：" + input?.Name);

                File.Delete(path);
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
}
