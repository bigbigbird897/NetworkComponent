using ArchitectureConfiguration;
using Common.LocalEntity;
using Microsoft.AspNetCore.Mvc;
using ServiceComponentEnhance.Interfaces;
using ServiceComponentEnhance.LocalEntity;

namespace NetworkComponent.Controllers.ComponentEnhanceController
{
    /// <summary>
    /// 记事本接口：把文本文件集中存放在后端固定目录 notes\ 下，提供列表/搜索/读取/保存/删除。
    /// 业务逻辑下沉到 Service 层（INotesService），本控制器仅负责路由与参数绑定。
    /// </summary>
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class NotesController : ApiServiceControllerBase<INotesService>
    {
        /// <summary>列出记事本文件，可按文件名关键字过滤</summary>
        [HttpGet]
        public ApiUnifiedReturnStructure<object> GetList([FromQuery] string? keyword)
        {
            return Service.GetList(keyword);
        }

        /// <summary>读取单个记事本内容</summary>
        [HttpGet]
        public ApiUnifiedReturnStructure<object> Get([FromQuery] string name)
        {
            return Service.Get(name);
        }

        /// <summary>新建或保存记事本（name 为文件名，content 为全文）</summary>
        [HttpPost]
        public ApiUnifiedReturnStructure<object> Save([FromBody] NoteInput input)
        {
            return Service.Save(input);
        }

        /// <summary>删除记事本</summary>
        [HttpPost]
        public ApiUnifiedReturnStructure<object> Delete([FromBody] NoteInput input)
        {
            return Service.Delete(input);
        }
    }
}
