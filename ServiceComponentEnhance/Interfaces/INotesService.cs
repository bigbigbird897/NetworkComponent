using Common.LocalEntity;
using ServiceComponentEnhance.LocalEntity;

namespace ServiceComponentEnhance.Interfaces
{
    /// <summary>
    /// 记事本服务接口：文本文件集中存放在后端固定目录 notes\ 下，
    /// 提供列表/搜索、读取、保存、删除；文件名只允许简单名称，防止目录穿越。
    /// </summary>
    public interface INotesService
    {
        /// <summary>列出记事本文件，可按文件名关键字过滤</summary>
        ApiUnifiedReturnStructure<object> GetList(string? keyword);

        /// <summary>读取单个记事本内容</summary>
        ApiUnifiedReturnStructure<object> Get(string name);

        /// <summary>新建或保存记事本（name 为文件名，content 为全文）</summary>
        ApiUnifiedReturnStructure<object> Save(NoteInput? input);

        /// <summary>删除记事本</summary>
        ApiUnifiedReturnStructure<object> Delete(NoteInput? input);
    }
}
