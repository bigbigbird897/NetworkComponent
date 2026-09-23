using Common.LocalEntity;
using ServiceComponentEnhance.LocalEntity;

namespace ServiceComponentEnhance.Interfaces
{
    /// <summary>
    /// 日志查看服务接口：读取 Serilog 按天滚动的日志文件（logs\nc-YYYYMMDD.log），
    /// 支持按日期范围筛选、查看内容、单个/批量删除。
    /// </summary>
    public interface ILogService
    {
        /// <summary>列出日志文件；startDate/endDate 形如 yyyy-MM-dd，按日期范围（含首尾）筛选，任一为空表示该侧不限</summary>
        ApiUnifiedReturnStructure<object> GetList(string? startDate, string? endDate);

        /// <summary>查看单个日志内容（默认返回末尾最多 maxLines 行，避免超大文件拖垮前端）</summary>
        ApiUnifiedReturnStructure<object> Content(string name, int maxLines);

        /// <summary>批量删除日志（单个也走这里），返回成功/失败清单</summary>
        ApiUnifiedReturnStructure<object> Delete(LogDeleteInput? input);
    }
}
