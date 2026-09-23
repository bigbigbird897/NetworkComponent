using ArchitectureConfiguration;
using Common.LocalEntity;
using Microsoft.AspNetCore.Mvc;
using ServiceComponentEnhance.Interfaces;
using ServiceComponentEnhance.LocalEntity;

namespace NetworkComponent.Controllers.ComponentEnhanceController
{
    /// <summary>
    /// 日志查看接口：读取 Serilog 按天滚动的日志文件（logs\nc-YYYYMMDD.log）。
    /// 支持按日期筛选、查看内容、单个/批量删除。
    /// 业务逻辑下沉到 Service 层（ILogService），本控制器仅负责路由与参数绑定。
    /// </summary>
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class LogController : ApiServiceControllerBase<ILogService>
    {
        /// <summary>列出日志文件；startDate/endDate 形如 yyyy-MM-dd，按日期范围（含首尾）筛选，任一为空表示该侧不限</summary>
        [HttpGet]
        public ApiUnifiedReturnStructure<object> GetList([FromQuery] string? startDate, [FromQuery] string? endDate)
        {
            return Service.GetList(startDate, endDate);
        }

        /// <summary>查看单个日志内容（默认返回末尾最多 maxLines 行，避免超大文件拖垮前端）</summary>
        [HttpGet]
        public ApiUnifiedReturnStructure<object> Content([FromQuery] string name, [FromQuery] int maxLines = 500)
        {
            return Service.Content(name, maxLines);
        }

        /// <summary>批量删除日志（body 为文件名数组），单个也走这里</summary>
        [HttpPost]
        public ApiUnifiedReturnStructure<object> Delete([FromBody] LogDeleteInput input)
        {
            return Service.Delete(input);
        }
    }
}
