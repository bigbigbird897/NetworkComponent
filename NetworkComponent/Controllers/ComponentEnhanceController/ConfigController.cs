using ArchitectureConfiguration;
using Common.LocalEntity;
using Microsoft.AspNetCore.Mvc;
using ServiceComponentEnhance.Interfaces;
using System.Text.Json;

namespace NetworkComponent.Controllers.ComponentEnhanceController
{
    /// <summary>
    /// 后端运行配置（appsettings.json）查看与修改接口。
    /// 保存成功后后端以退出码 42 退出，由外层 WPF 监测到后自动重新拉起，实现"改配置即重启"。
    /// 业务逻辑下沉到 Service 层（IConfigService），本控制器仅负责路由与参数绑定。
    /// </summary>
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class ConfigController : ApiServiceControllerBase<IConfigService>
    {
        /// <summary>读取当前 appsettings.json 的完整内容（对象形式）</summary>
        [HttpGet]
        public ApiUnifiedReturnStructure<object> Get()
        {
            return Service.Get();
        }

        /// <summary>保存 appsettings.json（请求体为完整 JSON），随后退出以便重启加载新配置</summary>
        [HttpPost]
        public ApiUnifiedReturnStructure<object> Save([FromBody] JsonElement body)
        {
            return Service.Save(body);
        }
    }
}
