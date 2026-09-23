using Microsoft.AspNetCore.Mvc;

namespace ArchitectureConfiguration
{
    /// <summary>
    /// 控制器属性注入基类：业务控制器继承本类后，无需构造函数注入服务，
    /// 直接在 <see cref="Service"/> 属性中获取业务服务。
    /// 由 Autofac 的 PropertiesAutowired（见 AutofacPropertityRegConfig）在解析控制器时自动注入。
    /// </summary>
    /// <typeparam name="TService">控制器所需的业务服务接口类型（须已在 Autofac 容器中注册）</typeparam>
    public abstract class ApiServiceControllerBase<TService> : ControllerBase
        where TService : class
    {
        /// <summary>
        /// 控制器使用的业务服务（Autofac 属性注入，容器中按 TService 注册的实例）。
        /// 调用时若为 null，说明该服务未注册到容器或属性注入未开启，需检查 Autofac 注册。
        /// </summary>
        public TService Service { get; set; } = null!;
    }
}
