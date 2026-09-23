using Common.LocalEntity;
using System.Text.Json;

namespace ServiceComponentEnhance.Interfaces
{
    /// <summary>
    /// 后端运行配置（appsettings.json）服务接口。
    /// 读取 / 保存配置，保存成功后请求外层壳程序重启后端以加载新配置。
    /// </summary>
    public interface IConfigService
    {
        /// <summary>读取当前 appsettings.json 的完整内容（对象形式），支持注释与尾逗号</summary>
        ApiUnifiedReturnStructure<object> Get();

        /// <summary>保存 appsettings.json（请求体为完整 JSON），随后以退出码 42 退出请求重启</summary>
        ApiUnifiedReturnStructure<object> Save(JsonElement body);
    }
}
